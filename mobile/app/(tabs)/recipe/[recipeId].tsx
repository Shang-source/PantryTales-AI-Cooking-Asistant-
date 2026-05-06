import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  BackHandler,
  Keyboard,
  Platform,
  ScrollView,
  Text,
  TextInput,
  TouchableOpacity,
  View,
  useWindowDimensions,
} from "react-native";
import type { LayoutChangeEvent } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { useLocalSearchParams, useRouter, useFocusEffect } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { Bookmark, Heart, MessageCircle, Share2 } from "lucide-react-native";
import { useAuth } from "@clerk/clerk-expo";

import { RecipeDetail } from "@/components/RecipeDetail";
import { CommentSection } from "@/components/CommentSection";
import { RecipePosterPreviewModal } from "@/components/RecipePosterPreviewModal";
import {
  RecipePosterData,
  recipeDetailToPosterData,
} from "@/components/RecipePoster";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/avatar";
import { Button } from "@/components/Button";
import type { ApiResponse } from "@/types/api";
import { useQueryClient } from "@tanstack/react-query";
import type {
  RecipeCardDto,
  RecipeDetailDto,
  RecipeSaveResponse,
} from "@/types/recipes";
import {
  applyRecipeLikeToggleResult,
  communityRecipesKey,
  myRecipesKey,
  recipeKey,
  upsertLikedRecipeId,
  useRecipeLikeToggle,
} from "@/hooks/useRecipeLikes";
import { toast } from "@/components/sonner";
import { useTheme } from "@/contexts/ThemeContext";
import type { RecipeCommentDto } from "@/types/comments";
import {
  applyRecipeSaveToggleResult,
  upsertSavedRecipeId,
  useRecipeSaveToggle,
  useSavedRecipeIds,
} from "@/hooks/useRecipeSaves";
import {
  normalizeRecipeCommentsResponse,
  recipeCommentsKey,
  useCreateRecipeComment,
  useDeleteRecipeComment,
  useRecipeComments,
  useToggleRecipeCommentLike,
} from "@/hooks/useRecipeComments";
import LoadingView from "@/components/ui/LoadingView";

type AnyRecipeSaveResponse = {
  recipeId?: unknown;
  isSaved?: unknown;
  savesCount?: unknown;
  savedCount?: unknown;
  savedByMe?: unknown;
};

function normalizeRecipeSaveResponse(
  input: AnyRecipeSaveResponse | null | undefined,
): RecipeSaveResponse | null {
  if (!input) return null;
  if (typeof input.recipeId !== "string") return null;

  const rawIsSaved = input.isSaved ?? input.savedByMe;
  if (typeof rawIsSaved !== "boolean") return null;

  const rawCount = input.savesCount ?? input.savedCount;
  const savesCount = typeof rawCount === "number" ? rawCount : undefined;

  return { recipeId: input.recipeId, isSaved: rawIsSaved, savesCount };
}

export default function RecipeDetailScreen() {
  const { recipeId, source, tab, backTo } = useLocalSearchParams<{
    recipeId?: string;
    source?: string;
    tab?: string;
    backTo?: string | string[];
  }>();
  const router = useRouter();
  const { isSignedIn } = useAuth();
  const { colors } = useTheme();
  const queryClient = useQueryClient();
  const scrollRef = useRef<ScrollView>(null);
  const commentInputRef = useRef<TextInput>(null);
  const [commentsOffsetY, setCommentsOffsetY] = useState<number | null>(null);
  const [recipeMeta, setRecipeMeta] = useState<RecipeDetailDto | null>(null);
  const [stats, setStats] = useState({ likes: 0, comments: 0, saves: 0 });
  const [isLiked, setIsLiked] = useState(false);
  const [isSaved, setIsSaved] = useState(false);
  const [hasSyncedSaveState, setHasSyncedSaveState] = useState(false);
  const [commentInput, setCommentInput] = useState("");
  const [isCommentFocused, setIsCommentFocused] = useState(false);
  const [keyboardHeight, setKeyboardHeight] = useState(0);
  const [deletingCommentId, setDeletingCommentId] = useState<string | null>(
    null,
  );
  const [likingCommentId, setLikingCommentId] = useState<string | null>(null);
  const [isPosterPreviewVisible, setIsPosterPreviewVisible] = useState(false);
  const isRecommendedType =
    recipeMeta?.type === "System" || recipeMeta?.type === "Model";
  const backTarget = Array.isArray(backTo) ? backTo[0] : backTo;

  const commentsQuery = useRecipeComments(recipeId);
  const { data: savedIds = [] } = useSavedRecipeIds();
  const isDetailReady = Boolean(recipeMeta);

  // Poster data for share preview
  // Smart Recipe and Recommended Recipe don't show description in poster
  const posterData: RecipePosterData | null = useMemo(() => {
    if (!recipeMeta) return null;
    const isSmartOrRecommended =
      recipeMeta.type === "System" || recipeMeta.type === "Model";
    return {
      ...recipeDetailToPosterData(recipeMeta),
      showDescription: !isSmartOrRecommended,
    };
  }, [recipeMeta]);

  const handleOpenPosterPreview = useCallback(() => {
    if (!recipeMeta) return;
    setIsPosterPreviewVisible(true);
  }, [recipeMeta]);

  const handleClosePosterPreview = useCallback(() => {
    setIsPosterPreviewVisible(false);
  }, []);

  useEffect(() => {
    if (!recipeId) return;
    if (!commentsQuery.isSuccess) return;
    setStats((prev) => ({ ...prev, comments: commentsQuery.totalCount }));
  }, [commentsQuery.isSuccess, commentsQuery.totalCount, recipeId]);

  useEffect(() => {
    if (!recipeMeta) return;
    setStats({
      likes: recipeMeta.likesCount ?? 0,
      comments: recipeMeta.commentsCount ?? 0,
      saves: recipeMeta.savedCount ?? 0,
    });
    setIsLiked(Boolean(recipeMeta.likedByMe));
    setIsSaved(Boolean(recipeMeta.savedByMe));
    upsertLikedRecipeId(
      queryClient,
      recipeMeta.id,
      Boolean(recipeMeta.likedByMe),
    );
    upsertSavedRecipeId(
      queryClient,
      recipeMeta.id,
      Boolean(recipeMeta.savedByMe),
    );
  }, [queryClient, recipeMeta]);

  useEffect(() => {
    setRecipeMeta(null);
    setStats({ likes: 0, comments: 0, saves: 0 });
    setIsLiked(false);
    setIsSaved(false);
    setHasSyncedSaveState(false);
  }, [recipeId]);

  useEffect(() => {
    const showEvent =
      Platform.OS === "ios" ? "keyboardWillShow" : "keyboardDidShow";
    const hideEvent =
      Platform.OS === "ios" ? "keyboardWillHide" : "keyboardDidHide";
    const showSub = Keyboard.addListener(showEvent, (event) => {
      setKeyboardHeight(event.endCoordinates?.height ?? 0);
    });
    const hideSub = Keyboard.addListener(hideEvent, () => {
      setKeyboardHeight(0);
      setIsCommentFocused(false);
      commentInputRef.current?.blur();
    });
    return () => {
      showSub.remove();
      hideSub.remove();
    };
  }, []);

  useEffect(() => {
    if (!recipeId || recipeMeta || hasSyncedSaveState) return;
    if (savedIds.length === 0) return;
    setIsSaved(savedIds.includes(recipeId));
    setHasSyncedSaveState(true);
  }, [hasSyncedSaveState, recipeId, recipeMeta, savedIds]);

  const handleCommentPress = useCallback(() => {
    if (!scrollRef.current) return;
    if (typeof commentsOffsetY === "number") {
      scrollRef.current.scrollTo({
        y: Math.max(commentsOffsetY - 24, 0),
        animated: true,
      });
      return;
    }
    scrollRef.current.scrollToEnd({ animated: true });
  }, [commentsOffsetY]);

  const handleCommentsLayout = useCallback((event: LayoutChangeEvent) => {
    setCommentsOffsetY(event.nativeEvent.layout.y);
  }, []);

  const handleBack = useCallback(() => {
    // Handle sources that start with "me-saves" (me-saves-community, me-saves-generated, etc.)
    if (source?.startsWith("me-saves")) {
      router.replace({ pathname: "/me", params: { tab: "Saves" } });
      return;
    }

    switch (source) {
      case "home":
        router.replace("/");
        break;

      case "recommended":
        router.replace("/recommended-recipes");
        break;

      case "smart":
        router.replace("/smart-recipes");
        break;

      case "me-posts":
        router.replace({ pathname: "/me", params: { tab: "Posts" } });
        break;

      case "me-likes":
        router.replace({ pathname: "/me", params: { tab: "Likes" } });
        break;

      case "me-notes":
        router.replace({ pathname: "/me", params: { tab: "Notes" } });
        break;

      case "community":
        router.replace("/community");
        break;

      case "cooking-history":
        // Use dismiss() to pop the current screen and return to cooking-history
        if (router.canDismiss()) {
          router.dismiss();
        } else {
          router.replace("/cooking-history");
        }
        break;

      default:
        router.replace("/");
    }
  }, [router, source]);

  useFocusEffect(
    useCallback(() => {
      if (recipeId) {
        queryClient.invalidateQueries({ queryKey: recipeKey(recipeId) });
        queryClient.invalidateQueries({
          queryKey: recipeCommentsKey(recipeId),
        });
      }

      const onBackPress = () => {
        handleBack();
        return true;
      };
      const sub = BackHandler.addEventListener(
        "hardwareBackPress",
        onBackPress,
      );
      return () => sub.remove();
    }, [handleBack, queryClient, recipeId]),
  );

  const likeMutation = useRecipeLikeToggle(recipeId);
  const saveMutation = useRecipeSaveToggle<AnyRecipeSaveResponse>(recipeId);

  const toggleLike = () => {
    if (!recipeId || likeMutation.isPending) return;
    const previousLiked = isLiked;
    const previousLikes = stats.likes;
    const optimisticLiked = !previousLiked;
    const delta = optimisticLiked ? 1 : -1;

    setIsLiked(optimisticLiked);
    setStats((prev) => ({ ...prev, likes: Math.max(0, prev.likes + delta) }));

    likeMutation.mutate(undefined, {
      onSuccess: (response) => {
        const payload = response.data;
        if (payload) {
          setIsLiked(payload.isLiked);
          setStats((prev) => ({ ...prev, likes: payload.likesCount }));
          applyRecipeLikeToggleResult(queryClient, payload, recipeMeta);
        }
      },
      onError: (error) => {
        setIsLiked(previousLiked);
        setStats((prev) => ({ ...prev, likes: previousLikes }));

        const message =
          error?.message ?? "Unable to update like. Please try again.";
        const status = (error as Error & { status?: number }).status;
        const looksUnauthorized =
          status === 401 ||
          /\\b401\\b/i.test(message) ||
          /unauthor/i.test(message) ||
          /determine clerk user id/i.test(message);

        if (looksUnauthorized) {
          Alert.alert("Sign in required", "Please sign in to like recipes.", [
            { text: "Cancel", style: "cancel" },
            { text: "Sign in", onPress: () => router.push("/sign-in") },
          ]);
        } else {
          Alert.alert("Like failed", message);
        }
      },
    });
  };

  const toggleSave = () => {
    if (!recipeId || saveMutation.isPending) return;

    const previousSaved = isSaved;
    const previousSavesCount = stats.saves;
    const optimisticSaved = !previousSaved;
    const delta = optimisticSaved ? 1 : -1;

    setIsSaved(optimisticSaved);
    setStats((prev) => ({ ...prev, saves: Math.max(0, prev.saves + delta) }));

    saveMutation.mutate(undefined, {
      onSuccess: (response) => {
        const payload = normalizeRecipeSaveResponse(response.data);
        if (!payload) {
          setIsSaved(previousSaved);
          setStats((prev) => ({ ...prev, saves: previousSavesCount }));
          return;
        }

        setIsSaved(payload.isSaved);
        const newSavesCount =
          typeof payload.savesCount === "number"
            ? payload.savesCount
            : previousSavesCount;
        setStats((prev) => ({ ...prev, saves: newSavesCount }));

        if (typeof payload.savesCount !== "number") {
          queryClient.invalidateQueries({ queryKey: ["recipe", recipeId] });
        }

        applyRecipeSaveToggleResult(queryClient, payload, recipeMeta, {
          previousIsSaved: previousSaved,
        });
      },
      onError: (error) => {
        setIsSaved(previousSaved);
        setStats((prev) => ({ ...prev, saves: previousSavesCount }));

        const message =
          error?.message ?? "Unable to update save. Please try again.";
        const status = (error as Error & { status?: number }).status;
        const looksUnauthorized =
          status === 401 ||
          /\\b401\\b/i.test(message) ||
          /unauthor/i.test(message) ||
          /determine clerk user id/i.test(message);

        if (looksUnauthorized) {
          Alert.alert("Sign in required", "Please sign in to save recipes.", [
            { text: "Cancel", style: "cancel" },
            { text: "Sign in", onPress: () => router.push("/sign-in") },
          ]);
        } else {
          Alert.alert("Save failed", message);
        }
      },
    });
  };

  const createCommentMutation = useCreateRecipeComment(recipeId);
  const deleteCommentMutation = useDeleteRecipeComment();
  const toggleCommentLikeMutation = useToggleRecipeCommentLike();

  const updateCommentsCountCaches = useCallback(
    (delta: number) => {
      if (!recipeId || delta === 0) return;

      setStats((prev) => ({
        ...prev,
        comments: Math.max(0, prev.comments + delta),
      }));

      queryClient.setQueryData<ApiResponse<RecipeDetailDto>>(
        recipeKey(recipeId),
        (prev) => {
          if (!prev?.data) return prev;
          return {
            ...prev,
            data: {
              ...prev.data,
              commentsCount: Math.max(
                0,
                (prev.data.commentsCount ?? 0) + delta,
              ),
            },
          };
        },
      );

      queryClient.setQueryData<
        ApiResponse<Array<{ id: string; commentsCount?: number }>>
      >(communityRecipesKey, (prev) => {
        if (!prev?.data) return prev;
        return {
          ...prev,
          data: prev.data.map((item) =>
            item.id === recipeId
              ? {
                  ...item,
                  commentsCount: Math.max(0, (item.commentsCount ?? 0) + delta),
                }
              : item,
          ),
        };
      });

      queryClient.setQueryData<ApiResponse<RecipeCardDto[]>>(
        myRecipesKey,
        (prev) => {
          if (!prev?.data) return prev;
          return {
            ...prev,
            data: prev.data.map((item) =>
              item.id === recipeId
                ? {
                    ...item,
                    commentsCount: Math.max(
                      0,
                      (item.commentsCount ?? 0) + delta,
                    ),
                  }
                : item,
            ),
          };
        },
      );
    },
    [queryClient, recipeId],
  );

  const handleSendComment = useCallback(() => {
    if (!recipeId) return;
    const trimmed = commentInput.trim();
    if (!trimmed) {
      toast.error("Enter a comment");
      return;
    }
    if (createCommentMutation.isPending) return;

    const key = recipeCommentsKey(recipeId);
    const previous = queryClient.getQueryData<ApiResponse<unknown>>(key);
    const normalized = normalizeRecipeCommentsResponse(recipeId, previous);

    const tempId = `temp-${Date.now()}-${Math.random().toString(36).slice(2, 11)}`;
    const tempComment: RecipeCommentDto = {
      id: tempId,
      userId: "me",
      authorNickname: "You",
      authorAvatarUrl: null,
      content: trimmed,
      createdAt: new Date().toISOString(),
      canDelete: true,
    };

    queryClient.setQueryData<ApiResponse<unknown>>(key, {
      code: 0,
      message: "Ok",
      data: {
        recipeId,
        totalCount: normalized.totalCount + 1,
        items: [tempComment, ...normalized.items],
      },
    });

    updateCommentsCountCaches(1);
    setCommentInput("");
    handleCommentPress();

    createCommentMutation.mutate(
      { content: trimmed },
      {
        onError: (error) => {
          queryClient.setQueryData(key, previous);
          updateCommentsCountCaches(-1);
          setCommentInput(trimmed);

          const message = error?.message ?? "Unable to post comment.";
          const looksUnauthorized =
            /\b401\b/i.test(message) ||
            /unauthor/i.test(message) ||
            /determine clerk user id/i.test(message);

          if (looksUnauthorized) {
            toast.info("Sign in required", {
              description: "Please sign in to comment.",
              action: {
                label: "Sign in",
                onClick: () => router.push("/sign-in"),
              },
            });
          } else {
            toast.error("Comment failed", { description: message });
          }
        },
        onSuccess: (response) => {
          const created = (response as ApiResponse<RecipeCommentDto>)?.data;
          if (!created) {
            queryClient.invalidateQueries({ queryKey: key });
            return;
          }

          queryClient.setQueryData<ApiResponse<unknown>>(key, (prev) => {
            const normalized = normalizeRecipeCommentsResponse(recipeId, prev);
            const items = normalized.items.map((item) =>
              item.id === tempId ? created : item,
            );
            return {
              code: 0,
              message: "Ok",
              data: {
                recipeId,
                totalCount: normalized.totalCount,
                items,
              },
            };
          });

          toast.success("Comment posted");
        },
        onSettled: () => {
          queryClient.invalidateQueries({ queryKey: key });
        },
      },
    );
  }, [
    commentInput,
    createCommentMutation,
    handleCommentPress,
    queryClient,
    recipeId,
    router,
    updateCommentsCountCaches,
  ]);

  const handleDeleteComment = useCallback(
    (commentId: string) => {
      if (!recipeId || deleteCommentMutation.isPending || deletingCommentId)
        return;

      const key = recipeCommentsKey(recipeId);
      const previous = queryClient.getQueryData<ApiResponse<unknown>>(key);
      const normalized = normalizeRecipeCommentsResponse(recipeId, previous);
      const nextItems = normalized.items.filter(
        (item) => item.id !== commentId,
      );
      const removed = nextItems.length !== normalized.items.length;

      if (removed) {
        queryClient.setQueryData<ApiResponse<unknown>>(key, {
          code: 0,
          message: "Ok",
          data: {
            recipeId,
            totalCount: Math.max(0, normalized.totalCount - 1),
            items: nextItems,
          },
        });
        updateCommentsCountCaches(-1);
      }

      setDeletingCommentId(commentId);
      deleteCommentMutation.mutate(
        { commentId },
        {
          onError: (error) => {
            queryClient.setQueryData(key, previous);
            if (removed) {
              updateCommentsCountCaches(1);
            }

            const message = error?.message ?? "Unable to delete comment.";
            const looksUnauthorized =
              /\b401\b/i.test(message) ||
              /unauthor/i.test(message) ||
              /determine clerk user id/i.test(message);

            if (looksUnauthorized) {
              toast.info("Sign in required", {
                description: "Please sign in to manage comments.",
                action: {
                  label: "Sign in",
                  onClick: () => router.push("/sign-in"),
                },
              });
            } else if (/403\b/.test(message) || /forbid/i.test(message)) {
              toast.error("Not allowed", {
                description: "You can only delete your own comments.",
              });
            } else {
              toast.error("Delete failed", { description: message });
            }
          },
          onSuccess: () => {
            toast.success("Deleted successfully");
          },
          onSettled: () => {
            setDeletingCommentId(null);
            queryClient.invalidateQueries({ queryKey: key });
          },
        },
      );
    },
    [
      deleteCommentMutation,
      deletingCommentId,
      queryClient,
      recipeId,
      router,
      updateCommentsCountCaches,
    ],
  );

  const handleLikeComment = useCallback(
    (commentId: string) => {
      if (!recipeId || toggleCommentLikeMutation.isPending || likingCommentId)
        return;

      if (!isSignedIn) {
        Alert.alert("Sign in required", "Please sign in to like comments.", [
          { text: "Cancel", style: "cancel" },
          { text: "Sign in", onPress: () => router.push("/sign-in") },
        ]);
        return;
      }

      const key = recipeCommentsKey(recipeId);
      const previous = queryClient.getQueryData<ApiResponse<unknown>>(key);
      const normalized = normalizeRecipeCommentsResponse(recipeId, previous);

      const targetComment = normalized.items.find(
        (item) => item.id === commentId,
      );
      if (!targetComment) return;

      const wasLiked = Boolean(targetComment.isLikedByCurrentUser);
      const prevLikeCount = targetComment.likeCount ?? 0;
      const optimisticLiked = !wasLiked;
      const optimisticCount = optimisticLiked
        ? prevLikeCount + 1
        : Math.max(0, prevLikeCount - 1);

      queryClient.setQueryData<ApiResponse<unknown>>(key, {
        code: 0,
        message: "Ok",
        data: {
          recipeId,
          totalCount: normalized.totalCount,
          items: normalized.items.map((item) =>
            item.id === commentId
              ? {
                  ...item,
                  isLikedByCurrentUser: optimisticLiked,
                  likeCount: optimisticCount,
                }
              : item,
          ),
        },
      });

      setLikingCommentId(commentId);
      toggleCommentLikeMutation.mutate(
        { commentId },
        {
          onError: (error) => {
            queryClient.setQueryData(key, previous);

            const message = error?.message ?? "Unable to like comment.";
            const status = (error as Error & { status?: number }).status;
            const looksUnauthorized =
              status === 401 ||
              /\b401\b/i.test(message) ||
              /unauthor/i.test(message) ||
              /determine clerk user id/i.test(message);

            if (looksUnauthorized) {
              Alert.alert(
                "Sign in required",
                "Please sign in to like comments.",
                [
                  { text: "Cancel", style: "cancel" },
                  { text: "Sign in", onPress: () => router.push("/sign-in") },
                ],
              );
            } else {
              toast.error("Like failed", { description: message });
            }
          },
          onSuccess: (response) => {
            const payload = response?.data;
            if (!payload) return;

            queryClient.setQueryData<ApiResponse<unknown>>(key, (prev) => {
              const current = normalizeRecipeCommentsResponse(recipeId, prev);
              return {
                code: 0,
                message: "Ok",
                data: {
                  recipeId,
                  totalCount: current.totalCount,
                  items: current.items.map((item) =>
                    item.id === payload.commentId
                      ? {
                          ...item,
                          isLikedByCurrentUser: payload.isLiked,
                          likeCount: payload.likeCount,
                        }
                      : item,
                  ),
                },
              };
            });
          },
          onSettled: () => {
            setLikingCommentId(null);
          },
        },
      );
    },
    [
      toggleCommentLikeMutation,
      likingCommentId,
      queryClient,
      recipeId,
      router,
      isSignedIn,
    ],
  );

  const handleStartCooking = useCallback(() => {
    if (!recipeId) return;
    router.push({
      pathname: "/cook",
      params: { recipeId, source: source ?? "home" },
    });
  }, [recipeId, router, source]);

  const tabBarHeight = 82;
  const scrollBottomPadding = isRecommendedType ? 12 : tabBarHeight + 60;

  const StartCookingButton = (
    <TouchableOpacity
      onPress={handleStartCooking}
      activeOpacity={0.9}
      className="h-12 flex-row items-center justify-center gap-2 rounded-full"
      style={{ backgroundColor: colors.accent }}
    >
      <Ionicons name="restaurant-outline" size={18} color={colors.bg} />
      <Text className="text-center text-base font-semibold" style={{ color: colors.bg }}>
        Start Cooking
      </Text>
    </TouchableOpacity>
  );

  return (
    <SafeAreaView
      edges={["top", "left", "right"]}
      className="flex-1"
      style={{ backgroundColor: colors.bg }}
    >
      {/* header */}
      <View className="flex-row items-center justify-between px-4 py-2">
        <View className="flex-row items-center gap-4">
          <TouchableOpacity
            onPress={handleBack}
            activeOpacity={0.7}
            className="px-1"
          >
            <Ionicons name="chevron-back" size={28} color={colors.accent} />
          </TouchableOpacity>
          {!isRecommendedType && isDetailReady && (
            <View className="flex-row items-center gap-3">
              <Avatar className="h-12 w-12" style={{ backgroundColor: colors.accentMuted }}>
                {recipeMeta?.author?.avatarUrl ? (
                  <AvatarImage source={{ uri: recipeMeta.author.avatarUrl }} />
                ) : null}
                <AvatarFallback style={{ backgroundColor: colors.accentMuted }}>
                  <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>
                    {recipeMeta?.author?.nickname?.charAt(0)?.toUpperCase() ??
                      "?"}
                  </Text>
                </AvatarFallback>
              </Avatar>
              <Text
                numberOfLines={1}
                className="max-w-[180px] text-xl font-semibold"
                style={{ color: colors.textPrimary }}
              >
                {recipeMeta?.author?.nickname?.trim()
                  ? recipeMeta.author.nickname
                  : "Deleted user"}
              </Text>
            </View>
          )}
        </View>
        {isDetailReady && (
          <View className="flex-row items-center gap-2">
            <TouchableOpacity
              onPress={handleOpenPosterPreview}
              activeOpacity={0.8}
              className="h-10 w-10 items-center justify-center rounded-full"
              style={{ backgroundColor: colors.card }}
            >
              <Share2 size={18} color={colors.textPrimary} />
            </TouchableOpacity>
            {isRecommendedType && (
              <TouchableOpacity
                onPress={toggleSave}
                activeOpacity={0.8}
                className="h-10 w-10 items-center justify-center rounded-full"
                style={{ backgroundColor: colors.card }}
              >
                <Bookmark
                  size={18}
                  color={isSaved ? colors.accent : colors.textPrimary}
                  fill={isSaved ? colors.accent : "transparent"}
                />
              </TouchableOpacity>
            )}
          </View>
        )}
      </View>

      {/* content */}
      <ScrollView
        ref={scrollRef}
        className="mt-2 w-full flex-1"
        showsVerticalScrollIndicator={false}
        contentContainerStyle={{
          paddingBottom: scrollBottomPadding,
        }}
      >
        <View className="flex-1 px-4 pt-4">
          {recipeId ? (
            <View className="w-full">
              {!isDetailReady && <LoadingView />}
              <View
                className="w-full"
                style={!isDetailReady ? { display: "none" } : undefined}
              >
                <RecipeDetail
                  recipeId={recipeId}
                  onRecipeReady={setRecipeMeta}
                  layout={isRecommendedType ? "recommended" : "default"}
                  showStartCookingButton={false}
                  onStartCooking={handleStartCooking}
                  showShareButton={true}
                  onSharePress={handleOpenPosterPreview}
                />
                {!isRecommendedType && (
                  <View onLayout={handleCommentsLayout} className="mt-8 w-full">
                    <CommentSection
                      commentsCount={isDetailReady ? stats.comments : 0}
                      comments={isDetailReady ? commentsQuery.items : []}
                      isLoading={!isDetailReady || commentsQuery.isLoading}
                      deletingCommentId={deletingCommentId}
                      likingCommentId={likingCommentId}
                      onDeleteComment={handleDeleteComment}
                      onLikeComment={handleLikeComment}
                    />
                  </View>
                )}
              </View>
            </View>
          ) : (
            <View className="mt-20 items-center rounded-3xl border px-4 py-8" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
              <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                Invalid recipe
              </Text>
              <Text className="mt-2 text-sm" style={{ color: colors.textSecondary }}>
                Try opening this page from the community feed again.
              </Text>
            </View>
          )}
        </View>
      </ScrollView>

      {!isRecommendedType && isDetailReady && (
        <View
          style={{
            position: "absolute",
            left: 0,
            right: 0,
            bottom: isCommentFocused
              ? Math.max(0, keyboardHeight - tabBarHeight)
              : 0,
            backgroundColor: colors.bg,
          }}
          className="pb-4 px-4"
        >
          <View className="w-full border-t pt-2 flex-row items-center gap-4" style={{ borderTopColor: colors.border }}>
            <View className="flex-1 flex-row items-center rounded-full border pl-4 pr-2 h-11" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
              <TextInput
                ref={commentInputRef}
                value={commentInput}
                onChangeText={setCommentInput}
                placeholder="Write a comment..."
                placeholderTextColor={colors.textMuted}
                className="flex-1 text-sm"
                style={{ paddingVertical: 0, color: colors.textPrimary }}
                textAlignVertical="center"
                editable={Boolean(recipeId)}
                maxLength={1000}
                onFocus={() => setIsCommentFocused(true)}
                onBlur={() => setIsCommentFocused(false)}
                onPressIn={() => {
                  if (!isCommentFocused) {
                    setIsCommentFocused(true);
                  }
                  if (Platform.OS === "android") {
                    commentInputRef.current?.focus();
                  }
                }}
              />
              <TouchableOpacity
                onPress={handleSendComment}
                disabled={
                  !commentInput.trim() || createCommentMutation.isPending
                }
                className="ml-2 p-2"
                activeOpacity={0.85}
              >
                <Ionicons
                  name="send"
                  size={18}
                  color={
                    commentInput.trim() && !createCommentMutation.isPending
                      ? colors.accent
                      : colors.textMuted
                  }
                />
              </TouchableOpacity>
            </View>
            {!isCommentFocused && (
              <View className="flex-row items-center gap-5">
                <TouchableOpacity
                  onPress={toggleLike}
                  activeOpacity={0.8}
                  disabled={likeMutation.isPending}
                  className="items-center gap-1"
                >
                  <Heart
                    size={20}
                    color={isLiked ? colors.accent : colors.textPrimary}
                    fill={isLiked ? colors.accent : "transparent"}
                  />
                  <Text
                    className="text-xs font-semibold"
                    style={{ color: isLiked ? colors.accent : colors.textPrimary }}
                  >
                    {stats.likes}
                  </Text>
                </TouchableOpacity>
                <TouchableOpacity
                  onPress={handleCommentPress}
                  activeOpacity={0.8}
                  disabled={!isDetailReady}
                  className="items-center gap-1"
                >
                  <MessageCircle size={20} color={colors.textPrimary} />
                  <Text className="text-xs font-semibold" style={{ color: colors.textPrimary }}>
                    {stats.comments}
                  </Text>
                </TouchableOpacity>
                <TouchableOpacity
                  onPress={toggleSave}
                  activeOpacity={0.8}
                  disabled={saveMutation.isPending}
                  className="items-center gap-1"
                >
                  <Bookmark
                    size={20}
                    color={isSaved ? colors.accent : colors.textPrimary}
                    fill={isSaved ? colors.accent : "transparent"}
                  />
                  <Text
                    className="text-xs font-semibold"
                    style={{ color: isSaved ? colors.accent : colors.textPrimary }}
                  >
                    {stats.saves}
                  </Text>
                </TouchableOpacity>
              </View>
            )}
          </View>

          {!isCommentFocused && (
            <View className="mt-2">{StartCookingButton}</View>
          )}
        </View>
      )}

      {isRecommendedType && (
        <View
          className="w-full px-4 border-t pb-2 pt-2"
          style={{
            backgroundColor: colors.bg,
            borderTopColor: colors.border,
          }}
        >
          {StartCookingButton}
        </View>
      )}

      {/* Recipe Poster Preview Modal */}
      <RecipePosterPreviewModal
        visible={isPosterPreviewVisible}
        recipe={posterData}
        onClose={handleClosePosterPreview}
      />
    </SafeAreaView>
  );
}
