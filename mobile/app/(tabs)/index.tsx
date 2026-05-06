import {
  Text,
  View,
  TouchableOpacity,
  ScrollView,
  RefreshControl,
  ActivityIndicator,
  Image,
  Platform,
} from "react-native";
import MasonryList from "@react-native-seoul/masonry-list";
import { useEffect, useState, useCallback, useMemo, memo } from "react";
import { SafeAreaView } from "react-native-safe-area-context";
import { useRouter } from "expo-router";
import type { Router } from "expo-router";
import { useQueryClient } from "@tanstack/react-query";
import Icon from "react-native-vector-icons/Feather";

import MainHeader from "@/components/MainHeader";
import { Button } from "@/components/Button";
import { Card, CardContent, CardHeader, CardFooter } from "@/components/card";
import StatCard from "@/components/ui/StatCard";
import { IconBadge } from "@/components/IconBadge";
import { useAuthMutation, useAuthQuery } from "@/hooks/useApi";
import { ApiResponse } from "@/types/api";
import { useInfiniteRecommendedRecipes } from "@/hooks/useRecommendedRecipes";
import { useRefreshState } from "@/hooks/useRefreshState";
import { useCookingTips, type CookingTip } from "@/hooks/useCookingTips";
import { CookingTipsTicker } from "@/components/CookingTipsTicker";
import { useFeaturedRecipes } from "@/hooks/useFeaturedRecipes";
import { FeaturedRecipesCarousel } from "@/components/FeaturedRecipesCarousel";
import { createRecommendationSeed } from "@/utils/recommendations";
import type { RecommendedRecipeDto } from "@/hooks/useRecommendedRecipes";
import type { RecipeSaveResponse } from "@/types/recipes";
import {
  applyRecipeSaveToggleResult,
  upsertSavedRecipeId,
  useSavedRecipeIds,
} from "@/hooks/useRecipeSaves";
import { useTheme } from "@/contexts/ThemeContext";
import type { InventoryStatsResponseDto } from "@/types/Inventory";
import { Search } from "lucide-react-native";
import { cn } from "@/utils/cn";

const normalizeDifficulty = (difficulty?: string | null) => {
  if (difficulty == null) return null;
  const raw = `${difficulty}`.trim();
  if (!raw) return null;
  const lowered = raw.toLowerCase();
  if (lowered === "none" || lowered === "0") return null;
  if (lowered === "1") return "Easy";
  if (lowered === "2") return "Medium";
  if (lowered === "3") return "Hard";
  if (lowered === "easy") return "Easy";
  if (lowered === "medium") return "Medium";
  if (lowered === "hard") return "Hard";
  return raw;
};

const CardIconStat = memo(({
  iconName,
  value,
  textColor,
}: {
  iconName: string;
  value: string | number;
  textColor: string;
}) => (
  <View className="flex-row items-center mr-2">
    <Icon name={iconName} size={13} color={textColor} />
    <Text className="text-xs ml-1" style={{ color: textColor }}>{value}</Text>
  </View>
));

const CommunityBadge = memo(({
  avatarUrl,
  nickname,
  overlayTextColor = "#ffffff",
}: {
  avatarUrl?: string | null;
  nickname?: string | null;
  overlayTextColor?: string;
}) => (
  <View
    className="absolute top-2 left-2 flex-row items-center bg-black/60 rounded-full px-1.5 py-0.5"
    accessibilityLabel={nickname ? `By ${nickname}` : "Community recipe"}
  >
    {avatarUrl ? (
      <Image
        source={{ uri: avatarUrl }}
        className="w-4 h-4 rounded-full"
        accessibilityLabel={nickname ?? "Author"}
      />
    ) : (
      <Icon name="users" size={12} color={overlayTextColor} />
    )}
    <Text className="text-[10px] ml-1 font-medium" style={{ color: overlayTextColor }}>Community</Text>
  </View>
));

type HomeShortcutIconSet = "Ionicons" | "MaterialCommunityIcons";
type HomeShortcutRoute = Extract<Parameters<Router["push"]>[0], string>;

type HomeShortcutAction = {
  label: string;
  route: HomeShortcutRoute;
  iconSet: HomeShortcutIconSet;
  iconName: string;
};

const HOME_SHORTCUT_ACTIONS: HomeShortcutAction[] = [
  {
    label: "Smart Recipes",
    route: "/smart-recipes",
    iconSet: "MaterialCommunityIcons" as const,
    iconName: "chef-hat",
  },
  {
    label: "Family",
    route: "/familymember",
    iconSet: "Ionicons" as const,
    iconName: "people-outline",
  },
  {
    label: "Cooking Tips",
    route: "/KnowledgeBase",
    iconSet: "Ionicons" as const,
    iconName: "book-outline",
  },
  {
    label: "Cooking History",
    route: "/cooking-history",
    iconSet: "MaterialCommunityIcons" as const,
    iconName: "book-variant",
  },
];

// Recipe card component - memoized for performance
// Uses recipeId + handlers instead of inline functions to preserve memo
const RecipeCard = memo(({
  recipe,
  onPressRecipe,
  textColor = "#ffffff",
  mutedTextColor = "rgba(255,255,255,0.6)",
  overlayTextColor = "#ffffff",
}: {
  recipe: RecommendedRecipeDto;
  onPressRecipe: (recipeId: string) => void;
  textColor?: string;
  mutedTextColor?: string;
  overlayTextColor?: string;
}) => {
  const hasImage = Boolean(recipe.coverImageUrl);
  const hasServings = recipe.servings != null && recipe.servings > 0;
  const hasTime = recipe.totalTimeMinutes != null && recipe.totalTimeMinutes > 0;
  const difficultyLabel = normalizeDifficulty(recipe.difficulty);
  const hasDifficulty = Boolean(difficultyLabel);
  const showStats = hasServings || hasTime || hasDifficulty;
  const isCommunityRecipe = recipe.type === "User";

  const handlePress = useCallback(() => {
    onPressRecipe(recipe.recipeId);
  }, [onPressRecipe, recipe.recipeId]);

  return (
    <Card onPress={handlePress} className="mx-0 mb-0 overflow-hidden p-0">
      <CardHeader className="w-full h-32 overflow-hidden rounded-t-xl">
        {hasImage ? (
          <Image
            className="w-full h-full"
            source={{ uri: recipe.coverImageUrl as string }}
            accessibilityLabel={recipe.title}
          />
        ) : (
          <View className="w-full h-full items-center justify-center bg-black/5">
            <Icon name="image" size={22} color="rgba(0, 0, 0, 0.5)" />
            <Text className="text-black/50 text-xs mt-1">No image</Text>
          </View>
        )}
        {isCommunityRecipe && (
          <CommunityBadge
            avatarUrl={recipe.authorAvatarUrl}
            nickname={recipe.authorNickname}
            overlayTextColor={overlayTextColor}
          />
        )}
      </CardHeader>
      <CardContent className="px-3 pt-2 pb-1">
        <Text className="font-semibold text-base" style={{ color: textColor }}>
          {recipe.title}
        </Text>
      </CardContent>
      <CardFooter className="px-3 pb-3">
        {showStats && (
          <View className="flex-row justify-start">
            {hasTime && (
              <CardIconStat iconName="clock" value={`${recipe.totalTimeMinutes}m`} textColor={mutedTextColor} />
            )}
            {hasDifficulty && (
              <CardIconStat iconName="star" value={difficultyLabel!} textColor={mutedTextColor} />
            )}
            {hasServings && (
              <CardIconStat iconName="users" value={recipe.servings!} textColor={mutedTextColor} />
            )}
          </View>
        )}
      </CardFooter>
    </Card>
  );
});

export default function Index() {
  const router = useRouter();
  const { colors } = useTheme();
  const isAndroid = Platform.OS === "android";
  const [recommendationSeed, setRecommendationSeed] = useState(
    createRecommendationSeed,
  );
  const [displayedRecommendedRecipes, setDisplayedRecommendedRecipes] =
    useState<RecommendedRecipeDto[]>([]);
  const [actionRowWidth, setActionRowWidth] = useState(0);
  const [actionRowContentWidth, setActionRowContentWidth] = useState(0);
  const {
    data: Statsdata,
    refetch: refetchStats,
    isFetching: isFetchingStats,
  } = useAuthQuery<ApiResponse<InventoryStatsResponseDto>>(
    ["inventory-stats"],
    "/api/inventory/stats",
  );

  // Fetch recommended recipes with infinite scroll
  const {
    recipes: recommendedRecipes,
    isLoading: isLoadingRecommended,
    isFetching: isFetchingRecommended,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useInfiniteRecommendedRecipes(10, undefined, recommendationSeed);

  const { isRefreshing, refresh: handleRefresh } = useRefreshState({
    isFetching: isFetchingStats || isFetchingRecommended,
    onRefresh: () => {
      setRecommendationSeed(createRecommendationSeed());
      refetchStats();
    },
  });
  const { data: savedIds = [] } = useSavedRecipeIds();
  const queryClient = useQueryClient();
  const [savingRecipeId, setSavingRecipeId] = useState<string | null>(null);
  const saveMutation = useAuthMutation<ApiResponse<RecipeSaveResponse>, string>(
    (recipeId) => `/api/recipes/${recipeId}/saves/toggle`,
    "POST",
  );

  // Fetch featured cooking tips for the ticker
  const { tips: cookingTips, isLoading: isLoadingTips } = useCookingTips(10);

  // Fetch featured community recipes for the carousel
  const { recipes: featuredRecipes, isLoading: isLoadingFeatured } = useFeaturedRecipes(10);

  // Handle tip press - navigate to the article
  const handleTipPress = useCallback(
    (tip: CookingTip) => {
      router.push({
        pathname: "/KnowledgeBase",
        params: { articleId: tip.id, tagId: tip.tagId.toString() },
      });
    },
    [router]
  );

  const isActionRowScrollable = actionRowContentWidth > actionRowWidth;

  useEffect(() => {
    recommendedRecipes.forEach((recipe) => {
      if (typeof recipe.savedByMe === "boolean") {
        upsertSavedRecipeId(queryClient, recipe.recipeId, recipe.savedByMe);
      }
    });
  }, [queryClient, recommendedRecipes]);

  useEffect(() => {
    if (!isRefreshing) {
      setDisplayedRecommendedRecipes(recommendedRecipes);
    }
  }, [isRefreshing, recommendedRecipes]);

  const toggleSave = useCallback((recipeId: string) => {
    if (saveMutation.isPending) return;
    const previousIsSaved = savedIds.includes(recipeId);
    const optimisticSaved = !previousIsSaved;
    setSavingRecipeId(recipeId);
    upsertSavedRecipeId(queryClient, recipeId, optimisticSaved);

    saveMutation.mutate(recipeId, {
      onSuccess: (response) => {
        const payload = response?.data;
        if (!payload) {
          upsertSavedRecipeId(queryClient, recipeId, previousIsSaved);
          return;
        }
        applyRecipeSaveToggleResult(queryClient, payload, null, {
          previousIsSaved,
        });
      },
      onError: () => {
        upsertSavedRecipeId(queryClient, recipeId, previousIsSaved);
      },
      onSettled: () => {
        setSavingRecipeId((current) => (current === recipeId ? null : current));
      },
    });
  }, [saveMutation, savedIds, queryClient]);

  // Handle end reached for infinite loading
  const handleEndReached = useCallback(() => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const handleRecipePress = useCallback((recipeId: string) => {
    router.push({
      pathname: "/recipe/[recipeId]",
      params: { recipeId, source: "home" },
    });
  }, [router]);

  // Convert to Set for O(1) lookup instead of O(n) array.includes()
  const savedIdsSet = useMemo(() => new Set(savedIds), [savedIds]);

  const renderRecipeItem = useCallback(
    ({ item }: { item: unknown; i: number }) => {
      const recipe = item as RecommendedRecipeDto;
      return (
        <View style={{ paddingHorizontal: 4, marginBottom: 8 }}>
          <RecipeCard
            recipe={recipe}
            onPressRecipe={handleRecipePress}
            textColor={colors.textPrimary}
            mutedTextColor={colors.textMuted}
            overlayTextColor={colors.overlayText}
          />
        </View>
      );
    },
    [handleRecipePress, colors.textPrimary, colors.textMuted, colors.overlayText]
  );

  const ListHeader = useMemo(
    () => (
      <>
        {/* Button Content */}
        <View className="border-b mt-3 mb-3 mx-1 pb-3" style={{ borderColor: colors.border }}>
          <ScrollView
            horizontal
            onLayout={(event) => {
              setActionRowWidth(event.nativeEvent.layout.width);
            }}
            onContentSizeChange={(width) => {
              setActionRowContentWidth(width);
            }}
            scrollEnabled={isActionRowScrollable}
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={{
              flexGrow: 1,
              justifyContent: isActionRowScrollable ? "flex-start" : "center",
            }}
            className="mt-2"
          >
            <View className="flex-row items-center gap-2 h-10">
              {HOME_SHORTCUT_ACTIONS.map((action) => (
                <Button
                  key={action.route}
                  variant="shortcut"
                  className="rounded-3xl px-3"
                  onPress={() => router.push(action.route)}
                >
                  <IconBadge
                    iconSet={action.iconSet}
                    iconName={action.iconName}
                    iconSize={16}
                    textClassName="text-sm"
                  >
                    {action.label}
                  </IconBadge>
                </Button>
              ))}
            </View>
          </ScrollView>
        </View>

        {/* Cooking Tips Ticker */}
        <View className="mx-1">
          <CookingTipsTicker
            tips={cookingTips}
            onTipPress={handleTipPress}
            isLoading={isLoadingTips}
          />
        </View>

        {/* Featured Recipes + Inventory Row */}
        <View className="flex-row mx-1 gap-2 mb-3">
          {/* Left: Featured Carousel */}
          <View className="flex-1">
            <View className="flex-row items-center mb-2">
              <IconBadge
                iconSet="Ionicons"
                iconName="star"
                iconColor={colors.accent}
                iconSize={16}
              >
                <Text className="font-semibold text-sm" style={{ color: colors.textPrimary }}>Featured</Text>
              </IconBadge>
            </View>
            <FeaturedRecipesCarousel
              recipes={featuredRecipes}
              isLoading={isLoadingFeatured}
            />
          </View>

          {/* Right: Inventory (compact) */}
          <View className="flex-1">
            <View className="flex-row items-center mb-2">
              <IconBadge
                iconSet="Ionicons"
                iconName="cube-outline"
                iconColor={colors.accent}
                iconSize={16}
              >
                <Text className="font-semibold text-sm" style={{ color: colors.textPrimary }}>Inventory</Text>
              </IconBadge>
            </View>
            <TouchableOpacity
              activeOpacity={0.8}
              onPress={() => router.push("/MyInventory")}
              style={{ height: 130, backgroundColor: colors.card, borderColor: colors.border }}
              className="rounded-xl p-3 border justify-center"
            >
              <View className="gap-3">
                <StatCard
                  value={Statsdata?.data?.totalCount ?? 0}
                  label="Total"
                  compact
                  icon="package"
                  iconColor={colors.accent}
                />
                <StatCard
                  value={Statsdata?.data?.expiringSoonCount ?? 0}
                  label="Expiring"
                  compact
                  icon="alert-triangle"
                  iconColor="#F59E0B"
                />
                <StatCard
                  value={Statsdata?.data?.storageMethodCount ?? 0}
                  label="Locations"
                  compact
                  icon="map-pin"
                  iconColor="#60A5FA"
                />
              </View>
            </TouchableOpacity>
          </View>
        </View>

        {/* Recommended Recipes Header */}
        <View className="flex-row items-center h-12 mx-1">
          <IconBadge
            iconSet="Ionicons"
            iconName="sparkles"
            iconColor={colors.accent}
            iconSize={20}
          >
            <Text className="font-semibold text-lg" style={{ color: colors.textPrimary }}>Recommendations</Text>
          </IconBadge>
          <TouchableOpacity
            className="ml-auto p-2"
            onPress={() => router.push("/recommended-recipes")}
          >
            <Search size={20} color={colors.accent} />
          </TouchableOpacity>
        </View>

        {/* Loading state for initial load */}
        {isLoadingRecommended && displayedRecommendedRecipes.length === 0 && (
          <View className="items-center py-8">
            <ActivityIndicator color={colors.accent} />
            <Text className="mt-2" style={{ color: colors.textMuted }}>
              Loading recommendations...
            </Text>
          </View>
        )}

        {/* Empty state */}
        {!isLoadingRecommended && displayedRecommendedRecipes.length === 0 && (
          <View className="items-center py-8 mx-1">
            <Text className="text-center" style={{ color: colors.textMuted }}>
              Set your preferences to get personalized recipe recommendations.
            </Text>
          </View>
        )}
      </>
    ),
    [
      isActionRowScrollable,
      cookingTips,
      isLoadingTips,
      handleTipPress,
      featuredRecipes,
      isLoadingFeatured,
      Statsdata,
      router,
      isLoadingRecommended,
      displayedRecommendedRecipes.length,
      colors.accent,
      colors.textPrimary,
      colors.textMuted,
      colors.card,
      colors.border,
    ]
  );

  const ListFooter = useMemo(
    () =>
      isFetchingNextPage ? (
        <View className="py-4 items-center">
          <ActivityIndicator color={colors.accent} />
          <Text className="mt-2 text-sm" style={{ color: colors.textMuted }}>Loading more...</Text>
        </View>
      ) : (
        <View className="h-6" />
      ),
    [isFetchingNextPage, colors.accent, colors.textMuted]
  );

  return (
    <SafeAreaView
      style={{ flex: 1, backgroundColor: colors.bg }}
      edges={["right", "left", "top"]}
    >
      <MainHeader />
<MasonryList
        data={displayedRecommendedRecipes}
        keyExtractor={(item: RecommendedRecipeDto) => item.recipeId}
        numColumns={2}
        renderItem={renderRecipeItem}
        extraData={savedIds}
        ListHeaderComponent={ListHeader}
        ListFooterComponent={ListFooter}
        onEndReached={handleEndReached}
        onEndReachedThreshold={0.3}
        showsVerticalScrollIndicator={false}
        contentContainerStyle={{ paddingHorizontal: 8 }}
        refreshing={isRefreshing}
        onRefresh={handleRefresh}
        {...({
          removeClippedSubviews: isAndroid,
          initialNumToRender: isAndroid ? 6 : 10,
          maxToRenderPerBatch: isAndroid ? 6 : 10,
          updateCellsBatchingPeriod: isAndroid ? 50 : 100,
          windowSize: isAndroid ? 7 : 12,
          scrollEventThrottle: 16,
        } as object)}
      />
    </SafeAreaView>
  );
}
