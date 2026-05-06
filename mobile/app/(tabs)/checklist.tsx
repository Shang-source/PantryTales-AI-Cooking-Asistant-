import { useState, useMemo, useEffect } from "react";
import { useTheme } from "@/contexts/ThemeContext";
import {
  View,
  Text,
  TouchableOpacity,
  Pressable,
  ActivityIndicator,
  SectionList,
  RefreshControl,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { ChevronDown, Plus, ShoppingCart } from "lucide-react-native";
import { MaterialCommunityIcons } from "@expo/vector-icons";
import {
  useChecklist,
  useUpdateChecklistItem,
  useDeleteChecklistItem,
  useMoveCheckedToInventory,
  useClearAllItems,
  ChecklistItemDto,
  ChecklistSection,
  useChecklistStats,
  useCategoryColors,
} from "@/hooks/useChecklist";
import { ChecklistItemDialog } from "@/components/checklist/ChecklistItemDialog";
import { ChecklistCard } from "@/components/checklist/ChecklistCard";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogClose,
} from "@/components/dialog";
import MainHeader from "@/components/MainHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/card";
import StatCard from "@/components/ui/StatCard";
import { IconBadge } from "@/components/IconBadge";
import { toast } from "@/components/sonner";

export default function ChecklistScreen() {
  const { colors } = useTheme();
  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<ChecklistItemDto | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteItemId, setDeleteItemId] = useState<string | null>(null);
  const [moveDialogOpen, setMoveDialogOpen] = useState(false);
  const [clearDialogOpen, setClearDialogOpen] = useState(false);
  const [noPurchasedDialogOpen, setNoPurchasedDialogOpen] = useState(false);
  const [isPullRefreshing, setIsPullRefreshing] = useState(false);
  const [collapsedSections, setCollapsedSections] = useState<
    Record<string, boolean>
  >({});
  const { isLoading, error, refetch, sections } = useChecklist();
  const updateItem = useUpdateChecklistItem();
  const deleteItem = useDeleteChecklistItem();
  const moveToInventory = useMoveCheckedToInventory();
  const clearAll = useClearAllItems();
  const statsQuery = useChecklistStats();
  const { categoryColors } = useCategoryColors();
  const totalCount = statsQuery.data?.data?.totalCount ?? 0;
  const purchasedCount = statsQuery.data?.data?.purchasedCount ?? 0;
  const remainingCount = statsQuery.data?.data?.remainingCount ?? 0;
  const handleRefresh = async () => {
    setIsPullRefreshing(true);
    try {
      await Promise.all([refetch(), statsQuery.refetch()]);
    } finally {
      setIsPullRefreshing(false);
    }
  };

  // Background polling to sync with other family members
  useEffect(() => {
    const intervalId = setInterval(() => {
      refetch();
      statsQuery.refetch();
    }, 5000); // Poll every 5 seconds

    return () => clearInterval(intervalId);
  }, [refetch, statsQuery]);

  const visibleSections = useMemo(
    () =>
      sections.map((section) => ({
        ...section,
        data: collapsedSections[section.title] ? [] : section.data,
      })),
    [sections, collapsedSections],
  );

  const handleToggleCheck = (item: ChecklistItemDto) => {
    updateItem.mutate({
      id: item.id,
      data: { isChecked: !item.isChecked },
    });
  };

  const handleDelete = (id: string) => {
    setDeleteItemId(id);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (deleteItemId) {
      try {
        await deleteItem.mutateAsync(deleteItemId);
        setDeleteItemId(null);
      } catch (err) {
        toast.error("Failed to delete checklist item.");
      }
    }
  };

  const handleEdit = (item: ChecklistItemDto) => {
    setEditingItem(item);
    setIsEditDialogOpen(true);
  };

  const handleMoveToInventory = () => {
    if (purchasedCount === 0) {
      setNoPurchasedDialogOpen(true);
      return;
    }
    setMoveDialogOpen(true);
  };

  const confirmMoveToInventory = async () => {
    try {
      await moveToInventory.mutateAsync();
      return true;
    } catch (err) {
      toast.error("Failed to move items to inventory.");
      return false;
    }
  };

  const handleClearAll = () => {
    if (totalCount === 0) {
      toast.info("No items to clear.");
      return;
    }
    setClearDialogOpen(true);
  };

  const confirmClearAll = async () => {
    try {
      await clearAll.mutateAsync();
      return true;
    } catch (err) {
      toast.error("Failed to clear items.");
      return false;
    }
  };

  const toggleSection = (title: string) => {
    setCollapsedSections((prev) => ({
      ...prev,
      [title]: !prev[title],
    }));
  };

  const renderSectionHeader = ({ section }: { section: ChecklistSection }) => {
    const isCollapsed = collapsedSections[section.title];
    const categoryColor = categoryColors[section.title];
    return (
      <View
        className={`px-4 pt-4 mx-4 ${
          isCollapsed ? "rounded-2xl mb-4 pb-4" : "rounded-t-2xl pb-3"
        }`}
        style={{
          backgroundColor: categoryColor ? `${categoryColor}20` : colors.card,
          borderTopWidth: categoryColor ? 1 : 0,
          borderLeftWidth: categoryColor ? 1 : 0,
          borderRightWidth: categoryColor ? 1 : 0,
          borderBottomWidth: isCollapsed && categoryColor ? 1 : 0,
          borderColor: categoryColor ? `${categoryColor}40` : undefined,
        }}
      >
        <Pressable
          onPress={() => toggleSection(section.title)}
          className="flex-row items-center justify-between"
        >
          <Text className="font-semibold text-base" style={{ color: categoryColor || colors.textPrimary }}>
            {section.title}
          </Text>
          <View
            style={{
              transform: [{ rotate: isCollapsed ? "-90deg" : "0deg" }],
            }}
          >
            <ChevronDown size={18} color={categoryColor || colors.textPrimary} />
          </View>
        </Pressable>
      </View>
    );
  };

  const renderItem = ({
    item,
    index,
    section,
  }: {
    item: ChecklistItemDto;
    index: number;
    section: ChecklistSection;
  }) => {
    const isLast = index === section.data.length - 1;
    const categoryColor = categoryColors[section.title];
    return (
      <View
        className={`mx-4 px-2 ${isLast ? "rounded-b-2xl mb-4" : ""}`}
        style={{
          backgroundColor: categoryColor ? `${categoryColor}20` : colors.card,
          borderLeftWidth: categoryColor ? 1 : 0,
          borderRightWidth: categoryColor ? 1 : 0,
          borderBottomWidth: isLast && categoryColor ? 1 : 0,
          borderColor: categoryColor ? `${categoryColor}40` : undefined,
        }}
      >
        <ChecklistCard
          item={item}
          onToggle={handleToggleCheck}
          onEdit={handleEdit}
          onDelete={handleDelete}
        />
      </View>
    );
  };

  return (
    <SafeAreaView
      className="flex-1"
      style={{ backgroundColor: colors.bg }}
      edges={["right", "left", "top"]}
    >
      <MainHeader />

      {/* Content */}
      {error ? (
        <View className="flex-1 items-center justify-center px-4">
          <Text className="text-center mb-4" style={{ color: colors.textSecondary }}>
            Failed to load checklist
          </Text>
          <TouchableOpacity
            onPress={() => refetch()}
            className="px-6 py-2 rounded-lg"
            style={{ backgroundColor: colors.accent }}
          >
            <Text className="font-medium" style={{ color: colors.bg }}>Retry</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <SectionList
          sections={visibleSections}
          keyExtractor={(item) => item.id}
          renderSectionHeader={renderSectionHeader}
          renderItem={renderItem}
          showsVerticalScrollIndicator={false}
          stickySectionHeadersEnabled={false}
          refreshControl={
            <RefreshControl
              refreshing={isPullRefreshing}
              onRefresh={handleRefresh}
              tintColor={colors.accent}
              colors={[colors.accent]}
              progressBackgroundColor={colors.card}
            />
          }
          ListEmptyComponent={
            isLoading && !isPullRefreshing ? (
              <View className="flex-1 items-center justify-center py-20 mt-20">
                <ActivityIndicator size="large" color={colors.accent} />
              </View>
            ) : (
              <View className="flex-1 items-center justify-center px-4 mt-20">
                <ShoppingCart size={80} color={colors.textMuted} />
                <Text className="text-lg font-semibold mt-4" style={{ color: colors.textPrimary }}>
                  No items in your checklist
                </Text>
                <Text className="mt-2 text-center" style={{ color: colors.textSecondary }}>
                  Find the recipes you like and add ingredients you need to buy
                </Text>
              </View>
            )
          }
          ListHeaderComponent={
            <View>
              {/* Stats Card*/}
              <Card className="mx-4 mt-4" style={{ backgroundColor: colors.card, borderColor: colors.border, borderWidth: 1 }}>
                <CardHeader style={{ backgroundColor: 'transparent' }}>
                  <IconBadge
                    iconSet="Ionicons"
                    iconName="cart-outline"
                    iconColor={colors.accent}
                    iconSize={20}
                  >
                    <CardTitle>Checklist</CardTitle>
                  </IconBadge>
                </CardHeader>
                <CardContent className="flex-row gap-4">
                  <View className="flex-1 items-center">
                    <StatCard
                      value={totalCount}
                      label="Total"
                    />
                  </View>
                  <View className="flex-1 items-center">
                    <StatCard
                      valueStyle={{ color: colors.success }}
                      value={purchasedCount}
                      label="Purchased"
                    />
                  </View>
                  <View className="flex-1 items-center">
                    <StatCard value={remainingCount} label="Remaining" />
                  </View>
                </CardContent>
              </Card>

              {/* Action Buttons - Outside Container */}
              <View className="flex-row gap-2 mx-4 mb-4">
                <TouchableOpacity
                  onPress={() => setIsAddDialogOpen(true)}
                  className="flex-1 flex-row items-center justify-center py-2.5 rounded-xl"
                  style={{ backgroundColor: colors.accent }}
                >
                  <Plus size={16} color={colors.bg} />
                  <Text className="font-medium ml-1 text-sm" style={{ color: colors.bg }}>Add</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  onPress={handleMoveToInventory}
                  className="flex-1 flex-row items-center justify-center py-2.5 rounded-xl border"
                  style={{ backgroundColor: colors.card, borderColor: colors.success }}
                >
                  <MaterialCommunityIcons name="package-variant" size={16} color={colors.success} />
                  <Text className="font-medium text-sm ml-1" style={{ color: colors.success }}>
                    To Inventory
                  </Text>
                </TouchableOpacity>

                <TouchableOpacity
                  onPress={handleClearAll}
                  className="flex-row items-center justify-center py-2.5 px-4 ml-1 rounded-xl border"
                  style={{ backgroundColor: colors.card, borderColor: colors.error }}
                >
                  <MaterialCommunityIcons name="broom" size={16} color={colors.error} />
                </TouchableOpacity>
              </View>
            </View>
          }
        />
      )}

      {/* Add Item Dialog */}
      <ChecklistItemDialog
        open={isAddDialogOpen}
        onOpenChange={setIsAddDialogOpen}
      />

      {/* Edit Item Dialog */}
      <ChecklistItemDialog
        open={isEditDialogOpen}
        item={editingItem}
        onOpenChange={(open) => {
          setIsEditDialogOpen(open);
          if (!open) setEditingItem(null);
        }}
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
              disabled={deleteItem.isPending}
              className={`flex-1 py-3 rounded-xl items-center ${deleteItem.isPending ? "opacity-50" : ""}`}
              style={{ backgroundColor: colors.error }}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>
                {deleteItem.isPending ? "Deleting..." : "Delete"}
              </Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add to Inventory Confirmation Dialog */}
      <Dialog open={moveDialogOpen} onOpenChange={setMoveDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add to Inventory</DialogTitle>
            <DialogDescription>
              Move {purchasedCount} purchased item
              {purchasedCount > 1 ? "s" : ""} to your inventory?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose className="flex-1 py-3 rounded-xl border items-center" style={{ borderColor: colors.border }}>
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
            </DialogClose>
            <Pressable
              onPress={() => {
                confirmMoveToInventory();
                setMoveDialogOpen(false);
              }}
              disabled={moveToInventory.isPending}
              className={`flex-1 py-3 rounded-xl items-center ${moveToInventory.isPending ? "opacity-50" : ""}`}
              style={{ backgroundColor: colors.success }}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>
                {moveToInventory.isPending ? "Adding..." : "Add"}
              </Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Clear All Items Confirmation Dialog */}
      <Dialog open={clearDialogOpen} onOpenChange={setClearDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Clear All Items</DialogTitle>
            <DialogDescription>
              Remove all {totalCount} item
              {totalCount > 1 ? "s" : ""} from your checklist?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose className="flex-1 py-3 rounded-xl border items-center" style={{ borderColor: colors.border }}>
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
            </DialogClose>
            <Pressable
              onPress={() => {
                confirmClearAll();
                setClearDialogOpen(false);
              }}
              disabled={clearAll.isPending}
              className={`flex-1 py-3 rounded-xl items-center ${clearAll.isPending ? "opacity-50" : ""}`}
              style={{ backgroundColor: colors.error }}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>
                {clearAll.isPending ? "Clearing..." : "Clear All"}
              </Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* No Purchased Items Dialog */}
      <Dialog
        open={noPurchasedDialogOpen}
        onOpenChange={setNoPurchasedDialogOpen}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>No Purchased Items</DialogTitle>
            <DialogDescription>
              There are no purchased items to add to inventory.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose className="flex-1 py-3 rounded-xl items-center" style={{ backgroundColor: colors.success }}>
              <Text className="font-medium" style={{ color: colors.overlayText }}>OK</Text>
            </DialogClose>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </SafeAreaView>
  );
}
