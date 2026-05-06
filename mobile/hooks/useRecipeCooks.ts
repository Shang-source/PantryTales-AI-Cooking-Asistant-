import { useQuery, useMutation, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useAuth } from "@clerk/clerk-expo";

import { fetcher } from "@/hooks/useApi";
import type { ApiResponse } from "@/types/api";
import type { MyCookedRecipeCardDto, MeCooksCountDto, RecipeCookResponse } from "@/types/recipes";

// Query keys for cooking history
export const cookedRecipeIdsKey = ["cooked-recipe-ids"] as const;
export const meCooksCountKey = ["me-cooks-count"] as const;
export const meCooksKeyPrefix = ["me-cooks"] as const;
export const meCooksKey = (page: number, pageSize: number) =>
  ["me-cooks", `page:${page}`, `pageSize:${pageSize}`] as const;

export const DEFAULT_ME_COOKS_PAGE_SIZE = 20;

/**
 * Hook to get cooking history count
 */
export function useCookingHistoryCount() {
  const { getToken } = useAuth();

  return useQuery<ApiResponse<MeCooksCountDto>>({
    queryKey: meCooksCountKey,
    queryFn: () => fetcher<ApiResponse<MeCooksCountDto>>("/api/me/cooks/count", "GET", getToken),
    staleTime: 0,
    gcTime: 300_000,
  });
}

/**
 * Hook to get cooking history list (with cook count per recipe, sorted by cook count desc)
 */
export function useCookingHistory(
  page: number = 1,
  pageSize: number = DEFAULT_ME_COOKS_PAGE_SIZE,
  searchQuery?: string,
) {
  const { getToken } = useAuth();
  const search = searchQuery?.trim();

  return useQuery<ApiResponse<MyCookedRecipeCardDto[]>>({
    queryKey: [...meCooksKey(page, pageSize), `search:${search ?? ""}`] as const,
    queryFn: () =>
      fetcher<ApiResponse<MyCookedRecipeCardDto[]>>(
        `/api/me/cooks?page=${page}&pageSize=${pageSize}${
          search ? `&search=${encodeURIComponent(search)}` : ""
        }`,
        "GET",
        getToken
      ),
    staleTime: 0,
    gcTime: 300_000,
  });
}

/**
 * Hook to record a recipe cook completion
 */
export function useRecipeCookComplete() {
  const { getToken } = useAuth();
  const queryClient = useQueryClient();

  return useMutation<ApiResponse<RecipeCookResponse>, Error, string>({
    mutationFn: (recipeId: string) =>
      fetcher<ApiResponse<RecipeCookResponse>>(
        `/api/recipes/${recipeId}/cook/complete`,
        "POST",
        getToken,
        {}
      ),
    onSuccess: (response) => {
      if (response.data) {
        // Only increment count if this is the first time cooking this recipe
        if (response.data.cookCount === 1) {
          updateMeCooksCountCache(queryClient, 1);
        }
        // Invalidate the list to refetch with updated cook counts
        queryClient.invalidateQueries({ queryKey: meCooksKeyPrefix });
      }
    },
  });
}

/**
 * Hook to delete a cooking history entry
 */
export function useDeleteCookingHistoryEntry() {
  const { getToken } = useAuth();
  const queryClient = useQueryClient();

  return useMutation<ApiResponse<void>, Error, string>({
    mutationFn: (cookId: string) =>
      fetcher<ApiResponse<void>>(`/api/me/cooks/${cookId}`, "DELETE", getToken),
    onSuccess: () => {
      // Update count cache
      updateMeCooksCountCache(queryClient, -1);
      // Invalidate the list to refetch
      queryClient.invalidateQueries({ queryKey: meCooksKeyPrefix });
    },
  });
}

/**
 * Hook to clear all cooking history
 */
export function useClearCookingHistory() {
  const { getToken } = useAuth();
  const queryClient = useQueryClient();

  return useMutation<ApiResponse<void>, Error, void>({
    mutationFn: () => fetcher<ApiResponse<void>>("/api/me/cooks", "DELETE", getToken),
    onSuccess: () => {
      // Reset count to 0
      queryClient.setQueryData<ApiResponse<MeCooksCountDto>>(meCooksCountKey, (prev) => {
        if (prev) {
          return { ...prev, data: { count: 0 } };
        }
        return { code: 0, message: "Ok", data: { count: 0 } };
      });
      // Invalidate the list
      queryClient.invalidateQueries({ queryKey: meCooksKeyPrefix });
    },
  });
}

// Helper function to update cook count cache
function updateMeCooksCountCache(queryClient: QueryClient, delta: number) {
  if (delta === 0) return;
  queryClient.setQueryData<ApiResponse<MeCooksCountDto>>(meCooksCountKey, (prev) => {
    const current = prev?.data?.count ?? 0;
    const nextCount = Math.max(0, current + delta);
    if (prev) {
      return { ...prev, data: { count: nextCount } };
    }
    return { code: 0, message: "Ok", data: { count: nextCount } };
  });
}

// Helper to remove an entry from all pages
export function removeFromAllMeCooksPages(queryClient: QueryClient, cookId: string) {
  const pages = queryClient.getQueriesData<ApiResponse<MyCookedRecipeCardDto[]>>({
    queryKey: meCooksKeyPrefix,
  });

  pages.forEach(([key, value]) => {
    if (!value?.data) return;
    const next = value.data.filter((item) => item.cookId !== cookId);
    if (next.length === value.data.length) return;
    queryClient.setQueryData<ApiResponse<MyCookedRecipeCardDto[]>>(key, {
      ...value,
      data: next,
    });
  });
}
