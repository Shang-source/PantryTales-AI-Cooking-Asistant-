import { useState } from "react";
import {
  Modal,
  ScrollView,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { Input } from "@/components/input";
import { useTheme } from "@/contexts/ThemeContext";

const COMMON_UNITS = [
  "cups", "cup", "tbsp", "tsp", "oz", "lb", "g", "kg", "ml", "L",
  "pieces", "pcs", "slices", "cloves", "bunch", "pinch", "dash", "to taste",
];

type UnitSelectorModalProps = {
  visible: boolean;
  currentUnit?: string;
  onSelect: (unit: string) => void;
  onClose: () => void;
};

export function UnitSelectorModal({
  visible,
  currentUnit,
  onSelect,
  onClose,
}: UnitSelectorModalProps) {
  const { colors } = useTheme();
  const [customUnitInput, setCustomUnitInput] = useState(currentUnit ?? "");

  const handleSelect = (unit: string) => {
    onSelect(unit);
    onClose();
  };

  const handleCustomSubmit = () => {
    if (customUnitInput.trim()) {
      onSelect(customUnitInput.trim());
      onClose();
      setCustomUnitInput("");
    }
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <TouchableOpacity
        className="flex-1 justify-end bg-black/50"
        activeOpacity={1}
        onPress={onClose}
      >
        <View
          className="rounded-t-3xl pb-8 pt-4"
          style={{ backgroundColor: colors.card }}
          onStartShouldSetResponder={() => true}
        >
          <View className="mb-4 items-center">
            <View className="h-1 w-10 rounded-full" style={{ backgroundColor: colors.border }} />
          </View>
          <Text className="mb-3 px-5 text-base font-semibold" style={{ color: colors.textPrimary }}>
            Select Unit
          </Text>
          <ScrollView className="max-h-64 px-5">
            <View className="flex-row flex-wrap gap-2">
              {COMMON_UNITS.map((unit) => (
                <TouchableOpacity
                  key={unit}
                  onPress={() => handleSelect(unit)}
                  className="rounded-lg border px-3 py-2"
                  style={{
                    borderColor: colors.border,
                    backgroundColor: currentUnit === unit ? colors.accent : colors.bg,
                  }}
                >
                  <Text
                    className="text-sm"
                    style={{
                      color: currentUnit === unit ? colors.bg : colors.textPrimary,
                    }}
                  >
                    {unit}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          </ScrollView>
          <View className="mt-4 px-5">
            <Text className="mb-2 text-sm" style={{ color: colors.textSecondary }}>
              Or enter custom unit:
            </Text>
            <View className="flex-row gap-2">
              <Input
                value={customUnitInput}
                onChangeText={setCustomUnitInput}
                placeholder="Custom unit"
                placeholderTextColor={colors.textMuted}
                className="flex-1 rounded-lg"
                style={{ color: colors.textPrimary, borderColor: colors.border, backgroundColor: colors.bg }}
              />
              <TouchableOpacity
                onPress={handleCustomSubmit}
                className="items-center justify-center rounded-lg px-4"
                style={{ backgroundColor: colors.accent }}
              >
                <Text className="text-sm font-semibold" style={{ color: colors.bg }}>Set</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </TouchableOpacity>
    </Modal>
  );
}
