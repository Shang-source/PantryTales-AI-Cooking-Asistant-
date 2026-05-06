import { useQuery, type QueryClient } from "@tanstack/react-query";

import type { ApiResponse } from "@/types/api";
import type {
  MeSavesCountDto,
  MySavedRecipeCardDto,
  RecipeCardDto,
  RecipeDetailDto,
  RecipeSaveResponse,
} from "@/types/recipes";
import { useAuthMutation } from "@/hooks/useApi";

export const savedRecipeIdsKey = ["saved-recipe-ids"] as const;
export const meSavesCountKey = ["me-saves-count"] as const;
export const meSavesKeyPrefix = ["me-saves"] as const;
export const meSavesKey = (page: number, pageSize: number) =>
  ["me-saves", `page:${page}`, `pageSize:${pageSize}`] as const;

export const communityRecipesKey = ["community-recipes", "scope:community"] as const;
export const myRecipesKey = ["my-recipes"] as const;
export const recipeKey = (recipeId: string) => ["recipe", recipeId] as const;

export const DEFAULT_ME_SAVES_PAGE_SIZE = 20;

const uniq = (values: string[]) => Array.from(new Set(values));

export function useSavedRecipeIds() {
  return useQuery<string[]>({
    queryKey: savedRecipeIdsKey,
    queryFn: async () => [],
    initialData: [],
    staleTime: Number.POSITIVE_INFINITY,
  });
}

export function useRecipeSaveToggle<TResponse = RecipeSaveResponse>(recipeId?: string) {
  return useAuthMutation<ApiResponse<TResponse>, void>(() => {
    if (!recipeId) {
      throw new Error("Missing recipe id");
    }
    return `/api/recipes/${recipeId}/saves/toggle`;
  }, "POST");
}

export function upsertSavedRecipeId(
  queryClient: QueryClient,
  recipeId: string,
  isSaved: boolean,
) {
  queryClient.setQueryData<string[]>(savedRecipeIdsKey, (prev) => {
    const current = prev ?? [];
    if (isSaved) {
      return uniq([...current, recipeId]);
    }
    return current.filter((id) => id !== recipeId);
  });
}

function updateRecipeDetailCache(
  queryClient: QueryClient,
  recipeId: string,
  isSaved: boolean,
  savesCount?: number,
) {
  queryClient.setQueryData<ApiResponse<RecipeDetailDto>>(
    recipeKey(recipeId),
    (prev) => {
      if (!prev?.data) return prev;
      return {
        ...prev,
        data: {
          ...prev.data,
          savedByMe: isSaved,
          ...(typeof savesCount === "number" ? { savedCount: savesCount } : {}),
        },
      };
    },
  );
}

function updateCommunityListCache(
  queryClient: QueryClient,
  recipeId: string,
  isSaved: boolean,
  savesCount?: number,
) {
  queryClient.setQueryData<
    ApiResponse<Array<{ id: string; savedCount?: number; savedByMe?: boolean }>>
  >(communityRecipesKey, (prev) => {
    if (!prev?.data) return prev;
    const next = prev.data.map((item) => {
      if (item.id !== recipeId) return item;
      return {
        ...item,
        savedByMe: isSaved,
        ...(typeof savesCount === "number" ? { savedCount: savesCount } : {}),
      };
    });
    return { ...prev, data: next };
  });
}

function updateMyRecipesCache(
  queryClient: QueryClient,
  recipeId: string,
  isSaved: boolean,
  savesCount?: number,
) {
  queryClient.setQueryData<ApiResponse<RecipeCardDto[]>>(myRecipesKey, (prev) => {
    if (!prev?.data) return prev;
    const next = prev.data.map((item) => {
      if (item.id !== recipeId) return item;
      return {
        ...item,
        savedByMe: isSaved,
        ...(typeof savesCount === "number" ? { savedCount: savesCount } : {}),
      };
    });
    return { ...prev, data: next };
  });
}

function updateMeSavesCountCache(queryClient: QueryClient, delta: number) {
  if (delta === 0) return;
  queryClient.setQueryData<ApiResponse<MeSavesCountDto>>(meSavesCountKey, (prev) => {
    const current = prev?.data?.count ?? 0;
    const nextCount = Math.max(0, current + delta);
    if (prev) {
      return { ...prev, data: { count: nextCount } };
    }
    return { code: 0, message: "Ok", data: { count: nextCount } };
  });
}

function removeFromAllMeSavesPages(queryClient: QueryClient, recipeId: string) {
  const pages = queryClient.getQueriesData<ApiResponse<MySavedRecipeCardDto[]>>({
    queryKey: meSavesKeyPrefix,
  });

  pages.forEach(([key, value]) => {
    if (!value?.data) return;
    const next = value.data.filter((item) => item.id !== recipeId);
    if (next.length === value.data.length) return;
    queryClient.setQueryData<ApiResponse<MySavedRecipeCardDto[]>>(key, {
      ...value,
      data: next,
    });
  });
}

function insertIntoFirstMeSavesPage(
  queryClient: QueryClient,
  item: MySavedRecipeCardDto,
  pageSize: number,
) {
  const key = meSavesKey(1, pageSize);
  queryClient.setQueryData<ApiResponse<MySavedRecipeCardDto[]>>(key, (prev) => {
    const current = prev?.data ?? [];
    const without = current.filter((x) => x.id !== item.id);
    const next = [item, ...without].slice(0, pageSize);
    if (prev) {
      return { ...prev, data: next };
    }
    return { code: 0, message: "Ok", data: next };
  });
}

export function applyRecipeSaveToggleResult(
  queryClient: QueryClient,
  response: RecipeSaveResponse,
  recipeMeta?: RecipeDetailDto | null,
  options?: { meSavesPageSize?: number; previousIsSaved?: boolean },
) {
  const pageSize = options?.meSavesPageSize ?? DEFAULT_ME_SAVES_PAGE_SIZE;
  const previousSavedIds = queryClient.getQueryData<string[]>(savedRecipeIdsKey) ?? [];
  const wasSaved =
    typeof options?.previousIsSaved === "boolean"
      ? options.previousIsSaved
      : previousSavedIds.includes(response.recipeId);
  const delta = (response.isSaved ? 1 : 0) - (wasSaved ? 1 : 0);
  const isPublic = recipeMeta ? recipeMeta.visibility === "Public" : true;

  upsertSavedRecipeId(queryClient, response.recipeId, response.isSaved);
  updateRecipeDetailCache(queryClient, response.recipeId, response.isSaved, response.savesCount);
  updateCommunityListCache(queryClient, response.recipeId, response.isSaved, response.savesCount);
  updateMyRecipesCache(queryClient, response.recipeId, response.isSaved, response.savesCount);
  if (isPublic) {
    updateMeSavesCountCache(queryClient, delta);
  }

  if (!response.isSaved || !isPublic) {
    removeFromAllMeSavesPages(queryClient, response.recipeId);
    return;
  }

  if (recipeMeta) {
    const coverImageUrl = recipeMeta.imageUrls?.find(Boolean) ?? null;
    const authorName = recipeMeta.author?.nickname ?? "";
    const now = new Date().toISOString();
    const savedCard: MySavedRecipeCardDto = {
      id: response.recipeId,
      title: recipeMeta.title,
      description: recipeMeta.description ?? null,
      coverImageUrl,
      authorId: recipeMeta.authorId ?? null,
      authorName,
      savedCount: typeof response.savesCount === "number" ? response.savesCount : recipeMeta.savedCount,
      savedByMe: true,
      savedAt: now,
      type: recipeMeta.type,
    };
    insertIntoFirstMeSavesPage(queryClient, savedCard, pageSize);
  } else {
    queryClient.invalidateQueries({ queryKey: meSavesKeyPrefix });
  }
}

