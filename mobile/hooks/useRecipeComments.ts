import { useMemo } from "react";
import type { QueryKey } from "@tanstack/react-query";

import type { ApiResponse } from "@/types/api";
import type {
  CommentListResponseDto,
  RecipeCommentDto,
  ToggleCommentLikeResponseDto,
} from "@/types/comments";
import { useAuthQuery, useAuthMutation } from "@/hooks/useApi";

export const recipeCommentsKey = (recipeId: string) =>
  ["recipe-comments", recipeId] as const;

export type NormalizedRecipeComments = {
  recipeId: string;
  totalCount: number;
  items: RecipeCommentDto[];
};

type AnyApiResponse = ApiResponse<unknown> | unknown;

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isRecipeCommentDto(value: unknown): value is RecipeCommentDto {
  if (!isObject(value)) return false;

  const { id, userId, content, createdAt } = value;

  if (typeof id !== "string") return false;
  if (typeof userId !== "string") return false;
  if (typeof content !== "string") return false;
  if (typeof createdAt !== "string") return false;

  const recipeId = value.recipeId;
  if (typeof recipeId !== "undefined" && typeof recipeId !== "string") return false;

  const authorAvatarUrl = value.authorAvatarUrl;
  if (
    typeof authorAvatarUrl !== "undefined" &&
    authorAvatarUrl !== null &&
    typeof authorAvatarUrl !== "string"
  ) {
    return false;
  }

  const canDelete = value.canDelete;
  if (typeof canDelete !== "undefined" && typeof canDelete !== "boolean") return false;

  const likeCount = value.likeCount;
  if (typeof likeCount !== "undefined" && typeof likeCount !== "number") return false;

  const isLikedByCurrentUser = value.isLikedByCurrentUser;
  if (
    typeof isLikedByCurrentUser !== "undefined" &&
    typeof isLikedByCurrentUser !== "boolean"
  ) {
    return false;
  }

  return true;
}

function asCommentArray(value: unknown): RecipeCommentDto[] | null {
  if (!Array.isArray(value)) return null;
  return value.filter(isRecipeCommentDto);
}

export function normalizeRecipeCommentsResponse(
  recipeId: string,
  response: AnyApiResponse,
): NormalizedRecipeComments {
  const payload = isObject(response) && "data" in response ? (response as any).data : response;

  const array = asCommentArray(payload);
  if (array) {
    return { recipeId, totalCount: array.length, items: array };
  }

  if (isObject(payload)) {
    const typed = payload as Partial<CommentListResponseDto>;
    const items = asCommentArray(typed.items) ?? [];
    const totalCount = typeof typed.totalCount === "number" ? typed.totalCount : items.length;
    return { recipeId, totalCount, items };
  }

  return { recipeId, totalCount: 0, items: [] };
}

export function useRecipeComments(recipeId?: string) {
  const queryKey: QueryKey = recipeId ? recipeCommentsKey(recipeId) : ["recipe-comments", "missing"];

  const query = useAuthQuery<ApiResponse<unknown>>(
    queryKey as string[],
    recipeId ? `/api/recipes/${recipeId}/comments` : "",
    { enabled: Boolean(recipeId) },
  );

  const normalized = useMemo(() => {
    if (!recipeId) return { recipeId: "", totalCount: 0, items: [] as RecipeCommentDto[] };
    return normalizeRecipeCommentsResponse(recipeId, query.data);
  }, [query.data, recipeId]);

  return { ...query, ...normalized };
}

export function useCreateRecipeComment(recipeId?: string) {
  return useAuthMutation<ApiResponse<RecipeCommentDto>, { content: string }>(() => {
    if (!recipeId) {
      throw new Error("Missing recipe id");
    }
    return `/api/recipes/${recipeId}/comments`;
  }, "POST");
}

export function useDeleteRecipeComment() {
  return useAuthMutation<unknown, { commentId: string }>(
    (body) => `/api/comments/${body.commentId}`,
    "DELETE",
  );
}

export function useToggleRecipeCommentLike() {
  return useAuthMutation<ApiResponse<ToggleCommentLikeResponseDto>, { commentId: string }>(
    (body) => `/api/comments/${body.commentId}/like`,
    "POST",
  );
}
