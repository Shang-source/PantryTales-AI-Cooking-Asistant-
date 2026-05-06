import { useCallback, useRef, useState } from "react";
import {
  ActivityIndicator,
  BackHandler,
  ScrollView,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { useLocalSearchParams, useRouter, useFocusEffect } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { useQueryClient } from "@tanstack/react-query";

import {
  RecipeEditForm,
  type RecipeEditFormRef,
} from "@/components/RecipeEditForm";
import { useTheme } from "@/contexts/ThemeContext";

export default function RecipeEditScreen() {
  const { recipeId, source, tab } = useLocalSearchParams<{
    recipeId?: string;
    source?: string;
    tab?: string;
  }>();
  const router = useRouter();
  const { colors } = useTheme();
  const queryClient = useQueryClient();
  const formRef = useRef<RecipeEditFormRef>(null);
  const [editStatus, setEditStatus] = useState({
    isSaving: false,
    isDeleting: false,
    isReady: false,
  });

  const handleBack = useCallback(() => {
    if (source === "me-posts") {
      router.replace({ pathname: "/me", params: { tab: "Posts" } });
      return;
    }
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace("/me");
    }
  }, [router, source]);

  const handleSaved = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ["my-recipes"] });
    queryClient.invalidateQueries({ queryKey: ["recipe", recipeId] });
    queryClient.invalidateQueries({
      queryKey: ["community-recipes", "scope:community"],
    });
    handleBack();
  }, [queryClient, recipeId, handleBack]);

  const handleDeleted = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ["my-recipes"] });
    queryClient.invalidateQueries({
      queryKey: ["community-recipes", "scope:community"],
    });
    if (source === "me-posts") {
      router.replace({ pathname: "/me", params: { tab: "Posts" } });
    } else {
      router.replace("/me");
    }
  }, [queryClient, router, source]);

  useFocusEffect(
    useCallback(() => {
      const onBackPress = () => {
        handleBack();
        return true;
      };
      const sub = BackHandler.addEventListener(
        "hardwareBackPress",
        onBackPress,
      );
      return () => sub.remove();
    }, [handleBack]),
  );

  const handleSave = () => {
    formRef.current?.save();
  };

  const handleDelete = () => {
    formRef.current?.delete();
  };

  if (!recipeId) {
    return (
      <SafeAreaView
        edges={["top", "left", "right"]}
        className="flex-1"
        style={{ backgroundColor: colors.bg }}
      >
        <View className="flex-1 items-center justify-center px-4">
          <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
            Invalid recipe
          </Text>
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView
      edges={["top", "left", "right"]}
      className="flex-1"
      style={{ backgroundColor: colors.bg }}
    >
      {/* Header */}
      <View
        className="flex-row items-center justify-between px-4 py-3"
        style={{ borderBottomWidth: 1, borderBottomColor: colors.border }}
      >
        <View className="flex-row items-center gap-3">
          <TouchableOpacity
            onPress={handleBack}
            activeOpacity={0.7}
            className="px-1"
          >
            <Ionicons name="chevron-back" size={28} color={colors.accent} />
          </TouchableOpacity>
          <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>
            Edit Recipe
          </Text>
        </View>

        <View className="flex-row items-center gap-2">
          <TouchableOpacity
            onPress={handleDelete}
            disabled={editStatus.isDeleting || !editStatus.isReady}
            activeOpacity={0.8}
            className="h-10 w-10 items-center justify-center rounded-full"
            style={{ backgroundColor: `${colors.error}20` }}
          >
            {editStatus.isDeleting ? (
              <ActivityIndicator size="small" color={colors.error} />
            ) : (
              <Ionicons name="trash-outline" size={20} color={colors.error} />
            )}
          </TouchableOpacity>

          <TouchableOpacity
            onPress={handleSave}
            disabled={editStatus.isSaving || !editStatus.isReady}
            activeOpacity={0.8}
            className="h-10 px-4 items-center justify-center rounded-full"
            style={{ backgroundColor: colors.accent }}
          >
            {editStatus.isSaving ? (
              <ActivityIndicator size="small" color={colors.bg} />
            ) : (
              <Text className="text-sm font-semibold" style={{ color: colors.bg }}>
                Save
              </Text>
            )}
          </TouchableOpacity>
        </View>
      </View>

      {/* Form */}
      <View className="flex-1">
        <RecipeEditForm
          ref={formRef}
          recipeId={recipeId}
          onSaved={handleSaved}
          onDeleted={handleDeleted}
          hideActions
          onStatusChange={setEditStatus}
        />
      </View>
    </SafeAreaView>
  );
}
