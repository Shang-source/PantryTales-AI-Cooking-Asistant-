import { useMemo, useState } from "react";
import {
  ActivityIndicator,
  Image,
  Keyboard,
  RefreshControl,
  ScrollView,
  Text,
  TouchableOpacity,
  TouchableWithoutFeedback,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { Sparkles } from "lucide-react-native";
import { useRouter } from "expo-router";

import MainHeader from "@/components/MainHeader";
import { SearchBar } from "@/components/SearchBar";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/avatar";
import { useAuthQuery } from "@/hooks/useApi";
import { useTheme } from "@/contexts/ThemeContext";
import type { ApiResponse } from "@/types/api";

type DetectedRecipe = {
  id: string;
  title: string;
  coverImageUrl?: string | null;
  authorNickname: string;
  authorAvatarUrl?: string | null;
  createdAt: string;
  tags: string[];
  likesCount?: number;
  commentsCount?: number;
  savedCount?: number;
};

type DetectedRecipesResponse = ApiResponse<DetectedRecipe[]>;

const CARD_HEIGHT = 320;
const CARD_MEDIA_HEIGHT = 160;

function RecipeCard({
  recipe,
  onPress,
  colors,
}: {
  recipe: DetectedRecipe;
  onPress: () => void;
  colors: any;
}) {
  const fallbackInitial =
    recipe.authorNickname?.trim()?.charAt(0)?.toUpperCase() ?? "?";
  const cover = recipe.coverImageUrl ?? null;
  const tags = recipe.tags ?? [];

  return (
    <TouchableOpacity
      activeOpacity={0.92}
      onPress={onPress}
      className="mb-4 overflow-hidden rounded-[26px] border"
      style={{ height: CARD_HEIGHT, backgroundColor: colors.card, borderColor: colors.border }}
    >
      <View
        style={{ height: CARD_MEDIA_HEIGHT, backgroundColor: colors.card }}
        className="w-full overflow-hidden"
      >
        {cover ? (
          <Image
            source={{ uri: cover }}
            className="h-full w-full"
            resizeMode="cover"
          />
        ) : (
          <View className="h-full w-full items-center justify-center">
            <Text className="text-[10px] font-semibold uppercase tracking-[3px]" style={{ color: colors.textMuted }}>
              NO COVER
            </Text>
          </View>
        )}
      </View>

      <View className="flex-1 px-3 py-3">
        <View className="flex-1 justify-between gap-2">
          <View className="gap-2">
            <Text
              className="text-[16px] font-semibold leading-tight"
              style={{ color: colors.textPrimary }}
              numberOfLines={2}
            >
              {recipe.title}
            </Text>
            {tags.length > 0 && (
              <View className="flex-row flex-wrap gap-2">
                {tags.slice(0, 3).map((tag) => (
                  <View
                    key={`${recipe.id}-${tag}`}
                    className="rounded-full border px-2.5 py-0.5"
                    style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  >
                    <Text className="text-[11px] font-medium" style={{ color: colors.textSecondary }}>
                      {tag}
                    </Text>
                  </View>
                ))}
              </View>
            )}
          </View>

          <View className="gap-1">
            <View className="flex-row items-center gap-2">
              <Avatar className="h-8 w-8 border" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
                {recipe.authorAvatarUrl ? (
                  <AvatarImage source={{ uri: recipe.authorAvatarUrl }} />
                ) : null}
                <AvatarFallback style={{ backgroundColor: colors.card }}>
                  <Text className="text-xs font-semibold" style={{ color: colors.textPrimary }}>
                    {fallbackInitial}
                  </Text>
                </AvatarFallback>
              </Avatar>
              <Text
                className="flex-1 text-[13px] font-semibold"
                style={{ color: colors.textPrimary }}
                numberOfLines={1}
              >
                {recipe.authorNickname || "AI Generated"}
              </Text>
            </View>
            <Text className="text-xs" style={{ color: colors.textMuted }}>
              {new Date(recipe.createdAt).toLocaleDateString()}
            </Text>
          </View>
        </View>
      </View>
    </TouchableOpacity>
  );
}

export default function AIDetectedRecipesScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { colors } = useTheme();
  const [searchValue, setSearchValue] = useState("");

  const { data, isLoading, isFetching, isError, error, refetch } =
    useAuthQuery<DetectedRecipesResponse>(
      ["ai-detected-recipes"],
      "/api/recipes?scope=ai-detected",
      { staleTime: 60_000 },
    );

  const normalizedSearch = searchValue.trim().toLowerCase();
  const recipes = useMemo(() => data?.data ?? [], [data]);

  const visibleRecipes = useMemo(() => {
    let ordered = [...recipes].sort(
      (a, b) =>
        new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    );

    if (!normalizedSearch) {
      return ordered;
    }

    return ordered.filter((recipe) => {
      const haystack =
        `${recipe.title} ${recipe.tags?.join(" ") ?? ""}`.toLowerCase();
      return haystack.includes(normalizedSearch);
    });
  }, [normalizedSearch, recipes]);

  const columns = useMemo(() => {
    const left: DetectedRecipe[] = [];
    const right: DetectedRecipe[] = [];

    visibleRecipes.forEach((recipe, index) => {
      if (index % 2 === 0) {
        left.push(recipe);
      } else {
        right.push(recipe);
      }
    });

    return [left, right];
  }, [visibleRecipes]);

  const bottomPadding = Math.max(insets.bottom, 18);
  const isRefreshing = isFetching && !isLoading;

  const navigateToRecipe = (recipe: DetectedRecipe) => {
    router.push({
      pathname: "/(tabs)/recipe/[recipeId]" as const,
      params: { recipeId: recipe.id },
    });
  };

  const renderEmptyState = () => (
    <View className="flex-1 items-center justify-center px-6 py-16">
      <View className="mb-6 h-20 w-20 items-center justify-center rounded-full" style={{ backgroundColor: `${colors.accent}30` }}>
        <Sparkles size={40} color={colors.accent} />
      </View>
      <Text className="text-center text-lg font-semibold" style={{ color: colors.textPrimary }}>
        No Detected Recipes Yet
      </Text>
      <Text className="mt-2 text-center text-sm" style={{ color: colors.textSecondary }}>
        Take a photo of a dish to identify and save the recipe
      </Text>
    </View>
  );

  const renderStatus = () => {
    if (isLoading) {
      return (
        <View className="flex-1 items-center justify-center px-6">
          <ActivityIndicator size="small" color={colors.accent} />
          <Text className="mt-3 text-sm" style={{ color: colors.textSecondary }}>
            Loading detected recipes...
          </Text>
        </View>
      );
    }

    if (isError) {
      return (
        <View className="flex-1 items-center justify-center px-6">
          <Text className="text-center text-base font-semibold" style={{ color: colors.textPrimary }}>
            Something went wrong
          </Text>
          <Text className="mt-2 text-center text-sm" style={{ color: colors.textSecondary }}>
            {error?.message ?? "Unable to load detected recipes."}
          </Text>
          <TouchableOpacity
            onPress={() => refetch()}
            className="mt-4 rounded-full px-5 py-2"
            style={{ backgroundColor: colors.card }}
            activeOpacity={0.9}
          >
            <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Retry</Text>
          </TouchableOpacity>
        </View>
      );
    }

    if (visibleRecipes.length === 0) {
      return renderEmptyState();
    }

    return (
      <ScrollView
        className="flex-1"
        showsVerticalScrollIndicator={false}
        refreshControl={
          <RefreshControl
            tintColor={colors.accent}
            refreshing={isRefreshing}
            onRefresh={refetch}
          />
        }
        contentContainerStyle={{ paddingBottom: bottomPadding + 16 }}
      >
        <View className="flex-row gap-4 px-4 pt-5">
          {columns.map((column, columnIndex) => (
            <View key={`column-${columnIndex}`} className="flex-1">
              {column.map((recipe) => (
                <RecipeCard
                  key={recipe.id}
                  recipe={recipe}
                  onPress={() => navigateToRecipe(recipe)}
                  colors={colors}
                />
              ))}
            </View>
          ))}
        </View>
      </ScrollView>
    );
  };

  return (
    <>
      <TouchableWithoutFeedback onPress={Keyboard.dismiss} accessible={false}>
        <View
          className="flex-1"
          style={{ paddingTop: insets.top + 8, paddingBottom: bottomPadding, backgroundColor: colors.bg }}
        >
          <MainHeader />
          <View className="flex-1">
            <View className="px-4 pt-4">
              <View className="flex-row items-center gap-2 mb-4">
                <Sparkles size={22} color={colors.accent} />
                <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>
                  AI Detected Recipes
                </Text>
              </View>

              <SearchBar
                placeholder="Search detected recipes"
                value={searchValue}
                onChangeText={setSearchValue}
                autoCorrect={false}
                returnKeyType="search"
              />
            </View>

            {renderStatus()}
          </View>
        </View>
      </TouchableWithoutFeedback>
    </>
  );
}
