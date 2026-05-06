import { View, Text, TouchableOpacity, Pressable } from "react-native";
import { Check, Pencil, Trash2 } from "lucide-react-native";
import { ChecklistItemDto } from "@/hooks/useChecklist";
import { useTheme } from "@/contexts/ThemeContext";

interface ChecklistItemRowProps {
  item: ChecklistItemDto;
  onToggle: (item: ChecklistItemDto) => void;
  onEdit: (item: ChecklistItemDto) => void;
  onDelete: (id: string) => void;
}

export function ChecklistCard({
  item,
  onToggle,
  onEdit,
  onDelete,
}: ChecklistItemRowProps) {
  const { colors } = useTheme();

  return (
    <TouchableOpacity
      onPress={() => onToggle(item)}
      className="flex-row items-center justify-between py-3 px-4 border-2 rounded-2xl mb-2"
      style={{ borderColor: colors.border, backgroundColor: colors.card }}
    >
      <View className="flex-row items-center flex-1">
        {/* Checkbox */}
        <View
          className="w-6 h-6 rounded-full border-2 items-center justify-center mr-3"
          style={
            item.isChecked
              ? { backgroundColor: colors.success, borderColor: colors.success }
              : { borderColor: colors.border, backgroundColor: "transparent" }
          }
        >
          {item.isChecked && <Check size={14} color={colors.overlayText} />}
        </View>

        {/* Item details - two lines */}
        <View className="flex-1">
          <Text
            className={`text-base font-medium ${item.isChecked ? "line-through" : ""}`}
            style={{ color: item.isChecked ? colors.textMuted : colors.textPrimary }}
          >
            {item.name}
          </Text>
          <Text className="text-sm" style={{ color: colors.textMuted }}>
            {item.amount} {item.unit}
          </Text>
        </View>
      </View>

      {/* Edit icon */}
      <Pressable
        testID="edit-button"
        onPress={(e) => {
          e?.stopPropagation?.();
          onEdit(item);
        }}
        className={`p-2 ${item.isChecked ? "opacity-40" : ""}`}
        hitSlop={8}
        disabled={item.isChecked}
      >
        <Pencil size={18} color={colors.accent} />
      </Pressable>

      {/* Delete icon */}
      <Pressable
        testID="delete-button"
        onPress={(e) => {
          e?.stopPropagation?.();
          onDelete(item.id);
        }}
        className="p-2"
        hitSlop={8}
      >
        <Trash2 size={18} color={colors.error} />
      </Pressable>
    </TouchableOpacity>
  );
}
