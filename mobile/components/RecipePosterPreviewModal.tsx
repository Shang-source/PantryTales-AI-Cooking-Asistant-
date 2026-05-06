import React, { useRef, useState, useCallback } from "react";
import {
  View,
  Text,
  Modal,
  TouchableOpacity,
  ScrollView,
  ActivityIndicator,
  Dimensions,
} from "react-native";
import { X } from "lucide-react-native";
import { RecipePoster, RecipePosterData } from "./RecipePoster";
import { saveRecipePosterToAlbum } from "@/utils/saveRecipePoster";
import { toast, Toaster } from "@/components/sonner";
import { useTheme } from "@/contexts/ThemeContext";

const { width: SCREEN_WIDTH } = Dimensions.get("window");
const POSTER_WIDTH = 350;
const POSTER_SCALE = Math.min(1, (SCREEN_WIDTH - 40) / POSTER_WIDTH);

interface RecipePosterPreviewModalProps {
  visible: boolean;
  recipe: RecipePosterData | null;
  onClose: () => void;
}

/**
 * RecipePosterPreviewModal - A modal component for previewing and saving recipe posters.
 * Shows a preview of the poster and provides Save/Cancel buttons.
 */
export function RecipePosterPreviewModal({
  visible,
  recipe,
  onClose,
}: RecipePosterPreviewModalProps) {
  const posterRef = useRef<View>(null);
  const [isSaving, setIsSaving] = useState(false);
  const { colors } = useTheme();

  const handleSave = useCallback(async () => {
    if (!posterRef.current) {
      toast.error("Unable to capture poster. Please try again.");
      return;
    }

    setIsSaving(true);

    try {
      const result = await saveRecipePosterToAlbum(posterRef);

      if (result.success) {
        toast.success("Recipe poster saved to your photo album!");
        onClose();
      } else {
        toast.error(result.error || "Failed to save poster. Please try again.");
      }
    } catch {
      toast.error("An unexpected error occurred. Please try again.");
    } finally {
      setIsSaving(false);
    }
  }, [onClose]);

  if (!recipe) return null;

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
      statusBarTranslucent
    >
      <View className="flex-1 bg-black/70 justify-center items-center">
        {/* Header */}
        <View className="w-full flex-row justify-between items-center px-6 py-4 absolute top-14 z-10">
          {/* Invisible placeholder to balance the close button */}
          <View className="w-10" />
          <Text className="text-lg font-semibold" style={{ color: colors.overlayText }}>
            Poster Preview
          </Text>
          <TouchableOpacity
            onPress={onClose}
            disabled={isSaving}
            className="p-2"
          >
            <X size={24} color={colors.overlayText} />
          </TouchableOpacity>
        </View>

        {/* Poster Preview */}
        <ScrollView
          contentContainerStyle={{
            flexGrow: 1,
            justifyContent: "center",
            alignItems: "center",
            paddingVertical: 100,
          }}
          showsVerticalScrollIndicator={false}
        >
          <View
            style={{
              width: POSTER_WIDTH * POSTER_SCALE,
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            <View
              style={{
                width: POSTER_WIDTH,
                transform: [{ scale: POSTER_SCALE }],
                transformOrigin: "center",
                shadowColor: "#000",
                shadowOffset: { width: 0, height: 4 },
                shadowOpacity: 0.3,
                shadowRadius: 8,
                elevation: 8,
              }}
            >
              <RecipePoster ref={posterRef} recipe={recipe} />
            </View>
          </View>
        </ScrollView>

        {/* Bottom Action Buttons */}
        <View className="w-full px-6 pb-12 pt-5 bg-black/50 flex-row gap-4">
          {/* Cancel Button */}
          <TouchableOpacity
            onPress={onClose}
            disabled={isSaving}
            className="flex-1 py-4 rounded-xl items-center border"
            style={{ backgroundColor: "rgba(255,255,255,0.15)", borderColor: "rgba(255,255,255,0.4)" }}
          >
            <Text className="font-semibold text-base" style={{ color: colors.overlayText }}>Cancel</Text>
          </TouchableOpacity>

          {/* Save Button */}
          <TouchableOpacity
            onPress={handleSave}
            disabled={isSaving}
            className="flex-1 py-4 rounded-xl items-center flex-row justify-center gap-2"
            style={{ backgroundColor: colors.accent }}
          >
            {isSaving ? (
              <>
                <ActivityIndicator size="small" color={colors.bg} />
                <Text className="font-semibold text-base" style={{ color: colors.bg }}>
                  Saving...
                </Text>
              </>
            ) : (
              <Text className="font-semibold text-base" style={{ color: colors.bg }}>
                Save to Album
              </Text>
            )}
          </TouchableOpacity>
        </View>

        {/* Toast container inside modal for proper z-index */}
        <Toaster />
      </View>
    </Modal>
  );
}
