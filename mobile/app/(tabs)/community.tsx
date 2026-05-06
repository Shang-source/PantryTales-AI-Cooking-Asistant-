import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ActivityIndicator,
  FlatList,
  Image,
  RefreshControl,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { SafeAreaView, useSafeAreaInsets } from "react-native-safe-area-context";
import { Heart } from "lucide-react-native";
import { useRouter } from "expo-router";
import { useQueryClient } from "@tanstack/react-query";
import { LinearGradient } from "@/lib/nativewind";

import MainHeader from "@/components/MainHeader";
import { SearchBar } from "@/components/SearchBar";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/avatar";
import { useAuthQuery } from "@/hooks/useApi";
import type { RecipeCardDto } from "@/types/recipes";
import { upsertLikedRecipeId, useLikedRecipeIds } from "@/hooks/useRecipeLikes";
import { upsertSavedRecipeId, useSavedRecipeIds } from "@/hooks/useRecipeSaves";
import type { ApiResponse } from "@/types/api";
import { useTheme } from "@/contexts/ThemeContext";

type FeedTab = "latest" | "popular";

type CommunityRecipe = RecipeCardDto;
type CommunityResponse = ApiResponse<RecipeCardDto[]>;

const FEED_TABS: FeedTab[] = ["latest", "popular"];
const CARD_HEIGHT = 180;
const CARD_MEDIA_HEIGHT = 140;
const TITLE_OVERLAY_HEIGHT = Math.round(CARD_MEDIA_HEIGHT * 0.42);
const GRID_HORIZONTAL_GAP = 6;
const GRID_VERTICAL_GAP = 8;
const GRID_PADDING_HORIZONTAL = 10;

function RecipeCard({
  recipe,
  isLiked,
  isSaved,
  onPress,
}: {
  recipe: CommunityRecipe;
  isLiked: boolean;
  isSaved: boolean;
  onPress: () => void;
}) {
  const { colors } = useTheme();
  const fallbackInitial =
    recipe.authorNickname?.trim()?.charAt(0)?.toUpperCase() ?? "?";
  const cover = recipe.coverImageUrl ?? null;

  return (
    <TouchableOpacity
      activeOpacity={0.92}
      onPress={onPress}
      className="overflow-hidden rounded-xl border"
      style={{ height: CARD_HEIGHT, borderColor: colors.border, backgroundColor: colors.card }}
    >
      <View
        style={{ height: CARD_MEDIA_HEIGHT, backgroundColor: colors.bg }}
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

        <View className="absolute inset-x-0 bottom-0">
          <LinearGradient
            colors={["transparent", "rgba(0,0,0,0.72)"]}
            start={{ x: 0.5, y: 0 }}
            end={{ x: 0.5, y: 1 }}
            style={{
              height: TITLE_OVERLAY_HEIGHT,
              paddingHorizontal: 12,
              paddingBottom: 10,
              justifyContent: "flex-end",
            }}
          >
            <Text
              className="text-[16px] font-semibold leading-tight"
              style={{ color: colors.overlayText }}
              numberOfLines={1}
            >
              {recipe.title}
            </Text>
          </LinearGradient>
        </View>
      </View>

      <View className="px-3 py-2">
        <View className="flex-row items-center">
          <Avatar className="h-6 w-6 border" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
            {recipe.authorAvatarUrl ? (
              <AvatarImage source={{ uri: recipe.authorAvatarUrl }} />
            ) : null}
            <AvatarFallback style={{ backgroundColor: colors.card }}>
              <Text className="text-[9px] font-semibold" style={{ color: colors.textPrimary }}>
                {fallbackInitial}
              </Text>
            </AvatarFallback>
          </Avatar>
          <Text
            className="flex-1 text-[12px] ml-1.5"
            style={{ color: colors.textSecondary }}
            numberOfLines={1}
          >
            {recipe.authorNickname}
          </Text>
          <View className="flex-row items-center gap-1 ml-2">
            <Heart
              size={14}
              color={isLiked ? colors.accent : colors.textMuted}
              fill={isLiked ? colors.accent : "transparent"}
            />
            <Text className="text-[11px]" style={{ color: colors.textSecondary }}>
              {recipe.likesCount ?? 0}
            </Text>
          </View>
        </View>
      </View>
    </TouchableOpacity>
  );
}

export default function CommunityScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const { colors } = useTheme();
  const [activeTab, setActiveTab] = useState<FeedTab>("latest");
  const [searchValue, setSearchValue] = useState("");
  const { data: likedIds } = useLikedRecipeIds();
  const likedIdList = likedIds ?? [];
  const { data: savedIds } = useSavedRecipeIds();
  const savedIdList = savedIds ?? [];

  const { data, isLoading, isFetching, isError, error, refetch } =
    useAuthQuery<CommunityResponse>(
      ["community-recipes", "scope:community"],
      "/api/recipes?scope=community",
      { staleTime: 60_000 },
    );

  const normalizedSearch = searchValue.trim().toLowerCase();
  const recipes = useMemo(() => data?.data ?? [], [data]);

  useEffect(() => {
    recipes.forEach((recipe) => {
      const liked = recipe.likedByMe ?? false;
      upsertLikedRecipeId(queryClient, recipe.id, liked);
    });
  }, [queryClient, recipes]);

  useEffect(() => {
    recipes.forEach((recipe) => {
      if (typeof recipe.savedByMe !== "boolean") return;
      upsertSavedRecipeId(queryClient, recipe.id, recipe.savedByMe);
    });
  }, [queryClient, recipes]);

  const visibleRecipes = useMemo(() => {
    const onlyPublic = recipes.filter((recipe) => {
      if (!recipe.visibility) return true;
      return recipe.visibility.toLowerCase() === "public";
    });

    let ordered = [...onlyPublic];

    if (activeTab === "popular") {
      ordered.sort((a, b) => (b.likesCount ?? 0) - (a.likesCount ?? 0));
    } else {
      ordered.sort(
        (a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
      );
    }

    if (!normalizedSearch) {
      return ordered;
    }

    return ordered.filter((recipe) => {
      const haystack =
        `${recipe.title} ${recipe.tags?.join(" ") ?? ""}`.toLowerCase();
      return haystack.includes(normalizedSearch);
    });
  }, [activeTab, normalizedSearch, recipes]);

  const gridRecipes = useMemo<(CommunityRecipe | null)[]>(() => {
    if (visibleRecipes.length % 2 === 1) {
      return [...visibleRecipes, null];
    }
    return visibleRecipes;
  }, [visibleRecipes]);

  const bottomPadding = Math.max(insets.bottom, 18);
  const isRefreshing = isFetching && !isLoading;

  const navigateToRecipe = useCallback(
    (recipe: CommunityRecipe) => {
      router.push({
        pathname: "/recipe/[recipeId]" as const,
        params: { recipeId: recipe.id, source: "community" },
      });
    },
    [router],
  );

  const keyExtractor = useCallback(
    (item: CommunityRecipe | null, index: number) =>
      item?.id ?? `empty-${index}`,
    [],
  );

  const renderRecipeItem = useCallback(
    ({ item, index }: { item: CommunityRecipe | null; index: number }) => (
      <View
        style={{
          flex: 1,
          marginBottom: GRID_VERTICAL_GAP,
          marginRight: index % 2 === 0 ? GRID_HORIZONTAL_GAP : 0,
        }}
      >
        {!item ? (
          <View className="h-[248px]" />
        ) : (
          <RecipeCard
            recipe={item}
            isLiked={
              typeof item.likedByMe === "boolean"
                ? item.likedByMe
                : likedIdList.includes(item.id)
            }
            isSaved={
              typeof item.savedByMe === "boolean"
                ? item.savedByMe
                : savedIdList.includes(item.id)
            }
            onPress={() => navigateToRecipe(item)}
          />
        )}
      </View>
    ),
    [likedIdList, navigateToRecipe, savedIdList],
  );

  const renderStatus = () => {
    if (isLoading) {
      return (
        <View className="flex-1 items-center justify-center px-6">
          <ActivityIndicator size="small" color={colors.accent} />
          <Text className="mt-3 text-sm" style={{ color: colors.textSecondary }}>
            Loading community recipes...
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
            {error?.message ?? "Unable to reach the pantry community."}
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
      return (
        <View className="flex-1 items-center justify-center px-6">
          <Text className="text-center text-base font-semibold" style={{ color: colors.textPrimary }}>
            No public recipes yet
          </Text>
          <Text className="mt-2 text-center text-sm" style={{ color: colors.textSecondary }}>
            All community posts must be published as Public. Try switching tabs
            or clearing the search filter.
          </Text>
        </View>
      );
    }

    return (
        <FlatList
          className="flex-1"
          data={gridRecipes}
        numColumns={2}
        keyExtractor={keyExtractor}
        showsVerticalScrollIndicator={false}
        refreshControl={
          <RefreshControl
            tintColor={colors.accent}
            refreshing={isRefreshing}
            onRefresh={refetch}
          />
        }
        contentContainerStyle={{
          paddingHorizontal: GRID_PADDING_HORIZONTAL,
          paddingTop: GRID_VERTICAL_GAP,
          paddingBottom: bottomPadding + 16,
        }}
        renderItem={renderRecipeItem}
      />
    );
  };

  return (
    <SafeAreaView
      style={{ flex: 1, backgroundColor: colors.bg }}
      edges={["right", "left", "top"]}
    >
      <MainHeader />
      <View className="flex-1">
        <View className="px-4 pt-4">
          <SearchBar
            placeholder="Search public notes"
            value={searchValue}
            onChangeText={setSearchValue}
            autoCorrect={false}
            returnKeyType="search"
          />

          <View className="mt-4 flex-row rounded-full p-1" style={{ backgroundColor: colors.card }}>
            {FEED_TABS.map((tab) => {
              const isActive = activeTab === tab;
              return (
                <TouchableOpacity
                  key={tab}
                  activeOpacity={0.9}
                  onPress={() => setActiveTab(tab)}
                  className="flex-1 items-center rounded-full px-3 py-2"
                  style={isActive ? { backgroundColor: colors.accent } : undefined}
                >
                  <Text
                    className="text-sm font-semibold"
                    style={{ color: isActive ? colors.bg : colors.textSecondary }}
                  >
                    {tab === "latest" ? "Latest" : "Popular"}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
        </View>

        {renderStatus()}
      </View>
    </SafeAreaView>
  );
}
