import { useEffect, useMemo, useState } from "react";
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  RefreshControl,
  ActivityIndicator,
  Image,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { useRouter } from "expo-router";
import Icon from "react-native-vector-icons/Feather";
import MaterialCommunityIcons from "react-native-vector-icons/MaterialCommunityIcons";
import Ionicons from "react-native-vector-icons/Ionicons";
import { SearchBar } from "@/components/SearchBar";
import { useRefreshState } from "@/hooks/useRefreshState";
import { createRecommendationSeed } from "@/utils/recommendations";
import {
  useInfiniteRecommendedRecipes,
  RecommendedRecipeDto,
} from "@/hooks/useRecommendedRecipes";
import { useTheme } from "@/contexts/ThemeContext";

const difficultyFilters = ["All", "Easy", "Medium", "Hard"] as const;
type DifficultyFilter = (typeof difficultyFilters)[number];

const cookingTimeFilters = ["All", "<=20 min", "20-45 min", ">45 min"] as const;

const CARD_HEIGHT = 220;
const CARD_IMAGE_HEIGHT = 120;
const GRID_VERTICAL_GAP = 8;
type CookingTimeFilter = (typeof cookingTimeFilters)[number];

const normalizeDifficulty = (value?: RecommendedRecipeDto["difficulty"]) => {
  if (!value) return "None";
  const raw = `${value}`.trim().toLowerCase();
  switch (raw) {
    case "easy":
    case "1":
      return "Easy";
    case "medium":
    case "2":
      return "Medium";
    case "hard":
    case "3":
      return "Hard";
    default:
      return value as string;
  }
};

export default function RecommendedRecipesPage() {
  const router = useRouter();
  const { colors } = useTheme();
  const [searchValue, setSearchValue] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [recommendationSeed, setRecommendationSeed] = useState(
    createRecommendationSeed,
  );
  const {
    recipes: recommendedRecipes,
    isLoading,
    isFetching,
    message,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useInfiniteRecommendedRecipes(20, debouncedSearch, recommendationSeed);
  const [displayedRecipes, setDisplayedRecipes] = useState<
    RecommendedRecipeDto[]
  >([]);
  const { isRefreshing, refresh: handleRefresh } = useRefreshState({
    isFetching: isFetching || isLoading,
    onRefresh: () => {
      setRecommendationSeed(createRecommendationSeed());
    },
  });
  const [activeDifficulty, setActiveDifficulty] =
    useState<DifficultyFilter>("All");
  const [activeCookingTime, setActiveCookingTime] =
    useState<CookingTimeFilter>("All");

  useEffect(() => {
    const handle = setTimeout(() => {
      setDebouncedSearch(searchValue);
    }, 350);
    return () => clearTimeout(handle);
  }, [searchValue]);

  useEffect(() => {
    if (!isRefreshing) {
      setDisplayedRecipes(recommendedRecipes);
    }
  }, [isRefreshing, recommendedRecipes]);

  const trimmedSearch = debouncedSearch.trim();
  const hasSearch = trimmedSearch.length > 0;

  const filteredRecipes = useMemo(() => {
    const search = trimmedSearch.toLowerCase();
    return displayedRecipes.filter((recipe) => {
      const matchesSearch =
        !search ||
        recipe.title.toLowerCase().includes(search) ||
        (recipe.tags ?? []).some((tag) => tag.toLowerCase().includes(search));
      const recipeDifficulty = normalizeDifficulty(recipe.difficulty);
      const difficultyMatch =
        activeDifficulty === "All" || recipeDifficulty === activeDifficulty;
      const time = recipe.totalTimeMinutes ?? 0;
      let matchesTime = true;
      if (activeCookingTime === "<=20 min") {
        matchesTime = time > 0 && time <= 20;
      } else if (activeCookingTime === "20-45 min") {
        matchesTime = time >= 20 && time <= 45;
      } else if (activeCookingTime === ">45 min") {
        matchesTime = time > 45;
      }
      return matchesSearch && difficultyMatch && matchesTime;
    });
  }, [trimmedSearch, displayedRecipes, activeDifficulty, activeCookingTime]);

  const navigateToRecipe = (recipeId: string) => {
    router.push({
      pathname: "/recipe/[recipeId]",
      params: {
        recipeId,
        source: "recommended",
        backTo: "/recommended-recipes",
      },
    });
  };

  const renderRecipeCard = ({ item }: { item: RecommendedRecipeDto }) => {
    const cover = item.coverImageUrl ?? null;
    const recipeDifficulty = normalizeDifficulty(item.difficulty);
    const infoParts = [
      item.totalTimeMinutes ? `${item.totalTimeMinutes}m` : null,
      recipeDifficulty !== "None" ? recipeDifficulty : null,
    ].filter(Boolean) as string[];
    const isCommunityRecipe = item.type === "User";

    return (
      <View className="flex-1 px-1">
        <TouchableOpacity
          onPress={() => navigateToRecipe(item.recipeId)}
          activeOpacity={0.85}
          className="w-full rounded-2xl overflow-hidden"
          style={{ height: CARD_HEIGHT, backgroundColor: colors.card }}
        >
          <View
            style={{ height: CARD_IMAGE_HEIGHT, backgroundColor: colors.card }}
            className="overflow-hidden"
          >
            {cover ? (
              <Image
                source={{ uri: cover }}
                className="h-full w-full"
                resizeMode="cover"
              />
            ) : (
              <View className="flex-1 items-center justify-center">
                <MaterialCommunityIcons
                  name="food"
                  size={32}
                  color={colors.textMuted}
                />
              </View>
            )}
            {isCommunityRecipe && (
              <View
                className="absolute top-2 left-2 flex-row items-center bg-black/60 rounded-full px-1.5 py-0.5"
                accessibilityLabel={
                  item.authorNickname
                    ? `By ${item.authorNickname}`
                    : "Community recipe"
                }
              >
                {item.authorAvatarUrl ? (
                  <Image
                    source={{ uri: item.authorAvatarUrl }}
                    className="w-4 h-4 rounded-full"
                    accessibilityLabel={item.authorNickname ?? "Author"}
                  />
                ) : (
                  <Icon name="users" size={12} color={colors.overlayText} />
                )}
                <Text className="text-[10px] ml-1 font-medium" style={{ color: colors.overlayText }}>
                  Community
                </Text>
              </View>
            )}
          </View>
          <View className="flex-1 px-3 py-2 justify-between">
            <View>
              <Text
                className="text-base font-semibold"
                style={{ color: colors.textPrimary }}
                numberOfLines={2}
              >
                {item.title}
              </Text>
              {(item.tags ?? []).length > 0 && (
                <Text
                  className="text-xs mt-1"
                  style={{ color: colors.textSecondary }}
                  numberOfLines={1}
                  ellipsizeMode="tail"
                >
                  {(() => {
                    const tags = item.tags ?? [];
                    const visible = tags.slice(0, 2);
                    const extras = tags.length - visible.length;
                    const text = visible.join(" · ");
                    return extras > 0 ? `${text} · ...` : text;
                  })()}
                </Text>
              )}
            </View>
            <View className="flex-row items-center justify-between">
              <Text className="text-xs" style={{ color: colors.textMuted }}>
                {infoParts.join(" · ") || "Details coming soon"}
              </Text>
              {item.servings ? (
                <Text className="text-xs" style={{ color: colors.textMuted }}>
                  {item.servings} servings
                </Text>
              ) : null}
            </View>
          </View>
        </TouchableOpacity>
      </View>
    );
  };

  const renderEmptyState = () => (
    <View className="flex-1 items-center justify-center py-10">
      <MaterialCommunityIcons
        name="chef-hat"
        size={80}
        color={colors.textMuted}
      />
      <Text className="text-lg font-semibold mt-4 text-center" style={{ color: colors.textPrimary }}>
        No recommendations yet
      </Text>
      <Text className="text-center mt-2 px-8" style={{ color: colors.textMuted }}>
        {message ||
          "Set your preferences to get personalized recipe recommendations."}
      </Text>
    </View>
  );

  const renderSearchEmptyState = () => (
    <View className="flex-1 items-center justify-center py-10">
      <MaterialCommunityIcons
        name="magnify"
        size={72}
        color={colors.textMuted}
      />
      <Text className="text-lg font-semibold mt-4 text-center" style={{ color: colors.textPrimary }}>
        No matching recipes
      </Text>
      <Text className="text-center mt-2 px-8" style={{ color: colors.textMuted }}>
        Try searching by a different keyword or tag.
      </Text>
    </View>
  );

  const renderFooter = () => {
    if (!isFetchingNextPage) return <View className="h-10" />;
    return (
      <View className="py-4 items-center">
        <ActivityIndicator color={colors.accent} />
        <Text className="mt-2 text-sm" style={{ color: colors.textMuted }}>Loading more...</Text>
      </View>
    );
  };

  const handleEndReached = () => {
    if (hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  };

  return (
    <SafeAreaView
      style={{ flex: 1, backgroundColor: colors.bg }}
      edges={["right", "left", "top"]}
    >
      <View className="px-5 pt-6 pb-3">
        <View className="flex-row items-center gap-4">
          <TouchableOpacity
            onPress={() => router.back()}
            className="items-center justify-center"
          >
            <Ionicons name="chevron-back" size={26} color={colors.accent} />
          </TouchableOpacity>
          <View>
            <Text className="text-2xl font-semibold" style={{ color: colors.textPrimary }}>
              Recommended
            </Text>
            <Text className="text-sm" style={{ color: colors.textMuted }}>Recipes</Text>
          </View>
        </View>
      </View>

      <View className="px-5 pb-3">
        <SearchBar
          placeholder="Search recipes..."
          value={searchValue}
          onChangeText={setSearchValue}
          returnKeyType="search"
        />
        <View className="mt-4">
          <Text className="text-xs uppercase" style={{ color: colors.textSecondary }}>Difficulty</Text>
          <View className="mt-2 flex-row flex-wrap gap-2">
            {difficultyFilters.map((value) => {
              const isActive = activeDifficulty === value;
              return (
                <TouchableOpacity
                  key={value}
                  onPress={() => setActiveDifficulty(value)}
                  className="rounded-full border px-3 py-1.5"
                  style={{
                    borderColor: isActive ? colors.accent : colors.border,
                    backgroundColor: isActive ? colors.accent : colors.card,
                  }}
                >
                  <Text
                    className="text-xs font-semibold"
                    style={{ color: isActive ? colors.bg : colors.textSecondary }}
                  >
                    {value}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
        </View>
        <View className="mt-4">
          <Text className="text-xs uppercase" style={{ color: colors.textSecondary }}>Cooking Time</Text>
          <View className="mt-2 flex-row flex-wrap gap-2">
            {cookingTimeFilters.map((value) => {
              const isActive = activeCookingTime === value;
              return (
                <TouchableOpacity
                  key={value}
                  onPress={() => setActiveCookingTime(value)}
                  className="rounded-full border px-3 py-1.5"
                  style={{
                    borderColor: isActive ? colors.accent : colors.border,
                    backgroundColor: isActive ? colors.accent : colors.card,
                  }}
                >
                  <Text
                    className="text-xs font-semibold"
                    style={{ color: isActive ? colors.bg : colors.textSecondary }}
                  >
                    {value}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
        </View>
      </View>

      {isLoading && displayedRecipes.length === 0 ? (
        <View className="flex-1 items-center justify-center">
          <ActivityIndicator size="large" color={colors.accent} />
          <Text className="mt-4" style={{ color: colors.textMuted }}>Loading recommendations...</Text>
        </View>
      ) : filteredRecipes.length === 0 ? (
        hasSearch ? (
          renderSearchEmptyState()
        ) : (
          renderEmptyState()
        )
      ) : (
        <FlatList
          data={filteredRecipes}
          keyExtractor={(item) => item.recipeId}
          renderItem={renderRecipeCard}
          numColumns={2}
          columnWrapperStyle={{
            justifyContent: "space-between",
            paddingHorizontal: 14,
            marginBottom: GRID_VERTICAL_GAP,
          }}
          contentContainerStyle={{
            paddingTop: 8,
            paddingBottom: 32,
          }}
          ListFooterComponent={renderFooter}
          onEndReached={handleEndReached}
          onEndReachedThreshold={0.5}
          refreshControl={
            <RefreshControl
              refreshing={isRefreshing}
              onRefresh={handleRefresh}
              tintColor="white"
            />
          }
          showsVerticalScrollIndicator={false}
        />
      )}
    </SafeAreaView>
  );
}
