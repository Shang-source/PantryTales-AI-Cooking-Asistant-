import { Modal, Pressable, Text, TouchableOpacity, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { Package, Sparkles, X } from "lucide-react-native";
import { useTheme } from "@/contexts/ThemeContext";

interface RecognitionModeSheetProps {
  visible: boolean;
  onClose: () => void;
  onIngredientScan: () => void;
  onRecipeScan: () => void;
}

export function RecognitionModeSheet({
  visible,
  onClose,
  onIngredientScan,
  onRecipeScan,
}: RecognitionModeSheetProps) {
  const insets = useSafeAreaInsets();
  const { colors } = useTheme();

  const handleIngredientScan = () => {
    onIngredientScan();
    onClose();
  };

  const handleRecipeScan = () => {
    onRecipeScan();
    onClose();
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="none"
      onRequestClose={onClose}
    >
      <Pressable className="flex-1 justify-end bg-black/50" onPress={onClose}>
        <Pressable
          onPress={(e) => e.stopPropagation()}
          className="rounded-t-[28px]"
          style={{ backgroundColor: colors.bg, paddingBottom: insets.bottom + 16 }}
        >
          {/* Header */}
          <View className="flex-row items-center justify-between px-5 py-4">
            <View>
              <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>
                Choose Recognition Mode
              </Text>
              <Text className="text-sm mt-1" style={{ color: colors.textSecondary }}>
                Please select what you want to recognize
              </Text>
            </View>
            <TouchableOpacity
              onPress={onClose}
              className="h-8 w-8 items-center justify-center"
              activeOpacity={0.7}
            >
              <X size={22} color={colors.textSecondary} />
            </TouchableOpacity>
          </View>

          {/* Options */}
          <View className="gap-3 px-5 pt-2">
            {/* Ingredient Scan */}
            <TouchableOpacity
              onPress={handleIngredientScan}
              className="flex-row items-center gap-4 rounded-2xl border p-4"
              style={{ backgroundColor: colors.card, borderColor: colors.border }}
              activeOpacity={0.9}
            >
              <View
                className="h-14 w-14 items-center justify-center rounded-xl"
                style={{ backgroundColor: `${colors.accent}20` }}
              >
                <Package size={28} color={colors.accent} />
              </View>
              <View className="flex-1">
                <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                  Ingredient Scan
                </Text>
                <Text className="text-sm leading-5 mt-1" style={{ color: colors.textSecondary }}>
                  Take photos of fridge or ingredients{"\n"}
                  Automatically recognize common ingredients
                </Text>
              </View>
            </TouchableOpacity>

            {/* Recipe Scan */}
            <TouchableOpacity
              onPress={handleRecipeScan}
              className="flex-row items-center gap-4 rounded-2xl border p-4"
              style={{ backgroundColor: colors.card, borderColor: colors.border }}
              activeOpacity={0.9}
            >
              <View
                className="h-14 w-14 items-center justify-center rounded-xl"
                style={{ backgroundColor: `${colors.accent}20` }}
              >
                <Sparkles size={28} color={colors.accent} />
              </View>
              <View className="flex-1">
                <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                  Recipe Scan
                </Text>
                <Text className="text-sm leading-5 mt-1" style={{ color: colors.textSecondary }}>
                  Take photos of dishes or recipes{"\n"}
                  Intelligently recognize recipe steps
                </Text>
              </View>
            </TouchableOpacity>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
