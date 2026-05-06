import { useAuthQuery, fetcher } from "./useApi";
import type { ApiResponse } from "@/types/api";
import { useAuth } from "@clerk/clerk-expo";
import { useInfiniteQuery } from "@tanstack/react-query";

export interface RecommendedRecipeDto {
  recipeId: string;
  title: string;
  description: string | null;
  coverImageUrl: string | null;
  totalTimeMinutes: number | null;
  difficulty: "None" | "Easy" | "Medium" | "Hard";
  servings: number | null;
  preferenceMatchCount: number;
  likesCount: number;
  savedCount: number;
  savedByMe: boolean;
  tags: string[];
  type: "User" | "System" | "Model";
  authorId: string | null;
  authorNickname: string | null;
  authorAvatarUrl: string | null;
}

export interface RecommendedRecipesResponse {
  recipes: RecommendedRecipeDto[];
  totalCount: number;
  message: string | null;
}

/**
 * Hook for fetching personalized recipe recommendations (simple, no pagination).
 */
export function useRecommendedRecipes(
  limit = 20,
  offset = 0,
  searchQuery?: string,
  seed?: string
) {
  const trimmedSearch = searchQuery?.trim();
  const searchParam = trimmedSearch
    ? `&search=${encodeURIComponent(trimmedSearch)}`
    : "";
  const seedParam = seed ? `&seed=${encodeURIComponent(seed)}` : "";
  const query = useAuthQuery<ApiResponse<RecommendedRecipesResponse>>(
    ["recommended-recipes", limit, offset, trimmedSearch ?? "", seed ?? ""],
    `/api/recipes/recommended?limit=${limit}&offset=${offset}${searchParam}${seedParam}`
  );

  return {
    ...query,
    recipes: query.data?.data?.recipes ?? [],
    totalCount: query.data?.data?.totalCount ?? 0,
    message: query.data?.data?.message ?? query.data?.message,
  };
}

/**
 * Hook for fetching recommended recipes with infinite scroll pagination.
 * Uses offset-based pagination.
 */
export function useInfiniteRecommendedRecipes(
  pageSize = 20,
  searchQuery?: string,
  seed?: string
) {
  const { getToken } = useAuth();
  const trimmedSearch = searchQuery?.trim();
  const searchParam = trimmedSearch
    ? `&search=${encodeURIComponent(trimmedSearch)}`
    : "";
  const seedParam = seed ? `&seed=${encodeURIComponent(seed)}` : "";

  const query = useInfiniteQuery<ApiResponse<RecommendedRecipesResponse>, Error>({
    queryKey: ["recommended-recipes-infinite", pageSize, trimmedSearch ?? "", seed ?? ""],
    queryFn: async ({ pageParam = 0 }) => {
      const url = `/api/recipes/recommended?limit=${pageSize}&offset=${pageParam}${searchParam}${seedParam}`;
      return fetcher<ApiResponse<RecommendedRecipesResponse>>(url, "GET", getToken);
    },
    initialPageParam: 0,
    getNextPageParam: (lastPage, allPages) => {
      const loadedCount = allPages.reduce(
        (sum, page) => sum + (page?.data?.recipes?.length ?? 0), 
        0
      );
      const totalCount = lastPage?.data?.totalCount ?? 0;
      
      // If we've loaded all items, return undefined to indicate no more pages
      if (loadedCount >= totalCount) {
        return undefined;
      }
      
      // Return next offset
      return loadedCount;
    },
  });

  // Flatten all pages into a single array of recipes
  const recipes = query.data?.pages.flatMap(
    (page) => page?.data?.recipes ?? []
  ) ?? [];

  // Get total count from first page
  const totalCount = query.data?.pages[0]?.data?.totalCount ?? 0;
  const message = query.data?.pages[0]?.data?.message ?? null;

  return {
    ...query,
    recipes,
    totalCount,
    message,
  };
}


