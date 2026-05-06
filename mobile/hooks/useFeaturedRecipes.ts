import { useAuthQuery } from "./useApi";
import type { ApiResponse } from "@/types/api";

/**
 * Featured recipe for the homepage carousel.
 */
export interface FeaturedRecipe {
  id: string;
  title: string;
  description: string | null;
  coverImageUrl: string | null;
  totalTimeMinutes: number | null;
  difficulty: "None" | "Easy" | "Medium" | "Hard";
  servings: number | null;
  likesCount: number;
  savedCount: number;
  authorId: string | null;
  authorNickname: string | null;
  authorAvatarUrl: string | null;
  tags: string[];
}

export interface UseFeaturedRecipesResult {
  /** Featured recipes for the carousel */
  recipes: FeaturedRecipe[];
  /** Whether the recipes are loading */
  isLoading: boolean;
  /** Error if fetching failed */
  error: Error | null;
  /** Refetch the recipes */
  refetch: () => void;
}

/**
 * Hook to fetch featured community recipes for the homepage carousel.
 * Fetches from /api/recipes/featured endpoint.
 *
 * @param count - Number of recipes to fetch (default 10)
 */
export function useFeaturedRecipes(count: number = 10): UseFeaturedRecipesResult {
  const {
    data: response,
    isLoading,
    error,
    refetch,
  } = useAuthQuery<ApiResponse<FeaturedRecipe[]>>(
    ["recipes-featured", count],
    `/api/recipes/featured?count=${count}`,
    {
      staleTime: 5 * 60 * 1000, // 5 minutes
      refetchOnWindowFocus: false,
    }
  );

  return {
    recipes: response?.data ?? [],
    isLoading,
    error: error ?? null,
    refetch,
  };
}

export default useFeaturedRecipes;
