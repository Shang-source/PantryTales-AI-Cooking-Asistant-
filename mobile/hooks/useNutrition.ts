import { useAuthMutation } from "./useApi";
import { ApiResponse } from "@/types/api";

/**
 * Types for nutrition calculation
 */
export interface NutritionIngredient {
  name: string;
  quantity: number;
  unit: string;
}

export interface CalculateNutritionRequest {
  ingredients: NutritionIngredient[];
  servings: number;
}

export interface NutritionData {
  calories: number;
  carbohydrates: number;
  fat: number;
  protein: number;
  sugar: number;
  sodium: number;
  saturatedFat: number;
  unsaturatedFat: number;
  transFat: number;
  fiber: number;
  cholesterol: number;
  servings: number;
}

export interface NutritionResponse {
  success: boolean;
  nutrition: NutritionData | null;
  warnings: string[];
  errorMessage?: string;
}

/**
 * Hook for calculating nutrition from ingredients.
 */
export function useCalculateNutrition() {
  return useAuthMutation<
    ApiResponse<NutritionResponse>,
    CalculateNutritionRequest
  >("/api/nutrition/calculate", "POST");
}
