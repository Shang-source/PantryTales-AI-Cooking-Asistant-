// mobile/components/CommentSection.tsx
"use client";

import { Alert, Platform, Text, TouchableOpacity, View } from "react-native";
import { Heart, Trash2 } from "lucide-react-native";

import { Avatar, AvatarFallback, AvatarImage } from "@/components/avatar";
import type { RecipeCommentDto } from "@/types/comments";
import { useTheme } from "@/contexts/ThemeContext";

export interface CommentSectionProps {
  commentsCount: number;
  comments: RecipeCommentDto[];
  isLoading?: boolean;
  deletingCommentId?: string | null;
  likingCommentId?: string | null;
  onDeleteComment?: (commentId: string) => void;
  onLikeComment?: (commentId: string) => void;
}

function formatRelativeTime(iso: string) {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  const diffMs = Date.now() - date.getTime();
  const diffSeconds = Math.max(0, Math.floor(diffMs / 1000));
  if (diffSeconds < 15) return "just now";
  const minutes = Math.floor(diffSeconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

export function CommentSection({
  commentsCount,
  comments,
  isLoading,
  deletingCommentId,
  likingCommentId,
  onDeleteComment,
  onLikeComment,
}: CommentSectionProps) {
  const { colors } = useTheme();
  const label = isLoading
    ? "Comments"
    : commentsCount === 1
      ? "1 Comment"
      : `${commentsCount} Comments`;

  return (
    <View className="w-full">
      <View className="flex-row items-center gap-2 mb-4">
        <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>{label}</Text>
      </View>

      <View className="gap-4">
        {isLoading ? (
          <Text className="text-sm" style={{ color: colors.textMuted }}>Loading comments...</Text>
        ) : null}

        {!isLoading && comments.length === 0 ? (
          <Text className="text-sm" style={{ color: colors.textMuted }}>No comments yet.</Text>
        ) : null}

        {comments.map((comment) => {
          const author =
            comment.authorNickname ?? comment.authorName ?? "Unknown";
          const time = formatRelativeTime(comment.createdAt);
          const isDeletable = Boolean(comment.canDelete);
          const isDeleting = deletingCommentId === comment.id;
          const isLiking = likingCommentId === comment.id;
          const isLiked = Boolean(comment.isLikedByCurrentUser);
          const likeCount = comment.likeCount ?? 0;

          return (
            <View key={comment.id} className="flex-row gap-3">
              <Avatar className="h-9 w-9" style={{ backgroundColor: colors.card }}>
                {comment.authorAvatarUrl ? (
                  <AvatarImage source={{ uri: comment.authorAvatarUrl }} />
                ) : null}
                <AvatarFallback style={{ backgroundColor: colors.card }}>
                  <Text className="text-xs font-semibold" style={{ color: colors.textPrimary }}>
                    {author.charAt(0).toUpperCase()}
                  </Text>
                </AvatarFallback>
              </Avatar>

              <View className="flex-1">
                <View className="flex-row items-center justify-between">
                  <View className="flex-row items-center gap-2">
                    <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>
                      {author}
                    </Text>
                  </View>

                  {isDeletable ? (
                    <TouchableOpacity
                      onPress={() => {
                        if (!onDeleteComment || isDeleting) return;
                        if (Platform.OS === "web") {
                          const confirmed =
                            typeof window !== "undefined" &&
                            window.confirm(
                              "Delete comment? This cannot be undone.",
                            );
                          if (confirmed) onDeleteComment(comment.id);
                          return;
                        }

                        Alert.alert(
                          "Delete comment?",
                          "This cannot be undone.",
                          [
                            { text: "Cancel", style: "cancel" },
                            {
                              text: "Delete",
                              style: "destructive",
                              onPress: () => onDeleteComment(comment.id),
                            },
                          ],
                        );
                      }}
                      disabled={!onDeleteComment || isDeleting}
                      hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                      activeOpacity={0.8}
                      className={`rounded-full p-1 ${
                        isDeleting ? "opacity-40" : "opacity-70"
                      }`}
                    >
                      <Trash2 size={14} color={colors.textMuted} />
                    </TouchableOpacity>
                  ) : null}
                </View>

                <Text className="mt-1 text-sm leading-5" style={{ color: colors.textSecondary }}>
                  {comment.content}
                </Text>

                <View className="mt-2 flex-row items-center gap-3">
                  <TouchableOpacity
                    onPress={() => {
                      if (!onLikeComment || isLiking) return;
                      onLikeComment(comment.id);
                    }}
                    disabled={!onLikeComment || isLiking}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                    activeOpacity={0.8}
                    className={`flex-row items-center gap-1 ${
                      isLiking ? "opacity-40" : ""
                    }`}
                  >
                    <Heart
                      size={14}
                      color={isLiked ? colors.accent : colors.textMuted}
                      fill={isLiked ? colors.accent : "transparent"}
                    />
                    <Text
                      className="text-xs"
                      style={{ color: isLiked ? colors.accent : colors.textMuted }}
                    >
                      {likeCount}
                    </Text>
                  </TouchableOpacity>
                  <Text className="text-[11px]" style={{ color: colors.textMuted }}>{time}</Text>
                </View>
              </View>
            </View>
          );
        })}
      </View>
    </View>
  );
}
