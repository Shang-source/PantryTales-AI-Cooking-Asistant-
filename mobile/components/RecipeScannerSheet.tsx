import { Modal, Pressable, Text, TouchableOpacity, View } from "react-native";
import { Camera, Image as ImageIcon, Sparkles, X } from "lucide-react-native";
import * as ImagePicker from "expo-image-picker";
import { useTheme } from "@/contexts/ThemeContext";

interface RecipeScannerSheetProps {
  visible: boolean;
  onClose: () => void;
  onTakePhoto: (uri: string) => void;
  onSelectFromGallery: (uri: string) => void;
}

export function RecipeScannerSheet({
  visible,
  onClose,
  onTakePhoto,
  onSelectFromGallery,
}: RecipeScannerSheetProps) {
  const { colors } = useTheme();

  const handleSelectFromGallery = async () => {
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ["images"],
      allowsEditing: true,
      quality: 0.8,
    });

    if (!result.canceled && result.assets[0]) {
      onClose();
      // Wait for native animation to finish
      setTimeout(() => onSelectFromGallery(result.assets[0].uri), 600);
    }
  };

  const handleTakePhoto = async () => {
    const { status } = await ImagePicker.requestCameraPermissionsAsync();
    if (status !== "granted") {
      console.log("Camera permission denied");
      return;
    }

    const result = await ImagePicker.launchCameraAsync({
      allowsEditing: true,
      quality: 0.8,
    });

    if (!result.canceled && result.assets[0]) {
      onClose();
      // Wait for native animation to finish
      setTimeout(() => onTakePhoto(result.assets[0].uri), 600);
    }
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="none"
      onRequestClose={onClose}
    >
      <Pressable
        className="flex-1 items-center justify-center bg-black/50 px-6"
        onPress={onClose}
      >
        <Pressable
          onPress={(e) => e.stopPropagation()}
          className="w-full max-w-[400px] rounded-[28px]"
          style={{ backgroundColor: colors.bg }}
        >
          {/* Header */}
          <View className="flex-row items-center justify-between px-5 py-4">
            <View className="flex-row items-center gap-3">
              <View
                className="h-10 w-10 items-center justify-center rounded-full"
                style={{ backgroundColor: `${colors.accent}30` }}
              >
                <Sparkles size={20} color={colors.accent} />
              </View>
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                Recipe Scanner
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

          {/* Description */}
          <View className="px-5 pb-5">
            <Text className="text-[15px] leading-6" style={{ color: colors.textSecondary }}>
              Take a photo of a dish or recipe, and we will intelligently
              recognize and generate the recipe steps for you to recreate at
              home.
            </Text>
          </View>

          {/* Buttons */}
          <View className="gap-3 px-5 pb-6">
            <TouchableOpacity
              onPress={handleTakePhoto}
              className="flex-row items-center justify-center gap-2 rounded-xl py-4"
              style={{ backgroundColor: colors.accent }}
              activeOpacity={0.9}
            >
              <Camera size={20} color={colors.bg} />
              <Text className="text-[15px] font-semibold" style={{ color: colors.bg }}>
                Take Photo
              </Text>
            </TouchableOpacity>

            <TouchableOpacity
              onPress={handleSelectFromGallery}
              className="flex-row items-center justify-center gap-2 rounded-xl border py-4"
              style={{ backgroundColor: colors.card, borderColor: colors.border }}
              activeOpacity={0.9}
            >
              <ImageIcon size={20} color={colors.textPrimary} />
              <Text className="text-[15px] font-semibold" style={{ color: colors.textPrimary }}>
                Choose from Album
              </Text>
            </TouchableOpacity>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
