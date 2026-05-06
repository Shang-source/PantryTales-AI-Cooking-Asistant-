import { View, Text, Pressable } from "react-native";
import { Pencil, Trash2 } from "lucide-react-native";
import { Card, CardContent } from "../card";
import { IconBadge } from "../IconBadge";

import { InventoryItemForm, STORAGE_FILTERS } from "@/types/Inventory";
import { useTheme } from "@/contexts/ThemeContext";

interface InventoryItemCardProps {
  inventoryItem: InventoryItemForm;
  onEdit: (updated: InventoryItemForm) => void;
  onDelete: (id: string) => void;
}

export function InventoryItemCard({
  inventoryItem,
  onEdit,
  onDelete,
}: InventoryItemCardProps) {
  const { colors } = useTheme();

  const daysLeft =
    inventoryItem.expiryDays === "" ? null : Number(inventoryItem.expiryDays);
  const isExpired = daysLeft !== null && daysLeft < 0;
  const expiryLabel =
    inventoryItem.expiryDays === ""
      ? "No expiry"
      : daysLeft !== null && daysLeft < 0
        ? `Expired ${Math.abs(daysLeft)} days ago`
        : `${daysLeft} days left`;

  return (
    <Card className={`m-0 mt-2 ${isExpired ? "border-2 border-red-500" : ""}`}>
      <CardContent>
        {/* Header */}
        <View className="flex-row items-center">
          <View className="flex-row items-center gap-2">
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              {inventoryItem.name}
            </Text>
            {isExpired && (
              <Text className="px-3 py-1 text-xs font-semibold rounded-md" style={{ backgroundColor: colors.error, color: colors.overlayText }}>
                Expired
              </Text>
            )}
          </View>

          <View className="flex-row items-center ml-auto">
            {/* Edit icon */}
            <Pressable
              onPress={() => onEdit(inventoryItem)}
              className="p-2"
              hitSlop={8}
            >
              <Pencil size={18} color={colors.accent} />
            </Pressable>

            {/* Delete icon */}
            <Pressable
              onPress={() => onDelete(inventoryItem.id)}
              className="p-2"
              hitSlop={8}
            >
              <Trash2 size={18} color={colors.error} />
            </Pressable>
          </View>
        </View>

        {/* Amount + Storage */}
        <View className="flex-row items-center mt-2">
          <Text className="text-sm" style={{ color: colors.textSecondary }}>
            {inventoryItem.amount} {inventoryItem.unit}
          </Text>

          <View className="flex-row items-center ml-4">
            <IconBadge
              iconName={
                STORAGE_FILTERS.find(
                  (filter) => filter.value === inventoryItem.storage
                )?.icon || "help-circle"
              }
              iconSet="MaterialCommunityIcons"
            >
              {inventoryItem.storage}
            </IconBadge>
          </View>
        </View>

        {/* Footer */}
        <View className="flex-row justify-between items-center mt-3">
          <Text style={{ color: colors.textMuted }}>
            Added {new Date(inventoryItem.addedDate).toLocaleDateString()}
          </Text>

          <Text style={{ color: isExpired ? colors.error : colors.textSecondary }}>
            {expiryLabel}
          </Text>
        </View>
      </CardContent>
    </Card>
  );
}
