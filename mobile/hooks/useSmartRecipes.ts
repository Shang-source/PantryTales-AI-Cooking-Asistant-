import { useAuthQuery, useAuthMutation } from "./useApi";
import type { ApiResponse } from "@/types/api";

export interface SmartRecipeIngredientDto {
  name: string;
  amount: number | null;
  unit: string | null;
  isOptional: boolean;
  category: string | null;
}

export interface SmartRecipeDto {
  id: string;
  recipeId: string;
  title: string;
  description: string | null;
  coverImageUrl: string | null;
  totalTimeMinutes: number | null;
  difficulty: "None" | "Easy" | "Medium" | "Hard";
  servings: number | null;
  missingIngredientsCount: number;
  missingIngredients: string[];
  matchScore: number | null;
  generatedDate: string;
  createdAt: string;
  ingredients: SmartRecipeIngredientDto[];
}

export interface GenerateRecipesParams {
  servings?: number;
}

/**
 * Hook for fetching and refreshing smart recipe suggestions.
 * @param options.autoFetch - If false, won't auto-fetch on mount (default: true)
 * @param options.allowStale - If true, returns recipes from previous days (default: false)
 */
export function useSmartRecipes(options?: { autoFetch?: boolean; allowStale?: boolean }) {
  const autoFetch = options?.autoFetch ?? true;
  const allowStale = options?.allowStale ?? false;

  const query = useAuthQuery<ApiResponse<SmartRecipeDto[]>>(
    ["smart-recipes", allowStale ? "stale" : "fresh"],
    `/api/smart-recipes?allowStale=${allowStale}`,
    { enabled: autoFetch },
  );

  const generateMutation = useAuthMutation<
    ApiResponse<SmartRecipeDto[]>,
    GenerateRecipesParams | undefined
  >(
    (params) =>
      `/api/smart-recipes/refresh${params?.servings ? `?servings=${params.servings}` : ""}`,
    "POST",
    {
      onSuccess: () => {
        query.refetch();
      },
    },
  );

  return {
    ...query,
    recipes: query.data?.data ?? [],
    message: query.data?.message,
    /** Generate or regenerate recipes with optional servings */
    generate: (params?: GenerateRecipesParams) => generateMutation.mutate(params),
    isGenerating: generateMutation.isPending,
    /** Alias for generate (for backwards compatibility) */
    refresh: (params?: GenerateRecipesParams) => generateMutation.mutate(params),
    isRefreshing: generateMutation.isPending,
  };
}
