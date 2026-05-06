import { useCallback, useEffect, useRef, useState } from "react";
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
  Pressable,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { router, useFocusEffect } from "expo-router";
import Icon from "react-native-vector-icons/Feather";

import { CookedRecipeCard } from "@/components/cookinghistory/CookedRecipeCard";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
  DialogClose,
} from "@/components/dialog";
import {
  useCookingHistory,
  useCookingHistoryCount,
  useDeleteCookingHistoryEntry,
  useClearCookingHistory,
  DEFAULT_ME_COOKS_PAGE_SIZE,
} from "@/hooks/useRecipeCooks";
import { useTheme } from "@/contexts/ThemeContext";
import type { MyCookedRecipeCardDto } from "@/types/recipes";
import { SearchBar } from "@/components/SearchBar";
import { toast } from "@/components/sonner";
import LoadingView from "@/components/ui/LoadingView";
import ErrorView from "@/components/ui/ErrorView";

export default function CookingHistoryScreen() {
  const { colors } = useTheme();
  const [deletingCookId, setDeletingCookId] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [page, setPage] = useState(1);
  const [historyItems, setHistoryItems] = useState<MyCookedRecipeCardDto[]>([]);
  const [hasMore, setHasMore] = useState(true);

  // Dialog states
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [clearAllDialogOpen, setClearAllDialogOpen] = useState(false);
  const [pendingDeleteItem, setPendingDeleteItem] = useState<{
    cookId: string;
    title: string;
  } | null>(null);

  const {
    data: historyResponse,
    isLoading: historyLoading,
    isError: historyError,
    isFetching,
    refetch: refetchHistory,
  } = useCookingHistory(page, DEFAULT_ME_COOKS_PAGE_SIZE, searchQuery);

  const { refetch: refetchCount } = useCookingHistoryCount();

  const deleteEntry = useDeleteCookingHistoryEntry();
  const clearHistory = useClearCookingHistory();

  // Track if this is the initial mount to avoid double-fetching
  const isInitialMount = useRef(true);

  useFocusEffect(
    useCallback(() => {
      // Skip refetch on initial mount - useQuery already fetches
      if (isInitialMount.current) {
        isInitialMount.current = false;
        return;
      }
      void refetchHistory();
      void refetchCount();
    }, [refetchHistory, refetchCount])
  );

  // Reset pagination when search changes
  useEffect(() => {
    setPage(1);
    setHistoryItems([]);
    setHasMore(true);
  }, [searchQuery]);

  // Sync query response to local state for pagination
  useEffect(() => {
    if (!historyResponse?.data) return;

    const pageItems = historyResponse.data;
    setHasMore(pageItems.length === DEFAULT_ME_COOKS_PAGE_SIZE);

    setHistoryItems((prev) => {
      // For page 1, replace entirely
      if (page === 1) return pageItems;
      // For subsequent pages, merge without duplicates
      const existingIds = new Set(prev.map((item) => item.cookId));
      const newItems = pageItems.filter((item) => !existingIds.has(item.cookId));
      return [...prev, ...newItems];
    });
  }, [historyResponse, page]);

  const handleRefresh = useCallback(async () => {
    setIsRefreshing(true);
    setPage(1);
  }, [refetchHistory, refetchCount]);

  useEffect(() => {
    if (!isRefreshing || page !== 1) return;
    let cancelled = false;
    (async () => {
      try {
        await Promise.all([refetchHistory(), refetchCount()]);
      } finally {
        if (!cancelled) setIsRefreshing(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isRefreshing, page, refetchHistory, refetchCount]);

  const handleDeleteEntry = useCallback((cookId: string, title: string) => {
    setPendingDeleteItem({ cookId, title });
    setDeleteDialogOpen(true);
  }, []);

  const confirmDeleteEntry = useCallback(async () => {
    if (!pendingDeleteItem) return;

    const { cookId } = pendingDeleteItem;
    setDeleteDialogOpen(false);
    setDeletingCookId(cookId);

    try {
      await deleteEntry.mutateAsync(cookId);
    } catch (error) {
      toast.error("Failed to delete recipe from history. Please try again.");
    } finally {
      setDeletingCookId(null);
      setPendingDeleteItem(null);
    }
  }, [deleteEntry, pendingDeleteItem]);

  const handleClearAll = useCallback(() => {
    if (historyItems.length === 0) return;
    setClearAllDialogOpen(true);
  }, [historyItems.length]);

  const confirmClearAll = useCallback(async () => {
    setClearAllDialogOpen(false);
    try {
      await clearHistory.mutateAsync();
    } catch (error) {
      toast.error("Failed to clear history. Please try again.");
    }
  }, [clearHistory]);

  const renderItem = useCallback(
    ({ item }: { item: MyCookedRecipeCardDto }) => (
        <CookedRecipeCard
          item={item}
          onPress={() =>
            router.push({
              pathname: "/(tabs)/recipe/[recipeId]",
              params: {
                recipeId: item.id,
                source: "cooking-history",
              },
            })
          }
          onDelete={() => handleDeleteEntry(item.cookId, item.title)}
        />
    ),
    [handleDeleteEntry, deletingCookId]
  );

  const renderEmpty = useCallback(
    () =>
      historyLoading || isFetching || isRefreshing ? null : (
        <View className="flex-1 items-center justify-center py-20">
          <Icon name="book-open" size={48} color={colors.textMuted} />
          <Text className="text-base mt-4 text-center px-8" style={{ color: colors.textSecondary }}>
            {searchQuery.trim()
              ? `No recipes found for "${searchQuery}"`
              : "No cooking history yet.\nComplete cooking a recipe to add it here!"}
          </Text>
          {!searchQuery.trim() && (
            <TouchableOpacity
              onPress={() => router.push("/")}
              className="mt-6 px-6 py-3 rounded-full"
              style={{ backgroundColor: colors.accent }}
            >
              <Text className="font-medium" style={{ color: colors.bg }}>Browse Recipes</Text>
            </TouchableOpacity>
          )}
        </View>
      ),
    [historyLoading, isFetching, isRefreshing, searchQuery, colors]
  );

  return (
    <SafeAreaView
      className="flex-1"
      style={{ backgroundColor: colors.bg }}
    >
      {/* Title Bar */}
      <View className="px-4 py-4 flex-row items-center justify-between">
        <View className="flex-row items-center">
          <TouchableOpacity onPress={() => router.back()} className="mr-3">
            <Icon name="arrow-left" size={24} color={colors.textPrimary} />
          </TouchableOpacity>
          <Text className="text-xl font-bold" style={{ color: colors.textPrimary }}>Cooking History</Text>
        </View>

        {historyItems.length > 0 && (
          <TouchableOpacity
            onPress={handleClearAll}
            disabled={clearHistory.isPending}
            className="flex-row items-center px-3 py-2 rounded-lg border"
            style={{ backgroundColor: colors.card, borderColor: colors.border }}
          >
            {clearHistory.isPending ? (
              <ActivityIndicator size="small" color={colors.textPrimary} />
            ) : (
              <>
                <Icon name="trash" size={16} color={colors.textPrimary} />
                <Text className="text-sm font-medium ml-1" style={{ color: colors.textPrimary }}>
                  Clear All
                </Text>
              </>
            )}
          </TouchableOpacity>
        )}
      </View>

      {/* Search Bar */}
      <View className="px-4 pb-3">
        <SearchBar
          placeholder="Search recipes..."
          value={searchQuery}
          onChangeText={setSearchQuery}
        />
      </View>

      {/* Loading State */}
      {historyLoading && <LoadingView />}

      {/* Error State */}
      {historyError && (
        <ErrorView errorPage="Cooking History" refetch={refetchHistory} />
      )}

      {/* History List */}
      {!historyLoading && (
        <FlatList
          data={historyItems}
          keyExtractor={(item) => item.cookId}
          renderItem={renderItem}
          ListEmptyComponent={renderEmpty}
          contentContainerStyle={{
            paddingTop: 12,
            paddingBottom: 24,
            flexGrow: 1,
          }}
          showsVerticalScrollIndicator={false}
          onEndReached={() => {
            if (historyLoading || isFetching || !hasMore || historyItems.length === 0) return;
            setPage((prev) => prev + 1);
          }}
          onEndReachedThreshold={0.4}
          refreshControl={
            <RefreshControl
              refreshing={isRefreshing}
              onRefresh={handleRefresh}
              tintColor={colors.accent}
              colors={[colors.accent]}
              progressBackgroundColor={colors.card}
            />
          }
        />
      )}

      {/* Delete Entry Dialog */}
      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete History</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete this recipe?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose
              asChild
              className="flex-1 py-3 rounded-xl border items-center"
              style={{ borderColor: colors.border }}
            >
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
            </DialogClose>
            <Pressable
              onPress={confirmDeleteEntry}
              className="flex-1 py-3 rounded-xl items-center"
              style={{ backgroundColor: colors.error }}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>Delete</Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Clear All Dialog */}
      <Dialog open={clearAllDialogOpen} onOpenChange={setClearAllDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Clear All History</DialogTitle>
            <DialogDescription>
              Are you sure you want to clear all cooking history?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose
              asChild
              className="flex-1 py-3 rounded-xl border items-center"
              style={{ borderColor: colors.border }}
            >
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
            </DialogClose>
            <Pressable
              className="flex-1 py-3 rounded-xl items-center"
              style={{ backgroundColor: colors.error }}
              onPress={confirmClearAll}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>Clear All</Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </SafeAreaView>
  );
}
