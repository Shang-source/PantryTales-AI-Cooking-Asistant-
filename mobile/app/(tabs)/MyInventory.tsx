import {
  View,
  Text,
  Pressable,
  FlatList,
  ActivityIndicator,
  Platform,
  RefreshControl,
  ScrollView,
} from "react-native";
import { useTheme } from "@/contexts/ThemeContext";
import { SafeAreaView } from "react-native-safe-area-context";
import { useState, useCallback, useEffect } from "react";
import { cn } from "@/utils/cn";
import { Package, Bot, X, ChefHat, BookOpen } from "lucide-react-native";
import { useFocusEffect, useRouter } from "expo-router";
import {
  InventoryItemForm,
  InventorySortBy,
  StorageMethodString,
  STORAGE_FILTERS,
} from "@/types/Inventory";
import { useInventoryList } from "@/hooks/useInventoryList";
import StatCard from "@/components/ui/StatCard";
import { IconBadge } from "@/components/IconBadge";
import { Button } from "@/components/Button";
import { SearchBar } from "@/components/SearchBar";
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
} from "@/components/dropdown-menu";
import { InventoryItemCard } from "@/components/inventory/InventoryCard";
import MainHeader from "@/components/MainHeader";
import { InventoryItemDialog } from "@/components/inventory/InventoryItemDialog";
import { toast } from "@/components/sonner";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/dialog";
import ErrorView from "@/components/ui/ErrorView";
import LoadingView from "@/components/ui/LoadingView";

type StorageFilter = "all" | StorageMethodString;
const sortOptions: {
  value: InventorySortBy;
  label: string;
}[] = [
  { value: "Expiring", label: "Expiring Soon" },
  { value: "DateAdded", label: "Date Added" },
  { value: "Name", label: "Name" },
];

export default function InventoryScreen() {
  const router = useRouter();
  const { colors } = useTheme();
  const floatingPosition = Platform.select({
    ios: { bottom: 32, right: 12 },
    android: { bottom: 16, right: 16 },
    default: { bottom: 16, right: 16 },
  });
  const [filterRowWidth, setFilterRowWidth] = useState(0);
  const [filterRowContentWidth, setFilterRowContentWidth] = useState(0);
  const [keyword, setKeyword] = useState("");
  const [debouncedKeyword, setDebouncedKeyword] = useState("");
  const [sortBy, setSortBy] = useState<InventorySortBy>("Expiring");
  const [storageFilter, setStorageFilter] = useState<StorageFilter>("all");
  const {
    items: flattenedItems,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isLoading,
    error,
    createInventoryItem,
    updateInventoryItem,
    deleteInventoryItem,
    statsQuery,
    refreshInventory,
  } = useInventoryList({ sortBy, storageFilter, keyword: debouncedKeyword });
  const isFilterRowScrollable = filterRowContentWidth > filterRowWidth;
  const totalItems = statsQuery.data?.data?.totalCount ?? 0;
  const expiringSoonCount = statsQuery.data?.data?.expiringSoonCount ?? 0;
  const storageMethodCount = statsQuery.data?.data?.storageMethodCount ?? 0;

  useEffect(() => {
    let isActive = true;
    const timeoutId = setTimeout(() => {
      if (isActive) {
        setDebouncedKeyword(keyword);
      }
    }, 300);
    return () => {
      isActive = false;
      clearTimeout(timeoutId);
    };
  }, [keyword]);

  useFocusEffect(
    useCallback(() => {
      refreshInventory();
    }, [refreshInventory]),
  );

  // Background polling to sync with other family members
  useEffect(() => {
    const intervalId = setInterval(() => {
      refreshInventory();
    }, 5000); // Poll every 5 seconds

    return () => clearInterval(intervalId);
  }, [refreshInventory]);

  const [isPullRefreshing, setIsPullRefreshing] = useState(false);
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const [addingItem, setAddingItem] = useState<InventoryItemForm | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteItemId, setDeleteItemId] = useState<string | null>(null);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<InventoryItemForm | null>(
    null,
  );
  const [botExpanded, setBotExpanded] = useState(false);

  const handleDelete = (id: string) => {
    setDeleteItemId(id);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (deleteItemId) {
      try {
        await deleteInventoryItem.mutateAsync({ id: deleteItemId });
        setDeleteItemId(null);
      } catch (err) {
        toast.error("Failed to delete inventory item.");
      }
    }
  };

  const handleEdit = (item: InventoryItemForm) => {
    setEditingItem(item);
    setIsEditDialogOpen(true);
  };

  const onAddManual = async (item: InventoryItemForm) => {
    try {
      await createInventoryItem.mutateAsync(item);
    } catch (err) {
      toast.error("Failed to create inventory item. Please try again.");
    }
  };

  const onEditManual = async (item: InventoryItemForm) => {
    try {
      await updateInventoryItem.mutateAsync(item);
    } catch (err) {
      toast.error("Failed to update inventory item. Please try again.");
    }
  };

  const handleBotNavigate =
    (path: "/smart-recipes" | "/KnowledgeBase") => () => {
      setBotExpanded(false);
      router.push(path);
    };

  return (
    <SafeAreaView
      className="flex-1"
      style={{ backgroundColor: colors.bg }}
      edges={["right", "left", "top"]}
    >
      <MainHeader />

      {error ? (
        <View className="flex-1 items-center justify-center px-4">
          <ErrorView errorPage="inventory" refetch={refreshInventory} />
        </View>
      ) : (
        <FlatList
          className="px-4"
          data={flattenedItems}
          keyExtractor={(item) => item.id}
          refreshControl={
            <RefreshControl
              refreshing={isPullRefreshing}
              onRefresh={async () => {
                setIsPullRefreshing(true);
                try {
                  await refreshInventory();
                } finally {
                  setIsPullRefreshing(false);
                }
              }}
              tintColor={colors.accent}
              colors={[colors.accent]}
              progressBackgroundColor={colors.card}
            />
          }
          renderItem={({ item }) => (
            <InventoryItemCard
              inventoryItem={item}
              onEdit={() => handleEdit(item)}
              onDelete={() => handleDelete(item.id)}
            />
          )}
          onEndReachedThreshold={0.4}
          showsVerticalScrollIndicator={false}
          ListEmptyComponent={
            isLoading && !isPullRefreshing ? (
              <LoadingView />
            ) : (
              <View className="flex-1 items-center justify-center mt-20">
                <Package size={80} color={colors.textMuted} />

                <Text className="text-lg font-semibold mt-4" style={{ color: colors.textPrimary }}>
                  No ingredients in inventory
                </Text>

                <Text className="mt-2 text-center" style={{ color: colors.textSecondary }}>
                  Start scanning ingredients to build your inventory
                </Text>
              </View>
            )
          }
          ListHeaderComponent={
            <View>
              <View className="flex-row justify-between items-center mb-4 mt-4">
                <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>
                  My Inventory
                </Text>
                <Pressable
                  className="w-28 border px-3 py-2 rounded-xl"
                  style={{ backgroundColor: colors.accent, borderColor: colors.border }}
                  onPress={() => setIsAddDialogOpen(true)}
                >
                  <Text style={{ color: colors.bg }}>+ Add Item</Text>
                </Pressable>
              </View>
              {/* Search */}
              <SearchBar
                placeholder="Search ingredients..."
                value={keyword}
                onChangeText={setKeyword}
              />

              {/* Stats */}
              <View className="justify-center flex-row gap-5 mt-2">
                <StatCard
                  value={totalItems}
                  className="w-32"
                  label="Total Items"
                />
                <StatCard
                  value={expiringSoonCount}
                  label="Expiring Soon"
                  className="w-32"
                />
                <StatCard
                  value={storageMethodCount}
                  className="w-32"
                  label="Locations"
                />
              </View>

              {/* Filters */}
              <ScrollView
                horizontal
                onLayout={(event) => {
                  setFilterRowWidth(event.nativeEvent.layout.width);
                }}
                onContentSizeChange={(width) => {
                  setFilterRowContentWidth(width);
                }}
                scrollEnabled={isFilterRowScrollable}
                showsHorizontalScrollIndicator={false}
                className="mt-2"
              >
                <View className="flex-row gap-1 pr-4">
                  <Button
                    variant="secondary"
                    size="sm"
                    className="h-8 rounded-full px-3"
                    style={storageFilter === "all" ? { backgroundColor: colors.accent } : undefined}
                    textStyle={storageFilter === "all" ? { color: colors.bg } : undefined}
                    onPress={() => {
                      setStorageFilter("all");
                    }}
                    textClassName="text-sm"
                  >
                    All
                  </Button>
                  {STORAGE_FILTERS.map((f) => (
                    <Button
                      variant="secondary"
                      key={f.value}
                      className="h-8 rounded-full px-3"
                      style={storageFilter === f.value ? { backgroundColor: colors.accent } : undefined}
                      textStyle={storageFilter === f.value ? { color: colors.bg } : undefined}
                      onPress={() => {
                        setStorageFilter(f.value);
                      }}
                    >
                      <IconBadge
                        iconSet="MaterialCommunityIcons"
                        iconName={f.icon}
                        iconSize={15}
                        iconColor={storageFilter === f.value ? colors.bg : colors.textPrimary}
                        textClassName="text-sm"
                        style={{ color: storageFilter === f.value ? colors.bg : colors.textPrimary }}
                      >
                        {f.label}
                      </IconBadge>
                    </Button>
                  ))}
                </View>
              </ScrollView>

              {/* Sort */}
              <View className="mt-3 flex-row items-center gap-2 border-b pb-2" style={{ borderColor: colors.border }}>
                <Text className="text-sm" style={{ color: colors.textMuted }}>Sort by:</Text>
                <View className="relative">
                  <DropdownMenu>
                    <DropdownMenuTrigger className="w-36 border rounded-lg px-3 py-2 flex-row justify-between items-center" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
                      <Text className="text-sm mr-2" style={{ color: colors.textPrimary }}>
                        {sortOptions.find((o) => o.value === sortBy)?.label}
                      </Text>
                      <Text style={{ color: colors.textPrimary }}>{"\u25BE"}</Text>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent
                      style={{ backgroundColor: colors.bg, borderColor: colors.border }}
                      className="absolute top-full border rounded-sm w-36"
                      sideOffset={Platform.OS === "android" ? 40 : 0}
                    >
                      {sortOptions.map((opt) => {
                        const isSelected = opt.value === sortBy;
                        return (
                          <DropdownMenuItem
                            key={opt.value}
                            onPress={() => {
                              setSortBy(opt.value);
                            }}
                            className="px-3 py-2 rounded-sm"
                            style={isSelected ? { backgroundColor: colors.card } : undefined}
                          >
                            <Text className="text-base" style={{ color: colors.textPrimary }}>
                              {opt.label}
                            </Text>
                          </DropdownMenuItem>
                        );
                      })}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </View>
              </View>
            </View>
          }
          onEndReached={() => {
            if (hasNextPage && !isFetchingNextPage) {
              fetchNextPage();
            }
          }}
        />
      )}

      {/* Add Item Dialog */}
      <InventoryItemDialog
        open={isAddDialogOpen}
        item={addingItem}
        onOpenChange={(open) => {
          setIsAddDialogOpen(open);
          if (!open) setAddingItem(null);
        }}
        onAdd={onAddManual}
        onUpdate={() => {}}
        isPending={createInventoryItem.isPending}
      />

      {/* Edit Item Dialog */}
      <InventoryItemDialog
        open={isEditDialogOpen}
        item={editingItem}
        onOpenChange={(open) => {
          setIsEditDialogOpen(open);
          if (!open) setEditingItem(null);
        }}
        onAdd={() => {}}
        onUpdate={onEditManual}
        isPending={updateInventoryItem.isPending}
      />

      {/* Delete Confirmation Dialog */}
      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Item</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete this item?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose className="flex-1 py-3 rounded-xl border items-center" style={{ borderColor: colors.border }}>
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
            </DialogClose>
            <Pressable
              onPress={() => {
                confirmDelete();
                setDeleteDialogOpen(false);
              }}
              disabled={deleteInventoryItem.isPending}
              className={`flex-1 py-3 rounded-xl items-center ${deleteInventoryItem.isPending ? "opacity-50" : ""}`}
              style={{ backgroundColor: colors.error }}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>
                {deleteInventoryItem.isPending ? "Deleting..." : "Delete"}
              </Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <View className="absolute flex-col items-end" style={floatingPosition}>
        {botExpanded && (
          <View className="flex-col items-end gap-2 mb-2">
            <Pressable
              onPress={handleBotNavigate("/smart-recipes")}
              className="rounded-full px-4 py-2 flex-row items-center gap-2 border"
              style={{ backgroundColor: colors.bg, borderColor: colors.border }}
            >
              <View className="rounded-full p-1" style={{ backgroundColor: colors.accent }}>
                <ChefHat color={colors.bg} size={16} />
              </View>
              <Text className="text-sm font-medium" style={{ color: colors.textPrimary }}>
                Smart Recipes
              </Text>
            </Pressable>
            <Pressable
              onPress={handleBotNavigate("/KnowledgeBase")}
              className="rounded-full px-4 py-2 flex-row items-center gap-2 border"
              style={{ backgroundColor: colors.bg, borderColor: colors.border }}
            >
              <View className="rounded-full p-1" style={{ backgroundColor: colors.accent }}>
                <BookOpen color={colors.bg} size={16} />
              </View>
              <Text className="text-sm font-medium" style={{ color: colors.textPrimary }}>
                Cooking Tips
              </Text>
            </Pressable>
          </View>
        )}
        <Pressable
          onPress={() => setBotExpanded((prev) => !prev)}
          className="rounded-full shadow-lg border p-3 items-center justify-center self-end"
          style={{ backgroundColor: colors.accent, borderColor: colors.border }}
        >
          {botExpanded ? (
            <X color={colors.bg} size={22} />
          ) : (
            <Bot color={colors.bg} size={22} />
          )}
        </Pressable>
      </View>
    </SafeAreaView>
  );
}
