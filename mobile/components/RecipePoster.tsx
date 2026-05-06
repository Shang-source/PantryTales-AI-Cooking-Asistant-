import React, { forwardRef } from "react";
import { View, Text, Image, StyleSheet } from "react-native";
import Icon from "react-native-vector-icons/Feather";

/**
 * Data structure for recipe poster rendering.
 * All fields are optional for fault tolerance.
 */
export interface RecipePosterData {
  title?: string;
  description?: string;
  imageUrl?: string | null;
  ingredients?: {
    name: string;
    amount?: number | null;
    unit?: string | null;
  }[];
  steps?: string[];
  totalTimeMinutes?: number | null;
  servings?: number | null;
  difficulty?: string | null;
  tags?: string[];
  // Nutrition fields
  calories?: number | null;
  carbohydrates?: number | null;
  fat?: number | null;
  protein?: number | null;
  sugar?: number | null;
  sodium?: number | null;
  saturatedFat?: number | null;
  // Display options
  showDescription?: boolean;
}

interface RecipePosterProps {
  recipe: RecipePosterData;
}

const formatAmount = (amount?: number | null, unit?: string | null): string => {
  if (amount == null && !unit) return "";
  if (amount == null) return unit ?? "";
  return unit ? `${amount} ${unit}` : `${amount}`;
};

const formatTime = (minutes?: number | null): string => {
  if (!minutes) return "";
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
};

/**
 * RecipePoster - A visually appealing poster component for recipe sharing.
 * Uses forwardRef to allow capturing via react-native-view-shot.
 * Displays ALL ingredients, steps, and nutrition. Title/description may be truncated for layout.
 */
export const RecipePoster = forwardRef<View, RecipePosterProps>(
  ({ recipe }, ref) => {
    const {
      title,
      description,
      imageUrl,
      ingredients = [],
      steps = [],
      totalTimeMinutes,
      servings,
      difficulty,
      tags = [],
      calories,
      carbohydrates,
      fat,
      protein,
      sugar,
      sodium,
      saturatedFat,
      showDescription = true,
    } = recipe;

    const hasNutrition =
      calories != null ||
      carbohydrates != null ||
      fat != null ||
      protein != null ||
      sugar != null ||
      sodium != null ||
      saturatedFat != null;

    return (
      <View ref={ref} style={styles.container} collapsable={false}>
        {/* Header Image */}
        <View style={styles.imageContainer}>
          {imageUrl ? (
            <Image
              source={{ uri: imageUrl }}
              style={styles.image}
              resizeMode="cover"
            />
          ) : (
            <View style={styles.placeholderImage}>
              <Icon name="image" size={48} color="#9CA3AF" />
            </View>
          )}
        </View>

        {/* Content Section */}
        <View style={styles.content}>
          {/* Title */}
          <Text style={styles.title} numberOfLines={2}>
            {title || "Untitled Recipe"}
          </Text>

          {/* Description */}
          {showDescription && description ? (
            <Text style={styles.description} numberOfLines={3}>
              {description}
            </Text>
          ) : null}

          {/* Meta Info Row */}
          <View style={styles.metaRow}>
            {totalTimeMinutes ? (
              <View style={styles.metaItem}>
                <Icon name="clock" size={14} color="#6B7280" />
                <Text style={styles.metaText}>
                  {formatTime(totalTimeMinutes)}
                </Text>
              </View>
            ) : null}
            {servings ? (
              <View style={styles.metaItem}>
                <Icon name="users" size={14} color="#6B7280" />
                <Text style={styles.metaText}>{servings} servings</Text>
              </View>
            ) : null}
            {difficulty && difficulty !== "None" ? (
              <View style={styles.metaItem}>
                <Icon name="bar-chart-2" size={14} color="#6B7280" />
                <Text style={styles.metaText}>{difficulty}</Text>
              </View>
            ) : null}
          </View>

          {/* Tags */}
          {tags.length > 0 ? (
            <View style={styles.tagsContainer}>
              {tags.slice(0, 5).map((tag, index) => (
                <View key={index} style={styles.tag}>
                  <Text style={styles.tagText}>{tag}</Text>
                </View>
              ))}
              {tags.length > 5 && (
                <Text style={styles.moreText}>+{tags.length - 5} more</Text>
              )}
            </View>
          ) : null}

          {/* Divider */}
          <View style={styles.divider} />

          {/* Ingredients Section - Show ALL */}
          {ingredients.length > 0 ? (
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>
                <Icon name="shopping-cart" size={14} color="#5C7770" />{" "}
                Ingredients
              </Text>
              {ingredients.map((ing, index) => (
                <View key={index} style={styles.ingredientRow}>
                  <View style={styles.bullet} />
                  <Text style={styles.ingredientText}>
                    {ing.name}
                    {formatAmount(ing.amount, ing.unit) ? (
                      <Text style={styles.ingredientAmount}>
                        {" "}
                        ({formatAmount(ing.amount, ing.unit)})
                      </Text>
                    ) : null}
                  </Text>
                </View>
              ))}
            </View>
          ) : null}

          {/* Steps Section - Show ALL */}
          {steps.length > 0 ? (
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>
                <Icon name="list" size={14} color="#5C7770" /> Steps
              </Text>
              {steps.map((step, index) => (
                <View key={index} style={styles.stepRow}>
                  <View style={styles.stepNumber}>
                    <Text style={styles.stepNumberText}>{index + 1}</Text>
                  </View>
                  <Text style={styles.stepText}>{step}</Text>
                </View>
              ))}
            </View>
          ) : null}

          {/* Nutrition Section - Expanded format like detail page */}
          {hasNutrition ? (
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>
                <Icon name="activity" size={14} color="#5C7770" /> Nutrition
              </Text>

              {/* Main summary row with calorie circle and macros */}
              <View style={styles.nutritionSummary}>
                {/* Calorie circle */}
                {calories != null && (
                  <View style={styles.calorieCircle}>
                    <Text style={styles.calorieValue}>{Math.round(calories)}</Text>
                    <Text style={styles.calorieLabel}>cals</Text>
                  </View>
                )}

                {/* Macro items */}
                <View style={styles.macrosContainer}>
                  {carbohydrates != null && (
                    <View style={styles.macroItem}>
                      <Text style={styles.macroValue}>{Math.round(carbohydrates)}g</Text>
                      <Text style={styles.macroLabel}>Carbs</Text>
                    </View>
                  )}
                  {fat != null && (
                    <View style={styles.macroItem}>
                      <Text style={styles.macroValue}>{Math.round(fat)}g</Text>
                      <Text style={styles.macroLabel}>Fat</Text>
                    </View>
                  )}
                  {protein != null && (
                    <View style={styles.macroItem}>
                      <Text style={styles.macroValue}>{Math.round(protein)}g</Text>
                      <Text style={styles.macroLabel}>Protein</Text>
                    </View>
                  )}
                </View>
              </View>

              {/* Detailed nutrition rows */}
              {(sugar != null || sodium != null || saturatedFat != null) && (
                <View style={styles.nutritionDetails}>
                  {sugar != null && (
                    <View style={styles.nutritionRow}>
                      <Text style={styles.nutritionRowLabel}>Sugar</Text>
                      <Text style={styles.nutritionRowValue}>{Math.round(sugar)}g</Text>
                    </View>
                  )}
                  {saturatedFat != null && (
                    <View style={styles.nutritionRow}>
                      <Text style={styles.nutritionRowLabel}>Saturated Fat</Text>
                      <Text style={styles.nutritionRowValue}>{Math.round(saturatedFat)}g</Text>
                    </View>
                  )}
                  {sodium != null && (
                    <View style={styles.nutritionRow}>
                      <Text style={styles.nutritionRowLabel}>Sodium</Text>
                      <Text style={styles.nutritionRowValue}>{Math.round(sodium)}mg</Text>
                    </View>
                  )}
                </View>
              )}
            </View>
          ) : null}

          {/* Footer Branding */}
          <View style={styles.footer}>
            <Image
              source={require("@/assets/images/logo.jpg")}
              style={styles.logoImage}
            />
            <Text style={styles.brandText}>PantryTales</Text>
          </View>
        </View>
      </View>
    );
  },
);

RecipePoster.displayName = "RecipePoster";

const styles = StyleSheet.create({
  container: {
    backgroundColor: "#FFFFFF",
    borderRadius: 16,
    overflow: "hidden",
    width: 350,
  },
  imageContainer: {
    height: 180,
    backgroundColor: "#F3F4F6",
  },
  image: {
    width: "100%",
    height: "100%",
  },
  placeholderImage: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    backgroundColor: "#F3F4F6",
  },
  content: {
    padding: 16,
  },
  title: {
    fontSize: 22,
    fontWeight: "bold",
    color: "#1F2937",
    marginBottom: 8,
  },
  description: {
    fontSize: 14,
    color: "#6B7280",
    marginBottom: 12,
    lineHeight: 20,
  },
  metaRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 16,
    marginBottom: 12,
  },
  metaItem: {
    flexDirection: "row",
    alignItems: "center",
    gap: 4,
  },
  metaText: {
    fontSize: 13,
    color: "#6B7280",
  },
  tagsContainer: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginBottom: 12,
  },
  tag: {
    backgroundColor: "#E8F0EE",
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 12,
  },
  tagText: {
    fontSize: 12,
    color: "#5C7770",
  },
  moreText: {
    fontSize: 12,
    color: "#9CA3AF",
    alignSelf: "center",
  },
  divider: {
    height: 1,
    backgroundColor: "#E5E7EB",
    marginVertical: 12,
  },
  section: {
    marginBottom: 16,
  },
  sectionTitle: {
    fontSize: 15,
    fontWeight: "600",
    color: "#5C7770",
    marginBottom: 10,
  },
  ingredientRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    marginBottom: 6,
  },
  bullet: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: "#D4A5A5",
    marginTop: 6,
    marginRight: 8,
  },
  ingredientText: {
    flex: 1,
    fontSize: 13,
    color: "#374151",
    lineHeight: 18,
  },
  ingredientAmount: {
    color: "#9CA3AF",
  },
  stepRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    marginBottom: 10,
  },
  stepNumber: {
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: "#5C7770",
    justifyContent: "center",
    alignItems: "center",
    marginRight: 10,
  },
  stepNumberText: {
    fontSize: 12,
    fontWeight: "bold",
    color: "#FFFFFF",
  },
  stepText: {
    flex: 1,
    fontSize: 13,
    color: "#374151",
    lineHeight: 18,
  },
  nutritionSummary: {
    flexDirection: "row",
    alignItems: "center",
    marginBottom: 12,
  },
  calorieCircle: {
    width: 70,
    height: 70,
    borderRadius: 35,
    backgroundColor: "#FEF3C7",
    borderWidth: 4,
    borderColor: "#F5A623",
    justifyContent: "center",
    alignItems: "center",
    marginRight: 12,
  },
  calorieValue: {
    fontSize: 18,
    fontWeight: "bold",
    color: "#1F2937",
  },
  calorieLabel: {
    fontSize: 10,
    color: "#6B7280",
  },
  macrosContainer: {
    flex: 1,
    flexDirection: "row",
    justifyContent: "space-around",
  },
  macroItem: {
    alignItems: "center",
    flex: 1,
  },
  macroValue: {
    fontSize: 16,
    fontWeight: "bold",
    color: "#1F2937",
  },
  macroLabel: {
    fontSize: 11,
    color: "#6B7280",
    marginTop: 2,
  },
  nutritionDetails: {
    backgroundColor: "#F9FAFB",
    borderRadius: 8,
    padding: 10,
  },
  nutritionRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    paddingVertical: 4,
  },
  nutritionRowLabel: {
    fontSize: 13,
    color: "#374151",
  },
  nutritionRowValue: {
    fontSize: 13,
    fontWeight: "600",
    color: "#5C7770",
  },
  footer: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    marginTop: 8,
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: "#F3F4F6",
    gap: 8,
  },
  logoImage: {
    width: 28,
    height: 28,
    borderRadius: 6,
  },
  brandText: {
    fontSize: 14,
    color: "#9CA3AF",
    fontWeight: "500",
  },
});

/**
 * Helper function to convert RecipeDetailDto to RecipePosterData.
 */
export function recipeDetailToPosterData(recipe: {
  title?: string;
  description?: string;
  imageUrls?: string[] | null;
  ingredients?: {
    name: string;
    amount?: number | null;
    unit?: string | null;
  }[];
  steps?: string[];
  totalTimeMinutes?: number | null;
  servings?: number | null;
  difficulty?: string | null;
  tags?: string[];
  calories?: number | null;
  carbohydrates?: number | null;
  fat?: number | null;
  protein?: number | null;
  sugar?: number | null;
  sodium?: number | null;
  saturatedFat?: number | null;
}): RecipePosterData {
  return {
    title: recipe.title,
    description: recipe.description,
    imageUrl: recipe.imageUrls?.[0] ?? null,
    ingredients: recipe.ingredients?.map((ing) => ({
      name: ing.name,
      amount: ing.amount,
      unit: ing.unit,
    })),
    steps: recipe.steps,
    totalTimeMinutes: recipe.totalTimeMinutes,
    servings: recipe.servings,
    difficulty: recipe.difficulty,
    tags: recipe.tags,
    calories: recipe.calories,
    carbohydrates: recipe.carbohydrates,
    fat: recipe.fat,
    protein: recipe.protein,
    sugar: recipe.sugar,
    sodium: recipe.sodium,
    saturatedFat: recipe.saturatedFat,
  };
}
