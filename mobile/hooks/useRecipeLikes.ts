import { useQuery, type QueryClient } from "@tanstack/react-query";

import type { ApiResponse } from "@/types/api";
import type {
  MeLikesCountDto,
  MyLikedRecipeCardDto,
  RecipeCardDto,
  RecipeDetailDto,
  RecipeLikeResponse,
} from "@/types/recipes";
import { useAuthMutation } from "@/hooks/useApi";

export const likedRecipeIdsKey = ["liked-recipe-ids"] as const;
export const meLikesCountKey = ["me-likes-count"] as const;
export const meLikesKeyPrefix = ["me-likes"] as const;
export const meLikesKey = (page: number, pageSize: number) =>
  ["me-likes", `page:${page}`, `pageSize:${pageSize}`] as const;

export const communityRecipesKey = ["community-recipes", "scope:community"] as const;
export const myRecipesKey = ["my-recipes"] as const;
export const recipeKey = (recipeId: string) => ["recipe", recipeId] as const;

export const DEFAULT_ME_LIKES_PAGE_SIZE = 20;

const uniq = (values: string[]) => Array.from(new Set(values));

/**
 * Client-only cache of recipe IDs that the current user has liked.
 *
 * This hook does not fetch from the server. It is initialized as an empty array
 * and populated via React Query cache updates (e.g. `upsertLikedRecipeId`) and
 * other server-backed queries that write into this cache.
 *
 * Do not treat this as a persistent source of truth; it is an in-memory index.
 */
export function useLikedRecipeIds() {
  return useQuery<string[]>({
    queryKey: likedRecipeIdsKey,
    queryFn: async () => [],
    initialData: [],
    staleTime: Number.POSITIVE_INFINITY,
  });
}

export function useRecipeLikeToggle(recipeId?: string) {
  return useAuthMutation<ApiResponse<RecipeLikeResponse>, void>(() => {
    if (!recipeId) {
      throw new Error("Missing recipe id");
    }
    return `/api/recipes/${recipeId}/likes/toggle`;
  }, "POST");
}

export function setLikedRecipeIds(queryClient: QueryClient, recipeIds: string[]) {
  queryClient.setQueryData<string[]>(likedRecipeIdsKey, () => uniq(recipeIds));
}

export function upsertLikedRecipeId(
  queryClient: QueryClient,
  recipeId: string,
  isLiked: boolean,
) {
  queryClient.setQueryData<string[]>(likedRecipeIdsKey, (prev) => {
    const current = prev ?? [];
    if (isLiked) {
      return uniq([...current, recipeId]);
    }
    return current.filter((id) => id !== recipeId);
  });
}

function updateRecipeDetailCache(
  queryClient: QueryClient,
  recipeId: string,
  isLiked: boolean,
  likesCount: number,
) {
  queryClient.setQueryData<ApiResponse<RecipeDetailDto>>(
    recipeKey(recipeId),
    (prev) => {
      if (!prev?.data) return prev;
      return {
        ...prev,
        data: {
          ...prev.data,
          likedByMe: isLiked,
          likesCount,
        },
      };
    },
  );
}

function updateCommunityListCache(
  queryClient: QueryClient,
  recipeId: string,
  isLiked: boolean,
  likesCount: number,
) {
  queryClient.setQueryData<ApiResponse<RecipeCardDto[]>>(communityRecipesKey, (prev) => {
    if (!prev?.data) return prev;
    const next = prev.data.map((item) =>
      item.id === recipeId ? { ...item, likesCount, likedByMe: isLiked } : item,
    );
    return { ...prev, data: next };
  });
}

function updateMyRecipesCache(
  queryClient: QueryClient,
  recipeId: string,
  likesCount: number,
) {
  queryClient.setQueryData<ApiResponse<RecipeCardDto[]>>(myRecipesKey, (prev) => {
    if (!prev?.data) return prev;
    const next = prev.data.map((item) =>
      item.id === recipeId ? { ...item, likesCount } : item,
    );
    return { ...prev, data: next };
  });
}

function updateMeLikesCountCache(queryClient: QueryClient, delta: number) {
  if (delta === 0) return;
  queryClient.setQueryData<ApiResponse<MeLikesCountDto>>(meLikesCountKey, (prev) => {
    const current = prev?.data?.count ?? 0;
    const nextCount = Math.max(0, current + delta);
    if (prev) {
      return { ...prev, data: { count: nextCount } };
    }
    return { code: 0, message: "Ok", data: { count: nextCount } };
  });
}

function removeFromAllMeLikesPages(queryClient: QueryClient, recipeId: string) {
  const pages = queryClient.getQueriesData<ApiResponse<MyLikedRecipeCardDto[]>>({
    queryKey: meLikesKeyPrefix,
  });

  pages.forEach(([key, value]) => {
    if (!value?.data) return;
    const next = value.data.filter((item) => item.id !== recipeId);
    if (next.length === value.data.length) return;
    queryClient.setQueryData<ApiResponse<MyLikedRecipeCardDto[]>>(key, {
      ...value,
      data: next,
    });
  });
}

function insertIntoFirstMeLikesPage(
  queryClient: QueryClient,
  item: MyLikedRecipeCardDto,
  pageSize: number,
) {
  const key = meLikesKey(1, pageSize);
  queryClient.setQueryData<ApiResponse<MyLikedRecipeCardDto[]>>(key, (prev) => {
    const current = prev?.data ?? [];
    const without = current.filter((x) => x.id !== item.id);
    const next = [item, ...without].slice(0, pageSize);
    if (prev) {
      return { ...prev, data: next };
    }
    return { code: 0, message: "Ok", data: next };
  });
}

export function applyRecipeLikeToggleResult(
  queryClient: QueryClient,
  response: RecipeLikeResponse,
  recipeMeta?: RecipeDetailDto | null,
  options?: { meLikesPageSize?: number },
) {
  /**
   * Applies the server toggle response to all relevant React Query caches
   * (detail, community list, my recipes, Me -> Likes list, and Me -> Likes count).
   *
   * `recipeMeta` should be provided when available so we can build a complete
   * card DTO for inserting into the first page of Me -> Likes without refetching.
   */
  const pageSize = options?.meLikesPageSize ?? DEFAULT_ME_LIKES_PAGE_SIZE;
  const previousLikedIds = queryClient.getQueryData<string[]>(likedRecipeIdsKey) ?? [];
  const wasLiked = previousLikedIds.includes(response.recipeId);
  const delta = (response.isLiked ? 1 : 0) - (wasLiked ? 1 : 0);
  const isPublic = recipeMeta ? recipeMeta.visibility === "Public" : true;

  upsertLikedRecipeId(queryClient, response.recipeId, response.isLiked);
  updateRecipeDetailCache(queryClient, response.recipeId, response.isLiked, response.likesCount);
  updateCommunityListCache(queryClient, response.recipeId, response.isLiked, response.likesCount);
  updateMyRecipesCache(queryClient, response.recipeId, response.likesCount);
  if (isPublic) {
    updateMeLikesCountCache(queryClient, delta);
  }

  if (!response.isLiked || !isPublic) {
    removeFromAllMeLikesPages(queryClient, response.recipeId);
    return;
  }

  if (recipeMeta) {
    const coverImageUrl = recipeMeta.imageUrls?.find(Boolean) ?? null;
    const authorName = recipeMeta.author?.nickname ?? "";
    const now = new Date().toISOString();
    const likedCard: MyLikedRecipeCardDto = {
      id: response.recipeId,
      title: recipeMeta.title,
      description: recipeMeta.description ?? null,
      coverImageUrl,
      authorId: recipeMeta.authorId ?? null,
      authorName,
      likesCount: response.likesCount,
      likedByMe: true,
      likedAt: now,
    };

    insertIntoFirstMeLikesPage(queryClient, likedCard, pageSize);
  } else {
    queryClient.invalidateQueries({ queryKey: meLikesKeyPrefix });
  }
}
