import { useEffect, useMemo, useState } from "react";
import {
  ActivityIndicator,
  Image,
  LayoutChangeEvent,
  NativeScrollEvent,
  NativeSyntheticEvent,
  ScrollView,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import Icon from "react-native-vector-icons/Feather";
import { Ionicons, MaterialCommunityIcons } from "@expo/vector-icons";
import { ChevronDown, ChevronUp, Share2 } from "lucide-react-native";

import { useAuthQuery } from "@/hooks/useApi";
import { useAddChecklistBatch } from "@/hooks/useChecklist";
import {
  findInventoryMatch,
  useInventoryItems,
} from "@/hooks/useInventoryItems";
import { ApiResponse } from "@/types/api";
import { RecipeDetailDto, RecipeVisibility } from "@/types/recipes";
import { NutritionSection } from "@/components/NutritionSection";
import { toast } from "@/components/sonner";
import { useTheme } from "@/contexts/ThemeContext";

export type RecipeDetailMode = "view" | "edit";

export type RecipeEditPrefill = {
  recipeId: string;
  title: string;
  description: string;
  steps: string[];
  ingredients: {
    name: string;
    amount: number | null;
    unit: string | null;
    isOptional: boolean;
  }[];
  tags: string[];
  imageUrls: string[];
  visibility: RecipeVisibility;
};

export interface RecipeDetailProps {
  recipeId?: string;
  mode?: RecipeDetailMode;
  variant?: "page" | "modal";
  layout?: "default" | "recommended";
  onPrefill?: (values: RecipeEditPrefill) => void;
  scrollable?: boolean;
  onRecipeReady?: (recipe: RecipeDetailDto) => void;
  onStartCooking?: () => void;
  showStartCookingButton?: boolean;
  showShareButton?: boolean;
  onSharePress?: () => void;
}

const RECIPE_DATE_LOCALE = "en-US";

const formatDate = (value: string | undefined) => {
  if (!value) return "Unknown date";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString(RECIPE_DATE_LOCALE, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
};

const formatAmount = (
  amount: number | null | undefined,
  unit?: string | null,
) => {
  // Treat 0 and null the same - don't show a numeric amount
  if ((amount == null || amount === 0) && !unit) return "to taste";
  if (amount == null || amount === 0) return unit ?? "to taste";
  const amountLabel = `${amount}`;
  return unit ? `${amountLabel} ${unit}` : amountLabel;
};

export const mapRecipeDetailToEditValues = (
  detail: RecipeDetailDto,
): RecipeEditPrefill => ({
  recipeId: detail.id,
  title: detail.title,
  description: detail.description,
  steps: detail.steps ?? [],
  ingredients: (detail.ingredients ?? []).map((ingredient) => ({
    name: ingredient.name,
    amount: ingredient.amount ?? null,
    unit: ingredient.unit ?? null,
    isOptional: ingredient.isOptional,
  })),
  tags: detail.tags ?? [],
  imageUrls: detail.imageUrls ?? [],
  visibility: detail.visibility,
});

export function RecipeDetail({
  recipeId,
  mode = "view",
  variant = "page",
  layout = "default",
  onPrefill,
  scrollable = false,
  onRecipeReady,
  onStartCooking,
  showStartCookingButton = false,
  showShareButton = false,
  onSharePress,
}: RecipeDetailProps) {
  const { colors } = useTheme();
  const [mediaWidth, setMediaWidth] = useState(0);
  const [activeMediaIndex, setActiveMediaIndex] = useState(0);
  const [stepsExpanded, setStepsExpanded] = useState(false);
  const [ingredientsExpanded, setIngredientsExpanded] = useState(true);
  const query = useAuthQuery<ApiResponse<RecipeDetailDto>>(
    ["recipe", recipeId ?? ""],
    `/api/recipes/${recipeId}`,
    {
      enabled: Boolean(recipeId),
      staleTime: 0,
      refetchOnMount: "always",
      refetchOnWindowFocus: "always",
    },
  );

  const { data, isLoading, isError, error, refetch } = query;
  const recipe = data?.data;
  const isRecommendedLayout = layout === "recommended";
  const isInitialSyncPending = Boolean(recipeId) && !query.isFetchedAfterMount;

  const { data: inventoryData } = useInventoryItems();
  const inventoryItems = useMemo(
    () => inventoryData?.data?.data ?? [],
    [inventoryData],
  );
  const addToChecklist = useAddChecklistBatch();

  useEffect(() => {
    if (!recipeId || !recipe) return;
    if (mode !== "edit" || !onPrefill) return;
    const values = mapRecipeDetailToEditValues(recipe);
    onPrefill(values);
  }, [recipeId, recipe, mode, onPrefill]);

  useEffect(() => {
    setActiveMediaIndex(0);
    setMediaWidth(0);
  }, [recipeId]);

  useEffect(() => {
    if (!recipe) return;
    if (!query.isFetchedAfterMount) return;
    onRecipeReady?.(recipe);
  }, [query.isFetchedAfterMount, recipe, onRecipeReady]);

  const handleMediaLayout = (event: LayoutChangeEvent) => {
    const width = Math.round(event.nativeEvent.layout.width);
    if (width && width !== mediaWidth) {
      setMediaWidth(width);
    }
  };

  const handleMediaScroll = (
    event: NativeSyntheticEvent<NativeScrollEvent>,
  ) => {
    const measurement = event.nativeEvent.layoutMeasurement.width;
    if (!measurement) return;
    const offset = event.nativeEvent.contentOffset.x;
    const index = Math.round(offset / measurement);
    if (index !== activeMediaIndex) {
      setActiveMediaIndex(index);
    }
  };

  if (!recipeId) {
    return (
      <View className="items-center justify-center rounded-3xl px-4 py-8" style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}>
        <Text className="text-center text-base font-semibold" style={{ color: colors.textPrimary }}>
          Select a recipe to continue.
        </Text>
      </View>
    );
  }

  if (isLoading || isInitialSyncPending) {
    return (
      <View className="items-center justify-center rounded-3xl px-4 py-8" style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}>
        <ActivityIndicator color={colors.accent} />
        <Text className="mt-3 text-sm" style={{ color: colors.textSecondary }}>
          Loading recipe details...
        </Text>
      </View>
    );
  }

  if (isError || !recipe) {
    return (
      <View className="items-center justify-center rounded-3xl px-4 py-8" style={{ backgroundColor: `${colors.error}15`, borderWidth: 1, borderColor: `${colors.error}40` }}>
        <Text className="text-center text-base font-semibold" style={{ color: colors.error }}>
          Unable to load recipe detail
        </Text>
        <Text className="mt-2 text-center text-sm" style={{ color: `${colors.error}CC` }}>
          {error?.message ?? "Please try again later."}
        </Text>
        <TouchableOpacity
          className="mt-4 rounded-full px-5 py-2"
          style={{ backgroundColor: colors.card }}
          onPress={() => refetch()}
        >
          <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Retry</Text>
        </TouchableOpacity>
      </View>
    );
  }

  // Filter for valid http(s) URLs only - empty strings and invalid URLs are excluded
  // Model (AI-generated) recipes typically don't have actual images
  const galleryImages = recipe.type === "Model"
    ? []
    : recipe.imageUrls?.filter((url): url is string =>
        Boolean(url) && (url.startsWith('http://') || url.startsWith('https://'))
      ) ?? [];
  const tags = recipe.tags ?? [];
  const steps = recipe.steps ?? [];
  const ingredientList = recipe.ingredients ?? [];
  const timeMinutes =
    recipe.totalTimeMinutes != null && recipe.totalTimeMinutes > 0
      ? recipe.totalTimeMinutes
      : null;
  const difficultyLabel =
    recipe.difficulty && recipe.difficulty !== "None"
      ? recipe.difficulty
      : null;
  const servingsLabel =
    recipe.servings != null && recipe.servings > 0
      ? `${recipe.servings} servings`
      : null;

  const ingredientStatuses = useMemo(() => {
    if (!ingredientList.length) return [];
    return ingredientList.map((ingredient) => {
      const match = findInventoryMatch(ingredient.name, inventoryItems);
      const neededAmount = ingredient.amount ?? null;
      const hasEnough = match
        ? neededAmount == null || match.availableAmount >= neededAmount
        : false;
      return {
        ...ingredient,
        isAvailable: hasEnough,
      };
    });
  }, [ingredientList, inventoryItems]);

  const sortedIngredients = useMemo(() => {
    return [...ingredientStatuses].sort(
      (a, b) => Number(a.isAvailable) - Number(b.isAvailable),
    );
  }, [ingredientStatuses]);

  const missingIngredients = useMemo(
    () => ingredientStatuses.filter((ingredient) => !ingredient.isAvailable),
    [ingredientStatuses],
  );

  const handleAddMissingToChecklist = () => {
    if (!recipeId) return;
    if (missingIngredients.length === 0) {
      toast.info("All ingredients are already in your inventory.");
      return;
    }

    const items = missingIngredients.map((ingredient) => ({
      name: ingredient.name,
      amount: ingredient.amount ?? 1,
      unit: ingredient.unit?.trim() || "unit",
      category: ingredient.category ?? "Other",
      fromRecipeId: recipeId,
    }));

    addToChecklist.mutate(
      { items, fromRecipeId: recipeId },
      {
        onSuccess: () => {
          toast.success("Added to checklist", {
            description: `${items.length} item(s) added to your checklist.`,
          });
        },
        onError: (error) => {
          toast.error("Failed to add to checklist", {
            description: error?.message ?? "Please try again.",
          });
        },
      },
    );
  };

  const metaRow = (
    <View className="mt-3 flex-row flex-wrap items-center gap-4">
      {timeMinutes != null && (
        <View className="flex-row items-center gap-1">
          <Icon name="clock" size={14} color={colors.textMuted} />
          <Text className="text-sm" style={{ color: colors.textSecondary }}>{timeMinutes} mins</Text>
        </View>
      )}
      {difficultyLabel && (
        <View className="flex-row items-center gap-1">
          <MaterialCommunityIcons
            name="chef-hat"
            size={14}
            color={colors.textMuted}
          />
          <Text className="text-sm" style={{ color: colors.textSecondary }}>{difficultyLabel}</Text>
        </View>
      )}
      {servingsLabel && (
        <View className="flex-row items-center gap-1">
          <Icon name="users" size={14} color={colors.textMuted} />
          <Text className="text-sm" style={{ color: colors.textSecondary }}>{servingsLabel}</Text>
        </View>
      )}
    </View>
  );

  const ingredientsSection = (
    <View className="rounded-2xl overflow-hidden" style={{ backgroundColor: `${colors.card}` }}>
      <TouchableOpacity
        onPress={() => setIngredientsExpanded(!ingredientsExpanded)}
        activeOpacity={0.8}
        className="flex-row items-center justify-between px-4 py-3"
      >
        <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
          Ingredients ({sortedIngredients.length})
        </Text>
        {ingredientsExpanded ? (
          <ChevronUp size={20} color={colors.accent} />
        ) : (
          <ChevronDown size={20} color={colors.accent} />
        )}
      </TouchableOpacity>

      {ingredientsExpanded && (
        <View className="px-4 pb-4">
          {sortedIngredients.length ? (
            <View className="gap-4">
              {sortedIngredients.map((ingredient, index) => (
                <View
                  key={`${recipe.id}-ingredient-${ingredient.recipeIngredientId ?? index}`}
                  className="flex-row items-center justify-between"
                >
                  <View className="flex-row items-center gap-3">
                    <Icon
                      name={ingredient.isAvailable ? "check" : "x"}
                      size={16}
                      color={ingredient.isAvailable ? colors.success : colors.error}
                    />
                    <Text
                      className="text-sm"
                      style={{ color: ingredient.isAvailable ? colors.textPrimary : colors.error }}
                    >
                      {ingredient.name}
                    </Text>
                  </View>
                  <Text className="text-sm" style={{ color: colors.textSecondary }}>
                    {formatAmount(ingredient.amount, ingredient.unit)}
                  </Text>
                </View>
              ))}
            </View>
          ) : (
            <Text className="text-sm" style={{ color: colors.textSecondary }}>
              No ingredients listed.
            </Text>
          )}
          {sortedIngredients.length > 0 && (
            <TouchableOpacity
              onPress={handleAddMissingToChecklist}
              activeOpacity={0.9}
              disabled={
                addToChecklist.isPending || missingIngredients.length === 0
              }
              className="mt-6 rounded-full py-3"
              style={{
                backgroundColor:
                  missingIngredients.length === 0
                    ? `${colors.success}33`
                    : colors.accent,
              }}
            >
              <Text
                className="text-center text-sm font-semibold"
                style={{
                  color:
                    missingIngredients.length === 0
                      ? colors.success
                      : colors.bg,
                }}
              >
                {addToChecklist.isPending
                  ? "Adding..."
                  : missingIngredients.length === 0
                    ? "✓ You have all ingredients!"
                    : "Add to Checklist"}
              </Text>
            </TouchableOpacity>
          )}
        </View>
      )}
    </View>
  );

  const modalLayout = (
    <View className="gap-5">
      {galleryImages.length > 0 && (
        <View className="overflow-hidden rounded-[28px]" style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}>
          <Image
            source={{ uri: galleryImages[0] }}
            className="h-[320px] w-full"
            resizeMode="cover"
          />
        </View>
      )}

      <View className="gap-3">
        <View className="flex-row items-start justify-between">
          <Text
            className="text-3xl font-bold flex-1 mr-2"
            style={{ color: colors.textPrimary }}
            numberOfLines={2}
          >
            {recipe.title}
          </Text>
          {showShareButton && onSharePress && (
            <TouchableOpacity onPress={onSharePress} className="p-1 mt-1">
              <Share2 size={22} color={colors.accent} />
            </TouchableOpacity>
          )}
        </View>
        {recipe.description ? (
          <Text className="text-base leading-7" style={{ color: colors.textSecondary }}>
            {recipe.description}
          </Text>
        ) : null}
        <View className="flex-row flex-wrap gap-2">
          {tags.length ? (
            tags.map((tag) => (
              <View
                key={`${recipe.id}-modal-tag-${tag}`}
                className="rounded-full px-3 py-1"
                style={{ backgroundColor: colors.card }}
              >
                <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>{tag}</Text>
              </View>
            ))
          ) : (
            <Text className="text-sm" style={{ color: colors.textSecondary }}>No tags provided.</Text>
          )}
        </View>
      </View>

      {/* Ingredients Section for modal layout */}
      {ingredientsSection}

      {/* Nutrition Section for modal layout */}
      <NutritionSection
        nutrition={{
          calories: recipe.calories,
          carbohydrates: recipe.carbohydrates,
          fat: recipe.fat,
          protein: recipe.protein,
          sugar: recipe.sugar,
          sodium: recipe.sodium,
          saturatedFat: recipe.saturatedFat,
          servings: recipe.servings,
        }}
      />

      <View className="rounded-2xl p-4" style={{ backgroundColor: colors.card }}>
        <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
          Cooking Steps
        </Text>
        <View className="mt-4 gap-3">
          {steps.length ? (
            steps.map((step, index) => (
              <View
                key={`${recipe.id}-modal-step-${index}`}
                className="flex-row items-start gap-3"
              >
                <View className="h-7 w-7 items-center justify-center rounded-full" style={{ backgroundColor: colors.accent }}>
                  <Text className="text-xs font-bold" style={{ color: colors.bg }}>
                    {index + 1}
                  </Text>
                </View>
                <Text className="flex-1 text-sm leading-6 pt-0.5" style={{ color: colors.textSecondary }}>
                  {step}
                </Text>
              </View>
            ))
          ) : (
            <Text className="text-sm" style={{ color: colors.textSecondary }}>No cooking steps yet.</Text>
          )}
        </View>
        <Text className="mt-4 text-xs" style={{ color: colors.textMuted }}>
          Updated {formatDate(recipe.updatedAt)}
        </Text>
      </View>
    </View>
  );

  const recommendedView = (
    <View className="gap-5">
      <View className="overflow-hidden rounded-2xl" style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}>
        {galleryImages.length > 0 && (
          <Image
            source={{ uri: galleryImages[0] }}
            className="h-[260px] w-full"
            resizeMode="cover"
          />
        )}
        <View className="px-5 pb-5 pt-4">
          <Text
            className="text-2xl font-bold"
            style={{ color: colors.textPrimary }}
            numberOfLines={2}
          >
            {recipe.title}
          </Text>
          {tags.length ? (
            <View className="mt-3 flex-row flex-wrap gap-2">
              {tags.map((tag) => (
                <View
                  key={`${recipe.id}-tag-${tag}`}
                  className="rounded-lg px-3 py-1"
                  style={{ backgroundColor: colors.bg }}
                >
                  <Text className="text-xs font-semibold" style={{ color: colors.textPrimary }}>
                    {tag}
                  </Text>
                </View>
              ))}
            </View>
          ) : null}
          {metaRow}
        </View>
      </View>

      {ingredientsSection}

      <NutritionSection
        nutrition={{
          calories: recipe.calories,
          carbohydrates: recipe.carbohydrates,
          fat: recipe.fat,
          protein: recipe.protein,
          sugar: recipe.sugar,
          sodium: recipe.sodium,
          saturatedFat: recipe.saturatedFat,
          servings: recipe.servings,
        }}
      />

      <View className="rounded-2xl p-4" style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}>
        <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
          Cooking Steps
        </Text>
        <View className="mt-4 gap-3">
          {steps.length ? (
            steps.map((step, index) => (
              <View
                key={`${recipe.id}-step-${index}`}
                className="flex-row items-start gap-3 rounded-2xl px-4 py-3"
                style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}
              >
                <View className="h-8 w-8 items-center justify-center rounded-full" style={{ backgroundColor: colors.accent }}>
                  <Text className="text-sm font-bold" style={{ color: colors.bg }}>
                    {index + 1}
                  </Text>
                </View>
                <Text className="flex-1 text-sm leading-6" style={{ color: colors.textSecondary }}>
                  {step}
                </Text>
              </View>
            ))
          ) : (
            <Text className="text-sm" style={{ color: colors.textSecondary }}>No cooking steps yet.</Text>
          )}
        </View>
      </View>

      {showStartCookingButton && onStartCooking ? (
        <TouchableOpacity
          onPress={onStartCooking}
          activeOpacity={0.9}
          className="h-12 flex-row items-center justify-center gap-2 rounded-full"
          style={{ backgroundColor: colors.accent }}
        >
          <Ionicons name="restaurant-outline" size={18} color={colors.bg} />
          <Text className="text-base font-semibold" style={{ color: colors.bg }}>
            Start Cooking
          </Text>
        </TouchableOpacity>
      ) : null}
    </View>
  );

  const detailView = (
    <View className="gap-4">
      {/* Recipe Image - only show if images exist */}
      {galleryImages.length > 0 && (
        <View
          className="overflow-hidden rounded-[28px]"
          onLayout={handleMediaLayout}
        >
          <View>
            <ScrollView
              horizontal
              pagingEnabled
              showsHorizontalScrollIndicator={false}
              onMomentumScrollEnd={handleMediaScroll}
            >
              {galleryImages.map((imageUrl, index) => (
                <View
                  key={`${recipe.id}-image-${index}`}
                  style={{ width: mediaWidth || "100%" }}
                  className="h-[280px] overflow-hidden"
                >
                  <Image
                    source={{ uri: imageUrl }}
                    className="h-full w-full"
                    resizeMode="cover"
                  />
                </View>
              ))}
            </ScrollView>
            {galleryImages.length > 1 ? (
              <View className="absolute bottom-3 left-0 right-0 flex-row items-center justify-center gap-2">
                {galleryImages.map((_, index) => (
                  <View
                    key={`${recipe.id}-indicator-${index}`}
                    className="h-2 rounded-full"
                    style={{
                      width: index === activeMediaIndex ? 20 : 8,
                      backgroundColor: index === activeMediaIndex ? colors.textPrimary : `${colors.textPrimary}80`
                    }}
                  />
                ))}
              </View>
            ) : null}
          </View>
        </View>
      )}

      {/* Title and Description */}
      <View>
        <Text
          className="text-2xl font-bold"
          style={{ color: colors.textPrimary }}
          numberOfLines={2}
        >
          {recipe.title}
        </Text>
        {/* Last Updated */}
        {recipe.updatedAt && (
          <View className="mt-3 px-1">
            <Text className="text-xs" style={{ color: colors.textMuted }}>
              Updated:{" "}
              {new Date(recipe.updatedAt).toLocaleDateString(undefined, {
                year: "numeric",
                month: "short",
                day: "numeric",
              })}{" "}
              {new Date(recipe.updatedAt).toLocaleTimeString(undefined, {
                hour: "2-digit",
                minute: "2-digit",
              })}
            </Text>
          </View>
        )}
        {recipe.description ? (
          <Text className="mt-2 text-sm leading-6" style={{ color: colors.textSecondary }}>
            {recipe.description}
          </Text>
        ) : null}

        {/* Tags */}
        {tags.length ? (
          <View className="mt-3 flex-row flex-wrap gap-2">
            {tags.map((tag) => (
              <View
                key={`${recipe.id}-tag-${tag}`}
                className="rounded-full px-3 py-1.5"
                style={{ backgroundColor: colors.card }}
              >
                <Text className="text-xs font-medium" style={{ color: colors.textPrimary }}>{tag}</Text>
              </View>
            ))}
          </View>
        ) : null}
      </View>

      {/* Ingredients */}
      {ingredientsSection}

      {/* Collapsible Cooking Steps */}
      <View className="mt-4 rounded-2xl overflow-hidden" style={{ backgroundColor: colors.card }}>
        <TouchableOpacity
          onPress={() => setStepsExpanded(!stepsExpanded)}
          activeOpacity={0.8}
          className="flex-row items-center justify-between px-4 py-3"
        >
          <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
            Cooking Steps ({steps.length})
          </Text>
          {stepsExpanded ? (
            <ChevronUp size={20} color={colors.accent} />
          ) : (
            <ChevronDown size={20} color={colors.accent} />
          )}
        </TouchableOpacity>

        {stepsExpanded && (
          <View className="px-4 pb-4 gap-3">
            {steps.length ? (
              steps.map((step, index) => (
                <View
                  key={`${recipe.id}-step-${index}`}
                  className="flex-row items-start gap-3"
                >
                  <View className="h-7 w-7 items-center justify-center rounded-full" style={{ backgroundColor: colors.accent }}>
                    <Text className="text-xs font-bold" style={{ color: colors.bg }}>
                      {index + 1}
                    </Text>
                  </View>
                  <Text className="flex-1 text-sm leading-6 pt-0.5" style={{ color: colors.textSecondary }}>
                    {step}
                  </Text>
                </View>
              ))
            ) : (
              <Text className="text-sm" style={{ color: colors.textSecondary }}>
                No cooking steps yet.
              </Text>
            )}
          </View>
        )}
      </View>

      {/* Divider */}
      <View className="h-px" style={{ backgroundColor: colors.border }} />

      {/* Nutrition Section */}
      <NutritionSection
        nutrition={{
          calories: recipe.calories,
          carbohydrates: recipe.carbohydrates,
          fat: recipe.fat,
          protein: recipe.protein,
          sugar: recipe.sugar,
          sodium: recipe.sodium,
          saturatedFat: recipe.saturatedFat,
          servings: recipe.servings,
        }}
      />
    </View>
  );

  const content =
    variant === "modal"
      ? modalLayout
      : isRecommendedLayout
        ? recommendedView
        : detailView;

  if (scrollable) {
    return (
      <ScrollView
        className="flex-1"
        showsVerticalScrollIndicator={false}
        contentContainerClassName="pb-6"
      >
        {content}
      </ScrollView>
    );
  }

  return content;
}
