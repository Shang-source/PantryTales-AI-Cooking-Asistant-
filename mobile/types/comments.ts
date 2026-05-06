export type RecipeCommentDto = {
  id: string;
  recipeId?: string;
  userId: string;
  authorNickname?: string;
  authorName?: string;
  authorAvatarUrl?: string | null;
  content: string;
  createdAt: string;
  canDelete?: boolean;
  likeCount?: number;
  isLikedByCurrentUser?: boolean;
};

export type CommentListResponseDto = {
  recipeId: string;
  totalCount?: number;
  items?: RecipeCommentDto[];
};

export type CreateCommentRequestDto = {
  content: string;
};

export type CommentDeleteResponseDto = {
  commentId: string;
  deleted: boolean;
};

export type ToggleCommentLikeResponseDto = {
  commentId: string;
  isLiked: boolean;
  likeCount: number;
};

