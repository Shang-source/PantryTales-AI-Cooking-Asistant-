import { useAuthQuery } from "./useApi";
import { ApiResponse } from "@/types/api";
import { InventoryListResponseDto } from "@/types/Inventory";

/**
 * Fetch all inventory items for comparison with recipe ingredients.
 * Returns a simplified list for checking ingredient availability.
 */
export function useInventoryItems() {
  return useAuthQuery<ApiResponse<InventoryListResponseDto>>(
    ["inventory-all"],
    "/api/inventory?pageSize=100",
  );
}

/**
 * Basic ingredients/seasonings (调料) that are commonly available in most kitchens.
 * These are assumed to be available if not explicitly tracked.
 * Includes both English and Chinese names for common seasonings.
 */
const BASIC_INGREDIENTS = [
  // Water
  "water", "水",
  // Salt
  "salt", "盐", "食盐",
  // Pepper
  "pepper", "black pepper", "white pepper", "胡椒", "黑胡椒", "白胡椒", "胡椒粉",
  // Oil
  "oil", "vegetable oil", "olive oil", "cooking oil", "sesame oil",
  "油", "食用油", "植物油", "橄榄油", "芝麻油", "香油", "花生油", "菜籽油",
  // Sugar
  "sugar", "white sugar", "brown sugar", "糖", "白糖", "红糖", "冰糖",
  // Flour
  "flour", "all-purpose flour", "面粉",
  // Soy sauce
  "soy sauce", "light soy sauce", "dark soy sauce",
  "酱油", "生抽", "老抽", "蚝油", "耗油",
  // Vinegar
  "vinegar", "rice vinegar", "white vinegar", "balsamic vinegar",
  "醋", "白醋", "米醋", "陈醋", "香醋",
  // Cooking wine
  "cooking wine", "rice wine", "料酒", "黄酒",
  // MSG and chicken powder
  "msg", "monosodium glutamate", "chicken powder", "chicken bouillon",
  "味精", "鸡精", "鸡粉",
  // Spices
  "garlic", "ginger", "green onion", "scallion", "shallot",
  "蒜", "大蒜", "姜", "生姜", "葱", "小葱", "香葱", "葱花",
  // Other common seasonings
  "five spice", "star anise", "cinnamon", "bay leaf",
  "五香粉", "八角", "桂皮", "香叶", "花椒", "辣椒", "干辣椒",
  "starch", "cornstarch", "淀粉", "生粉", "玉米淀粉",
  "baking powder", "baking soda", "泡打粉", "小苏打",
];

/**
 * Normalize ingredient name for comparison.
 * Removes common modifiers, extra whitespace, and normalizes Unicode.
 */
function normalizeIngredientName(name: string): string {
  return name
    .normalize("NFC") // Normalize Unicode to composed form
    .toLowerCase()
    .trim()
    .replace(/\s+/g, " ") // Collapse multiple spaces
    .replace(
      /^(fresh|dried|frozen|canned|chopped|minced|sliced|diced|ground)\s+/gi,
      "",
    )
    .replace(/,.*$/, "") // Remove anything after comma (e.g., "chicken, boneless")
    .trim();
}

/**
 * Check if an ingredient is a basic ingredient that's commonly available.
 */
export function isBasicIngredient(ingredientName: string): boolean {
  const normalized = normalizeIngredientName(ingredientName);
  return BASIC_INGREDIENTS.some((basic) => {
    const normalizedBasic = basic.normalize("NFC").toLowerCase();
    return (
      normalized === normalizedBasic ||
      normalized.includes(normalizedBasic) ||
      normalizedBasic.includes(normalized)
    );
  });
}

/**
 * Check if an ingredient is available in inventory.
 * Compares ingredient name (case-insensitive) and returns the available amount.
 */
export function findInventoryMatch(
  ingredientName: string,
  inventoryItems: { name: string; amount: number; unit: string }[],
): { found: boolean; availableAmount: number; unit: string } | null {
  const normalized = normalizeIngredientName(ingredientName);

  // Check for basic ingredients first - assume always available with unlimited amount
  if (isBasicIngredient(normalized)) {
    return {
      found: true,
      availableAmount: 9999,
      unit: "available",
    };
  }

  // Get individual words for matching
  const ingredientWords = normalized.split(" ").filter((w) => w.length > 2);

  const match = inventoryItems.find((item) => {
    const itemNormalized = normalizeIngredientName(item.name);
    const itemWords = itemNormalized.split(" ").filter((w) => w.length > 2);

    // Exact match
    if (itemNormalized === normalized) return true;

    // Containment match (either direction)
    if (
      itemNormalized.includes(normalized) ||
      normalized.includes(itemNormalized)
    ) {
      return true;
    }

    // Word overlap match - at least one significant word matches
    const hasWordMatch = ingredientWords.some((word) =>
      itemWords.some(
        (itemWord) =>
          itemWord === word ||
          itemWord.includes(word) ||
          word.includes(itemWord),
      ),
    );

    return hasWordMatch;
  });

  if (match) {
    return {
      found: true,
      availableAmount: match.amount,
      unit: match.unit,
    };
  }

  return null;
}

/**
 * Calculate missing ingredients for a recipe based on current inventory.
 * Returns the list of missing ingredient names and count.
 */
export function calculateMissingIngredients(
  recipeIngredients: { name: string; isOptional?: boolean }[],
  inventoryItems: { name: string; amount: number; unit: string }[],
): { missingIngredients: string[]; missingCount: number } {
  const missing: string[] = [];

  for (const ingredient of recipeIngredients) {
    // Skip optional ingredients
    if (ingredient.isOptional) continue;

    const match = findInventoryMatch(ingredient.name, inventoryItems);
    if (!match) {
      missing.push(ingredient.name);
    }
  }

  return {
    missingIngredients: missing,
    missingCount: missing.length,
  };
}
