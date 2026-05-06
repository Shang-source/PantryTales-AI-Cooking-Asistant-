import React, { useCallback, useEffect, useMemo, useState } from "react";
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Modal,
  Pressable,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { Ionicons } from "@expo/vector-icons";
import { useFocusEffect, useRouter } from "expo-router";
import Icon from "react-native-vector-icons/Feather";
import MaterialCommunityIcons from "react-native-vector-icons/MaterialCommunityIcons";
import { useSmartRecipes, SmartRecipeDto } from "@/hooks/useSmartRecipes";
import { useSmartRecipesStream } from "@/hooks/useSmartRecipesStream";
import {
  useInventoryItems,
  calculateMissingIngredients,
  findInventoryMatch,
} from "@/hooks/useInventoryItems";
import { useAuthQuery } from "@/hooks/useApi";
import { ApiResponse } from "@/types/api";
import { HouseholdMembersListDto } from "@/types/household";
import { useTheme } from "@/contexts/ThemeContext";

type FilterType = "all" | "canMake" | "missing";

const getLocalDateKey = () => {
  const today = new Date();
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
};

interface EnhancedSmartRecipe extends SmartRecipeDto {
  /** Recalculated missing count based on current inventory */
  calculatedMissingCount: number;
  /** Recalculated missing ingredients based on current inventory */
  calculatedMissingIngredients: string[];
  /** Final missing count used for filters/badges */
  displayMissingCount: number;
  /** Final missing ingredients used for display */
  displayMissingIngredients: string[];
}

export default function SmartRecipesPage() {
  const router = useRouter();
  const { colors } = useTheme();

  // Track if user has completed generation in current session
  const [hasCompletedGeneration, setHasCompletedGeneration] = useState(false);

  // Track if user chose to keep stale recipes
  const [showStaleRecipes, setShowStaleRecipes] = useState(false);

  // Initially fetch without stale recipes; only show stale if user explicitly chooses "Cancel"
  const {
    recipes: cachedRecipes,
    isLoading,
    message,
    refetch,
  } = useSmartRecipes({ autoFetch: true, allowStale: showStaleRecipes });

  // Show recipes view if we have recipes or are currently generating
  const hasExistingRecipes = cachedRecipes.length > 0;

  // Streaming hook for progressive recipe generation
  const {
    recipes: streamedRecipes,
    isStreaming,
    progress,
    error: streamError,
    startStreaming,
    cancelStreaming,
    resetStream,
  } = useSmartRecipesStream();

  // Use streamed recipes when streaming, otherwise use cached recipes
  const recipes =
    isStreaming || streamedRecipes.length > 0 ? streamedRecipes : cachedRecipes;
  const isGenerating = isStreaming;

  const { data: inventoryData } = useInventoryItems();
  const [activeFilter, setActiveFilter] = useState<FilterType>("all");
  const [focusTick, setFocusTick] = useState(0);

  // Cleanup streaming on unmount
  useEffect(() => {
    return () => {
      cancelStreaming();
    };
  }, [cancelStreaming]);

  // Refresh data and re-evaluate stale state when screen gains focus
  useFocusEffect(
    useCallback(() => {
      setFocusTick((tick) => tick + 1);
      void refetch();
    }, [refetch]),
  );

  // Servings dialog state
  const [showServingsDialog, setShowServingsDialog] = useState(false);
  const [servings, setServings] = useState<number>(2);

  // Get household info to determine default servings
  const { data: meResp } = useAuthQuery<
    ApiResponse<{ activeHouseholdId?: string }>
  >(["households-me"], "/api/households/me");
  const activeHouseholdId = meResp?.data?.activeHouseholdId;

  // Fetch household members to get default servings (household size)
  const { data: membersResp } = useAuthQuery<
    ApiResponse<HouseholdMembersListDto>
  >(
    ["household-members", activeHouseholdId ?? ""],
    activeHouseholdId ? `/api/households/${activeHouseholdId}/members` : "",
    { enabled: !!activeHouseholdId },
  );
  const householdSize = membersResp?.data?.activeMemberCount ?? 2;

  // Track if dialog has been shown today to avoid re-showing after cancel
  const [dialogShownForDate, setDialogShownForDate] = useState<string | null>(null);
  const todayKey = useMemo(() => getLocalDateKey(), [focusTick]);

  // Show servings dialog immediately when entering the page (once per day)
  useEffect(() => {
    // Show dialog if:
    // 1. Not currently generating
    // 2. Dialog is not already showing
    // 3. Haven't shown dialog today yet
    // 4. Haven't already opted into showing stale recipes (user clicked Cancel)
    const shouldShowDialog =
      !showStaleRecipes &&
      !isGenerating &&
      !showServingsDialog &&
      dialogShownForDate !== todayKey;

    if (shouldShowDialog) {
      setServings(householdSize > 0 ? householdSize : 2);
      setShowServingsDialog(true);
      setDialogShownForDate(todayKey);
    }
  }, [
    showStaleRecipes,
    isGenerating,
    showServingsDialog,
    dialogShownForDate,
    todayKey,
    householdSize,
  ]);

  // Update servings when household size loads (if dialog is open)
  useEffect(() => {
    if (showServingsDialog && householdSize > 0) {
      setServings(householdSize);
    }
  }, [householdSize, showServingsDialog]);

  // Handler for the refresh button - shows servings dialog
  const handleRefreshClick = () => {
    setServings(householdSize > 0 ? householdSize : 2);
    setShowServingsDialog(true);
  };

  // Confirm generation with selected servings - use streaming
  const handleConfirmGenerate = () => {
    setShowServingsDialog(false);
    // Mark generation as completed so autoFetch becomes enabled
    if (!hasCompletedGeneration) {
      setHasCompletedGeneration(true);
    }
    // Reset any previous stream state and start streaming
    resetStream();
    startStreaming(servings);
  };

  // Cancel the dialog - user doesn't want to generate, try to load stale recipes
  const handleCancelDialog = () => {
    setShowServingsDialog(false);
    // If no fresh recipes exist, try loading stale recipes instead
    if (!hasExistingRecipes && !showStaleRecipes) {
      setShowStaleRecipes(true);
    }
  };

  // Increment/decrement servings
  const incrementServings = () => setServings((s) => Math.min(s + 1, 20));
  const decrementServings = () => setServings((s) => Math.max(s - 1, 1));

  const buildIngredientList = (recipe: SmartRecipeDto) => {
    const seen = new Set<string>();
    const combined: { name: string; isOptional?: boolean }[] = [];

    const add = (name: string, isOptional = false) => {
      const key = name.trim().toLowerCase();
      if (!key || seen.has(key)) return;
      seen.add(key);
      combined.push({ name, isOptional });
    };

    recipe.ingredients.forEach((ing) => add(ing.name, ing.isOptional));
    recipe.missingIngredients.forEach((name) => add(name, false));

    return combined;
  };

  // Get inventory items for comparison
  const inventoryItems = useMemo(() => {
    const items = inventoryData?.data?.data ?? [];
    return items.map((item) => ({
      name: item.name,
      amount: item.amount,
      unit: item.unit,
    }));
  }, [inventoryData]);

  // Enhance recipes with recalculated missing ingredients based on current inventory
  const enhancedRecipes = useMemo(() => {
    return recipes.map((recipe): EnhancedSmartRecipe => {
      const { missingIngredients, missingCount } = calculateMissingIngredients(
        buildIngredientList(recipe),
        inventoryItems,
      );

      // Normalize and dedupe missing ingredients using case-insensitive comparison
      // to avoid showing duplicates like "Salt" and "salt"
      // Also filter out API-reported missing ingredients if they're now in inventory
      const mergedMissing = (() => {
        const seen = new Set<string>();
        const result: string[] = [];

        const add = (name: string) => {
          const key = name.trim().toLowerCase();
          if (!key || seen.has(key)) return;
          seen.add(key);
          result.push(name.trim());
        };

        // Add API-reported missing ingredients only if still not in inventory
        recipe.missingIngredients.forEach((name) => {
          const match = findInventoryMatch(name, inventoryItems);
          if (!match) {
            add(name);
          }
        });
        // Then add locally-calculated missing ingredients
        missingIngredients.forEach(add);

        return result;
      })();

      // Derive count from the final list to keep badge and list consistent
      const displayMissingCount = mergedMissing.length;

      return {
        ...recipe,
        calculatedMissingCount: missingCount,
        calculatedMissingIngredients: missingIngredients,
        displayMissingCount,
        displayMissingIngredients: mergedMissing,
      };
    });
  }, [recipes, inventoryItems]);

  const filteredRecipes = useMemo(() => {
    switch (activeFilter) {
      case "canMake":
        return enhancedRecipes.filter((r) => r.displayMissingCount === 0);
      case "missing":
        return enhancedRecipes.filter(
          (r) => r.displayMissingCount >= 1 && r.displayMissingCount <= 2,
        );
      default:
        return enhancedRecipes;
    }
  }, [enhancedRecipes, activeFilter]);

  const filterCounts = useMemo(
    () => ({
      all: enhancedRecipes.length,
      canMake: enhancedRecipes.filter((r) => r.displayMissingCount === 0)
        .length,
      missing: enhancedRecipes.filter(
        (r) => r.displayMissingCount >= 1 && r.displayMissingCount <= 2,
      ).length,
    }),
    [enhancedRecipes],
  );

  // Map difficulty number/string to display label
  const getDifficultyLabel = (
    difficulty: SmartRecipeDto["difficulty"] | number,
  ): string | null => {
    const map: Record<number | string, string> = {
      1: "Easy",
      2: "Medium",
      3: "Hard",
      Easy: "Easy",
      Medium: "Medium",
      Hard: "Hard",
    };
    return map[difficulty] ?? null;
  };

  const getMissingBadgeColor = (count: number) => {
    if (count === 0) return "bg-green-500";
    if (count <= 2) return "bg-yellow-500";
    return "bg-red-400";
  };

  const renderRecipeCard = (recipe: EnhancedSmartRecipe) => (
    <TouchableOpacity
      key={recipe.id}
      className="rounded-2xl overflow-hidden mb-3"
      style={{ backgroundColor: colors.card }}
      onPress={() =>
        router.push({
          pathname: "/(tabs)/recipe/[recipeId]",
          params: { recipeId: recipe.recipeId, source: "smart" },
        })
      }
      activeOpacity={0.7}
    >
      {/* Header */}
      <View className="px-3 pt-3 pb-2">
        <View className="flex-row items-start justify-between">
          <Text
            className="text-base font-semibold flex-1 mr-2"
            style={{ color: colors.textPrimary }}
            numberOfLines={1}
          >
            {recipe.title}
          </Text>
          {/* Missing ingredients badge */}
          <View
            className={`px-2.5 py-1 rounded-full ${getMissingBadgeColor(recipe.displayMissingCount)}`}
          >
            {recipe.displayMissingCount === 0 ? (
              <Text className="text-xs font-medium" style={{ color: colors.bg }}>✓ Ready</Text>
            ) : (
              <Text className="text-xs font-medium" style={{ color: colors.bg }}>
                Missing {recipe.displayMissingCount}
              </Text>
            )}
          </View>
        </View>

        {/* Meta info row */}
        <View className="flex-row items-center gap-3 mt-1.5">
          {recipe.totalTimeMinutes && (
            <View className="flex-row items-center">
              <Icon name="clock" size={11} color={colors.textSecondary} />
              <Text className="text-xs ml-1" style={{ color: colors.textSecondary }}>
                {recipe.totalTimeMinutes} min
              </Text>
            </View>
          )}
          {getDifficultyLabel(recipe.difficulty) && (
            <View className="flex-row items-center">
              <MaterialCommunityIcons
                name="chef-hat"
                size={11}
                color={colors.textSecondary}
              />
              <Text className="text-xs ml-1" style={{ color: colors.textSecondary }}>
                {getDifficultyLabel(recipe.difficulty)}
              </Text>
            </View>
          )}
          {recipe.servings && (
            <View className="flex-row items-center">
              <Icon name="users" size={11} color={colors.textSecondary} />
              <Text className="text-xs ml-1" style={{ color: colors.textSecondary }}>
                {recipe.servings}{" "}
                {recipe.servings === 1 ? "serving" : "servings"}
              </Text>
            </View>
          )}
        </View>
      </View>

      {/* Ingredients section */}
      {recipe.ingredients.length > 0 && (
        <View className="px-3 pb-2.5 pt-2" style={{ borderTopWidth: 1, borderTopColor: colors.border }}>
          <View className="flex-row flex-wrap gap-1.5">
            {recipe.ingredients.slice(0, 6).map((ing, idx) => {
              const isMissing = recipe.displayMissingIngredients.some(
                (m) => m.toLowerCase() === ing.name.toLowerCase(),
              );
              return (
                <View
                  key={idx}
                  className="flex-row items-center px-2 py-0.5 rounded-full"
                  style={{ backgroundColor: isMissing ? "rgba(239,68,68,0.2)" : colors.card }}
                >
                  <Icon
                    name={isMissing ? "x" : "check"}
                    size={9}
                    color={isMissing ? "#f87171" : "#22c55e"}
                  />
                  <Text
                    className="text-xs ml-1"
                    style={{ color: isMissing ? "#fca5a5" : colors.textSecondary }}
                  >
                    {ing.name}
                  </Text>
                </View>
              );
            })}
            {recipe.ingredients.length > 6 && (
              <View className="px-2 py-0.5 rounded-full" style={{ backgroundColor: colors.card }}>
                <Text className="text-xs" style={{ color: colors.textMuted }}>
                  +{recipe.ingredients.length - 6}
                </Text>
              </View>
            )}
          </View>
        </View>
      )}
    </TouchableOpacity>
  );

  const renderEmptyState = () => (
    <View className="flex-1 items-center justify-center py-20">
      <MaterialCommunityIcons
        name="chef-hat"
        size={64}
        color={colors.textMuted}
      />
      <Text className="text-lg mt-4 text-center px-10" style={{ color: colors.textMuted }}>
        {message ||
          "Add items to your inventory to get personalized recipe suggestions!"}
      </Text>
      <TouchableOpacity
        className="mt-6 px-6 py-3 rounded-full"
        style={{ backgroundColor: colors.accent }}
        onPress={() => router.push("/MyInventory")}
      >
        <Text className="font-semibold" style={{ color: colors.bg }}>Add Inventory</Text>
      </TouchableOpacity>
    </View>
  );

  return (
    <SafeAreaView
      style={{ flex: 1, backgroundColor: colors.bg }}
      edges={["right", "left", "top"]}
    >
      {/* Header */}
      <View className="flex-row items-center justify-between px-5 py-3">
        <TouchableOpacity onPress={() => router.back()} className="p-2">
          <Ionicons name="chevron-back" size={26} color={colors.accent} />
        </TouchableOpacity>
        <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>Smart Recipes</Text>
        <TouchableOpacity
          onPress={handleRefreshClick}
          disabled={isGenerating}
          className="p-2"
          style={{ opacity: isGenerating ? 0.5 : 1 }}
        >
          <Icon name="refresh-cw" size={20} color={colors.accent} />
        </TouchableOpacity>
      </View>

      {/* Servings Dialog */}
      <Modal
        visible={showServingsDialog}
        transparent
        animationType="fade"
        onRequestClose={handleCancelDialog}
      >
        <Pressable
          className="flex-1 bg-black/50 justify-center items-center"
          onPress={handleCancelDialog}
        >
          <Pressable
            style={{ backgroundColor: colors.bg }}
            className="rounded-2xl p-6 mx-6 w-80"
            onPress={(e) => e.stopPropagation()}
          >
            <Text className="text-xl font-semibold text-center mb-2" style={{ color: colors.textPrimary }}>
              Generate Recipes
            </Text>
            <Text className="text-sm text-center mb-6" style={{ color: colors.textSecondary }}>
              How many servings do you need?
            </Text>

            {/* Servings Selector */}
            <View className="flex-row items-center justify-center gap-4 mb-6">
              <TouchableOpacity
                onPress={decrementServings}
                disabled={servings <= 1}
                className="w-12 h-12 rounded-full items-center justify-center"
                style={{
                  backgroundColor: servings <= 1 ? colors.border : colors.accent,
                  opacity: servings <= 1 ? 0.5 : 1,
                }}
              >
                <Ionicons
                  name="remove"
                  size={24}
                  color={servings <= 1 ? colors.textMuted : colors.bg}
                />
              </TouchableOpacity>

              <View className="w-20 items-center">
                <Text className="text-4xl font-bold" style={{ color: colors.textPrimary }}>
                  {servings}
                </Text>
                <Text className="text-sm" style={{ color: colors.textMuted }}>servings</Text>
              </View>

              <TouchableOpacity
                onPress={incrementServings}
                disabled={servings >= 20}
                className="w-12 h-12 rounded-full items-center justify-center"
                style={{
                  backgroundColor: servings >= 20 ? colors.border : colors.accent,
                  opacity: servings >= 20 ? 0.5 : 1,
                }}
              >
                <Ionicons
                  name="add"
                  size={24}
                  color={servings >= 20 ? colors.textMuted : colors.bg}
                />
              </TouchableOpacity>
            </View>

            {/* Info text */}
            <Text className="text-xs text-center mb-6" style={{ color: colors.textMuted }}>
              Default is based on your household size ({householdSize} members)
            </Text>

            {/* Buttons */}
            <View className="flex-row gap-3">
              <TouchableOpacity
                onPress={handleCancelDialog}
                className="flex-1 py-3 rounded-full"
                style={{ backgroundColor: colors.card }}
              >
                <Text className="text-center font-medium" style={{ color: colors.textPrimary }}>
                  Cancel
                </Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={handleConfirmGenerate}
                className="flex-1 py-3 rounded-full"
                style={{ backgroundColor: colors.accent }}
              >
                <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                  Generate
                </Text>
              </TouchableOpacity>
            </View>
          </Pressable>
        </Pressable>
      </Modal>

      {/* Content */}
      {isLoading ? (
        <View className="flex-1 items-center justify-center">
          <ActivityIndicator size="large" color={colors.accent} />
          <Text className="mt-4" style={{ color: colors.textMuted }}>Loading...</Text>
        </View>
      ) : !hasExistingRecipes && !isGenerating && showServingsDialog ? (
        // Show nothing only while servings dialog is visible
        <View className="flex-1" />
      ) : isGenerating && streamedRecipes.length === 0 ? (
        <View className="flex-1 items-center justify-center">
          <ActivityIndicator size="large" color={colors.accent} />
          <Text className="mt-4" style={{ color: colors.textMuted }}>
            {progress && progress.current > 0
              ? `Generated ${progress.current} recipe${progress.current > 1 ? "s" : ""}...`
              : "Generating recipes..."}
          </Text>

          {streamError && (
            <Text className="text-red-300 mt-2 text-sm">{streamError}</Text>
          )}
        </View>
      ) : recipes.length === 0 && !isStreaming ? (
        renderEmptyState()
      ) : (
        <ScrollView className="flex-1 px-5">
          {/* Filter tabs */}
          <View className="flex-row mb-4 gap-2">
            <TouchableOpacity
              className="px-4 py-2 rounded-full"
              style={{ backgroundColor: activeFilter === "all" ? colors.accent : colors.card }}
              onPress={() => setActiveFilter("all")}
            >
              <Text
                className="text-sm font-medium"
                style={{ color: activeFilter === "all" ? colors.bg : colors.textSecondary }}
              >
                All ({filterCounts.all})
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              className={`px-4 py-2 rounded-full ${activeFilter === "canMake" ? "bg-green-500" : ""}`}
              style={activeFilter !== "canMake" ? { backgroundColor: colors.card } : undefined}
              onPress={() => setActiveFilter("canMake")}
            >
              <Text
                className="text-sm font-medium"
                style={{ color: activeFilter === "canMake" ? colors.bg : colors.textSecondary }}
              >
                Ready ({filterCounts.canMake})
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              className={`px-4 py-2 rounded-full ${activeFilter === "missing" ? "bg-yellow-500" : ""}`}
              style={activeFilter !== "missing" ? { backgroundColor: colors.card } : undefined}
              onPress={() => setActiveFilter("missing")}
            >
              <Text
                className="text-sm font-medium"
                style={{ color: activeFilter === "missing" ? colors.bg : colors.textSecondary }}
              >
                Missing 1-2 ({filterCounts.missing})
              </Text>
            </TouchableOpacity>
          </View>

          {/* Streaming progress indicator */}
          {isStreaming && (
            <View className="rounded-xl p-3 mb-4" style={{ backgroundColor: colors.card }}>
              <View className="flex-row items-center">
                <ActivityIndicator size="small" color={colors.accent} />
                <Text className="ml-2" style={{ color: colors.textSecondary }}>
                  {progress && progress.current > 0
                    ? `Generated ${progress.current} recipe${progress.current > 1 ? "s" : ""}...`
                    : "Generating recipes..."}
                </Text>
              </View>
            </View>
          )}

          {/* Stats */}
          <View className="flex-row items-center mb-4">
            <MaterialCommunityIcons name="auto-fix" size={20} color={colors.accent} />
            <Text className="ml-2" style={{ color: colors.textSecondary }}>
              {filterCounts.canMake} recipe{filterCounts.canMake !== 1 ? "s" : ""} ready to make
            </Text>
          </View>

          {/* Recipe list */}
          {filteredRecipes.length > 0 ? (
            filteredRecipes.map(renderRecipeCard)
          ) : (
            <View className="items-center py-10">
              <MaterialCommunityIcons
                name="food-off"
                size={48}
                color={colors.textMuted}
              />
              <Text className="mt-3 text-center" style={{ color: colors.textMuted }}>
                No recipes match this filter
              </Text>
            </View>
          )}

          {/* Bottom padding */}
          <View className="h-8" />
        </ScrollView>
      )}
    </SafeAreaView>
  );
}
