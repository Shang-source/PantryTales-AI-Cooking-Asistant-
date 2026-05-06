import {
  ActivityIndicator,
  Alert,
  FlatList,
  Image,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
  useWindowDimensions,
} from "react-native";
import type { LayoutChangeEvent } from "react-native";
import Icon from "react-native-vector-icons/Feather";
import {
  SafeAreaView,
  useSafeAreaInsets,
} from "react-native-safe-area-context";
import { useAuthMutation, useAuthQuery } from "@/hooks/useApi";
import { useEffect, useState, useCallback, useMemo, useRef } from "react";
import { router, useLocalSearchParams, useFocusEffect } from "expo-router";
import { useQueryClient } from "@tanstack/react-query";

import { getDefaultAvatarUrl } from "@/utils/avatar";
import { Avatar, AvatarImage, AvatarFallback } from "@/components/avatar";
import { Card, CardTitle, CardContent } from "@/components/card";
import { Skeleton } from "@/components/skeleton";
import StatCard from "@/components/ui/StatCard";
import { IconBadge } from "@/components/IconBadge";
import { ApiResponse } from "@/types/api";
import { ToggleGroup, ToggleGroupItem } from "@/components/toggle-group";
import {
  MeLikesCountDto,
  MeSavesCountDto,
  MyLikedRecipeCardDto,
  MySavedRecipeCardDto,
  RecipeCardDto,
  RecipeType,
} from "@/types/recipes";
import { RecipeNoteCard } from "@/components/RecipeNoteCard";
import { RecipeDetail } from "@/components/RecipeDetail";
import {
  RecipeEditForm,
  type RecipeEditFormRef,
} from "@/components/RecipeEditForm";
import { useTheme } from "@/contexts/ThemeContext";

import {
  DEFAULT_ME_LIKES_PAGE_SIZE,
  likedRecipeIdsKey,
  setLikedRecipeIds,
} from "@/hooks/useRecipeLikes";
import {
  DEFAULT_ME_SAVES_PAGE_SIZE,
  upsertSavedRecipeId,
} from "@/hooks/useRecipeSaves";

export type UserGender = "Unknown" | "Male" | "Female" | "NotApplicable";

export type UserPreferenceRelation =
  | "Like"
  | "Dislike"
  | "Allergy"
  | "Restriction"
  | "Goal"
  | "Other";

export interface UserPreference {
  relation: UserPreferenceRelation;
  tagId: number;
  tagName: string;
  tagDisplayName: string;
  tagType: string;
  tagIcon?: string | null;
  tagColor?: string | null;
}

export interface UserProfileResponse {
  id: string;
  clerkUserId: string;
  email: string;
  nickname: string;
  avatarUrl: string | null;
  age: number | null;
  gender: UserGender | null;
  height: number | null;
  weight: number | null;
  createdAt: string;
  updatedAt: string;
  preferences: UserPreference[];
}

const createLayoutLogger =
  (scope: "view" | "edit") => (part: string) => (event: LayoutChangeEvent) => {
    if (!__DEV__) return;
    const { width, height } = event.nativeEvent.layout;
    console.log(
      `[RecipeModalLayout][${scope}] ${part}: ${width.toFixed(1)}x${height.toFixed(1)}`,
    );
  };

type EditModalStatus = {
  isSaving: boolean;
  isDeleting: boolean;
  isReady?: boolean;
};

type UpdateProfilePayload = {
  avatarUrl: string | null;
  age: number | null;
  gender: UserGender | null;
  height: number | null;
  weight: number | null;
};

export default function MeScreen() {
  const { tab } = useLocalSearchParams<{ tab?: string }>();
  const queryClient = useQueryClient();
  const safeInsets = useSafeAreaInsets();
  const { colors } = useTheme();
  const [userProfile, setUserProfile] = useState<UserProfileResponse | null>(
    null,
  );
  const [activeModal, setActiveModal] = useState<"view" | "edit" | null>(null);
  const [selectedRecipeId, setSelectedRecipeId] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const { mutateAsync: updateProfile } = useAuthMutation<
    ApiResponse<UserProfileResponse>,
    UpdateProfilePayload
  >("/api/users/me", "PUT");

  const { data, isLoading, refetch } = useAuthQuery<
    ApiResponse<UserProfileResponse>
  >(["user-profile"], "/api/users/me", {
    staleTime: 0,
    gcTime: 0,
    refetchOnMount: true,
  });
  const {
    data: recipesResponse,
    isLoading: recipesLoading,
    isError: recipesError,
    error: recipesErrorObject,
    refetch: refetchRecipes,
  } = useAuthQuery<ApiResponse<RecipeCardDto[]>>(
    ["my-recipes"],
    "/api/recipes?scope=me",
    { staleTime: 60_000 },
  );
  useFocusEffect(
    useCallback(() => {
      void refetch();
    }, [refetch]),
  );
  useFocusEffect(
    useCallback(() => {
      void refetchRecipes();
    }, [refetchRecipes]),
  );

  const { data: meLikesCountResponse, refetch: refetchMeLikesCount } =
    useAuthQuery<ApiResponse<MeLikesCountDto>>(
      ["me-likes-count"],
      "/api/me/likes/count",
      {
        staleTime: 0,
        gcTime: 300_000,
        refetchOnMount: true,
      },
    );

  useFocusEffect(
    useCallback(() => {
      void refetchMeLikesCount();
    }, [refetchMeLikesCount]),
  );

  const { data: meSavesCountResponse, refetch: refetchMeSavesCount } =
    useAuthQuery<ApiResponse<MeSavesCountDto>>(
      ["me-saves-count"],
      "/api/me/saves/count",
      {
        staleTime: 0,
        gcTime: 300_000,
        refetchOnMount: true,
      },
    );

  useFocusEffect(
    useCallback(() => {
      void refetchMeSavesCount();
    }, [refetchMeSavesCount]),
  );

  const meLikesPageSize = DEFAULT_ME_LIKES_PAGE_SIZE;
  const { data: meLikesResponse, refetch: refetchMeLikes } = useAuthQuery<
    ApiResponse<MyLikedRecipeCardDto[]>
  >(
    ["me-likes", "page:1", `pageSize:${meLikesPageSize}`],
    `/api/me/likes?page=1&pageSize=${meLikesPageSize}`,
    { staleTime: 0, gcTime: 300_000 },
  );

  const meSavesPageSize = DEFAULT_ME_SAVES_PAGE_SIZE;
  const { data: meSavesResponse, refetch: refetchMeSaves } = useAuthQuery<
    ApiResponse<MySavedRecipeCardDto[]>
  >(
    ["me-saves", "page:1", `pageSize:${meSavesPageSize}`],
    `/api/me/saves?page=1&pageSize=${meSavesPageSize}`,
    { staleTime: 0, gcTime: 300_000 },
  );

  useFocusEffect(
    useCallback(() => {
      void refetchMeLikes();
      void refetchMeSaves();
    }, [refetchMeLikes, refetchMeSaves]),
  );

  const handleRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await Promise.all([
        refetch(),
        refetchRecipes(),
        refetchMeLikesCount(),
        refetchMeSavesCount(),
        refetchMeLikes(),
        refetchMeSaves(),
      ]);
    } finally {
      setRefreshing(false);
    }
  }, [
    refetch,
    refetchRecipes,
    refetchMeLikesCount,
    refetchMeSavesCount,
    refetchMeLikes,
    refetchMeSaves,
  ]);

  const { height: windowHeight } = useWindowDimensions();
  const detailDialogMaxHeight = Math.min(windowHeight * 0.85, 640);
  const editDialogMaxHeight = Math.min(windowHeight * 0.9, 800);
  const keyboardBehavior = Platform.OS === "ios" ? "padding" : undefined;
  const editFormRef = useRef<RecipeEditFormRef>(null);
  const [editStatus, setEditStatus] = useState({
    isSaving: false,
    isDeleting: false,
    isReady: false,
  });
  const handleEditStatusChange = useCallback((status: EditModalStatus) => {
    setEditStatus((prev) => ({
      isSaving: status.isSaving,
      isDeleting: status.isDeleting,
      isReady: status.isReady ?? prev.isReady,
    }));
  }, []);
  const handleConfirmSave = useCallback(() => {
    if (
      !selectedRecipeId ||
      editStatus.isSaving ||
      editStatus.isDeleting ||
      !editStatus.isReady
    ) {
      return;
    }
    const executeSave = () => editFormRef.current?.save();
    if (Platform.OS === "web") {
      const confirmFn =
        typeof globalThis !== "undefined" &&
        typeof (globalThis as { confirm?: (message?: string) => boolean })
          .confirm === "function"
          ? (globalThis as { confirm?: (message?: string) => boolean }).confirm
          : undefined;
      if (!confirmFn || confirmFn("Save the latest changes?")) {
        executeSave();
      }
      return;
    }
    Alert.alert("Save changes", "Do you want to save your latest edits?", [
      { text: "Cancel", style: "cancel" },
      { text: "Save", style: "default", onPress: executeSave },
    ]);
  }, [
    selectedRecipeId,
    editStatus.isSaving,
    editStatus.isDeleting,
    editStatus.isReady,
  ]);
  const logViewLayout = useMemo(() => createLayoutLogger("view"), []);
  const logEditLayout = useMemo(() => createLayoutLogger("edit"), []);

  const deleteRecipeMutation = useAuthMutation<ApiResponse<void>, void>(
    () => {
      if (!selectedRecipeId) {
        throw new Error("Missing recipe id");
      }
      return `/api/recipes/${selectedRecipeId}`;
    },
    "DELETE",
    {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: ["my-recipes"] });
        queryClient.invalidateQueries({
          queryKey: ["community-recipes", "scope:community"],
        });
        void refetchRecipes();
        setActiveModal(null);
        setSelectedRecipeId(null);
      },
    },
  );
  useEffect(() => {
    if (data?.data) {
      setUserProfile(data.data);
    } else {
      setUserProfile(null);
    }
  }, [data]);

  const meLikedRecipes = useMemo(
    () => meLikesResponse?.data ?? [],
    [meLikesResponse],
  );
  useEffect(() => {
    const nextIds = meLikedRecipes.map((recipe) => recipe.id);
    if (nextIds.length === 0) return;
    const previous =
      queryClient.getQueryData<string[]>(likedRecipeIdsKey) ?? [];
    setLikedRecipeIds(queryClient, [...previous, ...nextIds]);
  }, [meLikedRecipes, queryClient]);

  const meSavedRecipes = useMemo(
    () => meSavesResponse?.data ?? [],
    [meSavesResponse],
  );
  useEffect(() => {
    meSavedRecipes.forEach((recipe) => {
      upsertSavedRecipeId(queryClient, recipe.id, true);
    });
  }, [meSavedRecipes, queryClient]);

  const syncedDefaultAvatarForUserId = useRef<string | null>(null);
  const syncingDefaultAvatarForUserId = useRef<string | null>(null);
  const {
    id: userId,
    avatarUrl: userAvatarUrl,
    age: userAge,
    gender: userGender,
    height: userHeight,
    weight: userWeight,
  } = userProfile ?? {};

  useEffect(() => {
    if (!userId) return;
    if (userAvatarUrl) return;
    const currentUserId = userId;
    if (
      syncedDefaultAvatarForUserId.current === currentUserId ||
      syncingDefaultAvatarForUserId.current === currentUserId
    ) {
      return;
    }

    const defaultAvatarUrl = getDefaultAvatarUrl(currentUserId);
    if (!defaultAvatarUrl) return;

    syncingDefaultAvatarForUserId.current = currentUserId;
    let cancelled = false;

    const syncDefaultAvatar = async () => {
      try {
        const updated = await updateProfile({
          avatarUrl: defaultAvatarUrl,
          age: userAge ?? null,
          gender: userGender ?? null,
          height: userHeight ?? null,
          weight: userWeight ?? null,
        });

        if (cancelled) return;
        syncedDefaultAvatarForUserId.current = currentUserId;

        if (updated?.data) {
          await queryClient.invalidateQueries({ queryKey: ["user-profile"] });
        }
      } catch {
        // Avoid retry loops; avatar fallback will still display locally.
      } finally {
        if (syncingDefaultAvatarForUserId.current === currentUserId) {
          syncingDefaultAvatarForUserId.current = null;
        }
      }
    };

    void syncDefaultAvatar();

    return () => {
      cancelled = true;
    };
  }, [
    queryClient,
    updateProfile,
    userAvatarUrl,
    userGender,
    userAge,
    userHeight,
    userId,
    userWeight,
  ]);

  const handleOpenRecipeDetail = (id: string) => {
    router.push({
      pathname: "/recipe/[recipeId]" as const,
      params: { recipeId: id, source: "me-posts", tab: "Posts" },
    });
  };

  const handleOpenRecipeEdit = (id: string) => {
    router.push({
      pathname: "/recipe-edit/[recipeId]" as const,
      params: { recipeId: id, source: "me-posts", tab: "Posts" },
    });
  };

  const handleCloseModals = () => {
    setActiveModal(null);
    setSelectedRecipeId(null);
    setEditStatus({ isSaving: false, isDeleting: false, isReady: false });
  };

  const handleEditSaved = () => {
    handleCloseModals();
    queryClient.invalidateQueries({
      queryKey: ["community-recipes", "scope:community"],
    });
    void refetchRecipes();
  };

  const handleEditDeleted = () => {
    handleCloseModals();
    queryClient.invalidateQueries({
      queryKey: ["community-recipes", "scope:community"],
    });
    void refetchRecipes();
  };

  const handleStartCooking = () => {
    if (!selectedRecipeId) return;
    router.push({
      pathname: "/cook",
      params: { recipeId: selectedRecipeId, source: "me-posts", tab: "Posts" },
    });
    handleCloseModals();
  };

  const handleOpenSavedRecipe = (id: string, type: RecipeType) => {
    const source =
      type === "User"
        ? "me-saves-community"
        : type === "Model"
          ? "me-saves-generated"
          : "me-saves-recommended";
    router.push({
      pathname: "/recipe/[recipeId]" as const,
      params: { recipeId: id, source, tab: "Saves" },
    });
  };

  const handleDeleteRecipe = () => {
    if (!selectedRecipeId || !canDeleteSelectedRecipe) return;

    if (Platform.OS === "web") {
      const confirmFn =
        typeof globalThis !== "undefined" &&
        typeof (globalThis as { confirm?: (message?: string) => boolean })
          .confirm === "function"
          ? (globalThis as { confirm?: (message?: string) => boolean }).confirm
          : undefined;
      const confirmed = confirmFn
        ? confirmFn("Delete this recipe permanently?")
        : true;
      if (!confirmed) {
        return;
      }
      deleteRecipeMutation.mutate(undefined, {
        onError: (error) =>
          Alert.alert("Delete failed", error.message ?? "Please try again."),
      });
      return;
    }

    Alert.alert(
      "Delete Recipe",
      "This action cannot be undone. Are you sure you want to delete this recipe?",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Delete",
          style: "destructive",
          onPress: () =>
            deleteRecipeMutation.mutate(undefined, {
              onError: (error) =>
                Alert.alert(
                  "Delete failed",
                  error.message ?? "Please try again.",
                ),
            }),
        },
      ],
    );
  };

  const myRecipes = useMemo(
    () => recipesResponse?.data ?? [],
    [recipesResponse],
  );
  // Only show user-created recipes in Posts (Public/Private)
  // Scanned recipes (type: Model) should only appear in Saves
  const publicRecipes = useMemo(
    () =>
      myRecipes.filter(
        (recipe) => recipe.visibility === "Public" && recipe.type === "User",
      ),
    [myRecipes],
  );
  const privateRecipes = useMemo(
    () =>
      myRecipes.filter(
        (recipe) => recipe.visibility === "Private" && recipe.type === "User",
      ),
    [myRecipes],
  );
  const selectedRecipeCard = useMemo(
    () => myRecipes.find((recipe) => recipe.id === selectedRecipeId) ?? null,
    [myRecipes, selectedRecipeId],
  );
  const isViewModalOpen = activeModal === "view";
  const isEditModalOpen = activeModal === "edit";
  const canDeleteSelectedRecipe = Boolean(
    selectedRecipeCard &&
    userProfile &&
    selectedRecipeCard.authorId &&
    selectedRecipeCard.authorId === userProfile.id,
  );

  const count = {
    published: myRecipes.length,
    saved: meSavesCountResponse?.data?.count ?? 0,
    liked: meLikesCountResponse?.data?.count ?? 0,
  };

  const tabs = [
    { name: "Posts", icon: "book-open" },
    { name: "Saves", icon: "bookmark" },
    { name: "Likes", icon: "heart" },
  ];

  const [activeTab, setActiveTab] = useState("Posts");
  const [postsMode, setPostsMode] = useState<"all" | "public" | "private">(
    "all",
  );

  const lastAppliedTabRef = useRef<string | null>(null);
  useEffect(() => {
    if (
      !tab ||
      typeof tab !== "string" ||
      !["Posts", "Notes", "Likes", "Saves"].includes(tab)
    ) {
      lastAppliedTabRef.current = null;
      return;
    }
    const normalizedTab = tab === "Notes" ? "Posts" : tab;
    if (lastAppliedTabRef.current === normalizedTab) return;
    lastAppliedTabRef.current = normalizedTab;
    setActiveTab(normalizedTab);
  }, [tab]);

  // Placeholder removed - all tabs now have content

  const preferences = userProfile?.preferences ?? [];
  const goals = preferences.filter((item) => item.relation === "Goal");
  const dietaryPreferences = preferences.filter(
    (item) => item.relation === "Like",
  );
  const allergyPreferences = preferences.filter(
    (item) => item.relation === "Allergy" || item.relation === "Restriction",
  );

  const formatMetric = (value: number | null | undefined, unit?: string) => {
    if (value === null || value === undefined) return "Not set";
    const numeric = Number(value);
    return unit ? `${numeric} ${unit}` : `${numeric}`;
  };

  const formatGender = (gender: UserGender | null | undefined) => {
    if (!gender) return "Not set";
    switch (gender) {
      case "Male":
        return "Male";
      case "Female":
        return "Female";
      case "Unknown":
        return "Unknown";
      case "NotApplicable":
        return "N/A";
      default:
        return gender;
    }
  };

  // Helper to determine if a color is light (for choosing contrasting text)
  const isLightColor = (hexColor: string): boolean => {
    const hex = hexColor.replace("#", "");
    const r = parseInt(hex.substring(0, 2), 16);
    const g = parseInt(hex.substring(2, 4), 16);
    const b = parseInt(hex.substring(4, 6), 16);
    // Using luminance formula
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    return luminance > 0.5;
  };

  const renderPill = (
    label: string,
    key: string | number,
    color?: string | null,
  ) => {
    const bgColor = color ?? colors.accent;
    const textColor = isLightColor(bgColor) ? "#000000" : "#ffffff";
    return (
      <View
        key={key}
        className="rounded-full px-3 py-1.5 min-h-[32px] justify-center"
        style={{ backgroundColor: bgColor }}
      >
        <Text className="text-sm font-medium" style={{ color: textColor }}>{label}</Text>
      </View>
    );
  };

  const renderInfoItem = (label: string, value: string | number) => (
    <View key={label} className="w-[48%] mb-2">
      <Text className="text-xs" style={{ color: colors.textSecondary }}>{label}</Text>
      <Text className="text-lg font-semibold mt-0.5" style={{ color: colors.textPrimary }}>{value}</Text>
    </View>
  );
  const goalAndDietary = [...goals, ...dietaryPreferences];

  const infoItems = [
    { label: "Age", value: formatMetric(userProfile?.age, "") },
    { label: "Gender", value: formatGender(userProfile?.gender) },
    { label: "Height", value: formatMetric(userProfile?.height, "cm") },
    { label: "Weight", value: formatMetric(userProfile?.weight, "kg") },
  ];

  // Calculate profile completion
  const profileCompletion = useMemo(() => {
    if (!userProfile) return { filled: 0, total: 6, percentage: 0 };

    let filled = 0;
    const total = 6; // age, gender, height, weight, goals/dietary, allergies

    // Check for positive values (0 means not set)
    if (userProfile.age != null && userProfile.age > 0) filled++;
    if (userProfile.gender && userProfile.gender !== "Unknown") filled++;
    if (userProfile.height != null && userProfile.height > 0) filled++;
    if (userProfile.weight != null && userProfile.weight > 0) filled++;
    if (goalAndDietary.length > 0) filled++;
    if (allergyPreferences.length > 0) filled++;

    return { filled, total, percentage: Math.round((filled / total) * 100) };
  }, [userProfile, goalAndDietary.length, allergyPreferences.length]);

  // Check if profile is mostly empty (less than 50% complete)
  const isProfileMostlyEmpty = profileCompletion.percentage < 50;

  const avatarUrl =
    userProfile?.avatarUrl || getDefaultAvatarUrl(userProfile?.id) || undefined;

  const renderRecipeDetailModal = () => (
    <Modal
      transparent
      statusBarTranslucent
      animationType="fade"
      visible={isViewModalOpen}
      onRequestClose={handleCloseModals}
    >
      <KeyboardAvoidingView
        behavior={keyboardBehavior}
        style={styles.modalContainer}
      >
        <View
          style={[
            styles.modalWrapper,
            {
              paddingTop: safeInsets.top + 16,
              paddingBottom: safeInsets.bottom + 16,
            },
          ]}
        >
          <Pressable style={styles.backdrop} onPress={handleCloseModals} />
          <View
            className="w-full max-w-[420px] rounded-[32px] overflow-hidden"
            style={[styles.modalCard, { maxHeight: detailDialogMaxHeight, backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }]}
            onLayout={logViewLayout("card")}
          >
            <View
              className="flex-row items-center justify-between px-5 py-4"
              style={{ borderBottomWidth: 1, borderBottomColor: colors.border }}
              onLayout={logViewLayout("header")}
            >
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                Recipe Details
              </Text>
              <TouchableOpacity
                onPress={handleCloseModals}
                className="h-8 w-8 items-center justify-center rounded-full"
                style={{ backgroundColor: colors.card }}
              >
                <Icon name="x" size={18} color={colors.textPrimary} />
              </TouchableOpacity>
            </View>

            <View style={styles.modalBody}>
              <ScrollView
                style={styles.modalScroll}
                contentContainerStyle={styles.modalScrollContent}
                showsVerticalScrollIndicator
                keyboardShouldPersistTaps="handled"
                onLayout={logViewLayout("scroll")}
              >
                <View className="pt-4 pb-2">
                  <RecipeDetail
                    recipeId={selectedRecipeId ?? undefined}
                    variant="modal"
                  />
                </View>
              </ScrollView>
            </View>

            <View
              className="px-5 pb-5 pt-4"
              style={{ borderTopWidth: 1, borderTopColor: colors.border }}
              onLayout={logViewLayout("footer")}
            >
              <TouchableOpacity
                onPress={handleStartCooking}
                disabled={!selectedRecipeId}
                className={`flex-row items-center justify-center rounded-full py-3 ${
                  !selectedRecipeId ? "opacity-60" : ""
                }`}
                style={{ backgroundColor: colors.accent }}
                activeOpacity={0.9}
              >
                <Text className="text-base font-semibold" style={{ color: colors.bg }}>
                  Start Cooking
                </Text>
              </TouchableOpacity>
              {canDeleteSelectedRecipe ? (
                <TouchableOpacity
                  onPress={handleDeleteRecipe}
                  disabled={!selectedRecipeId || deleteRecipeMutation.isPending}
                  className={`mt-3 flex-row items-center justify-center rounded-full py-3 ${
                    !selectedRecipeId || deleteRecipeMutation.isPending
                      ? "opacity-60"
                      : ""
                  }`}
                  style={{ borderWidth: 1, borderColor: colors.border }}
                  activeOpacity={0.9}
                >
                  {deleteRecipeMutation.isPending ? (
                    <ActivityIndicator color={colors.textPrimary} />
                  ) : (
                    <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                      Delete Recipe
                    </Text>
                  )}
                </TouchableOpacity>
              ) : null}
            </View>
          </View>
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );

  const renderRecipeEditModal = () => (
    <Modal
      transparent
      statusBarTranslucent
      animationType="fade"
      visible={isEditModalOpen}
      onRequestClose={handleCloseModals}
    >
      <KeyboardAvoidingView
        behavior={keyboardBehavior}
        style={styles.modalContainer}
      >
        <View
          style={[
            styles.modalWrapper,
            {
              paddingTop: safeInsets.top + 16,
              paddingBottom: safeInsets.bottom + 16,
            },
          ]}
        >
          <Pressable style={styles.backdrop} onPress={handleCloseModals} />
          <View
            className="w-full max-w-[520px] rounded-[32px] overflow-hidden"
            style={[styles.modalCard, { maxHeight: editDialogMaxHeight, backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }]}
            onLayout={logEditLayout("card")}
          >
            <View
              className="flex-row items-center justify-between px-5 py-4"
              style={{ borderBottomWidth: 1, borderBottomColor: colors.border }}
              onLayout={logEditLayout("header")}
            >
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                Edit Recipe
              </Text>
              <TouchableOpacity
                onPress={handleCloseModals}
                className="h-8 w-8 items-center justify-center rounded-full"
                style={{ backgroundColor: colors.card }}
              >
                <Icon name="x" size={18} color={colors.textPrimary} />
              </TouchableOpacity>
            </View>

            <View style={styles.modalBody}>
              <ScrollView
                style={styles.modalScroll}
                contentContainerStyle={styles.modalScrollContent}
                keyboardShouldPersistTaps="handled"
                showsVerticalScrollIndicator
                onLayout={logEditLayout("scroll")}
              >
                <View className="px-5 py-4">
                  {selectedRecipeId ? (
                    <RecipeEditForm
                      ref={editFormRef}
                      recipeId={selectedRecipeId}
                      onSaved={handleEditSaved}
                      onDeleted={handleEditDeleted}
                      hideActions
                      onStatusChange={handleEditStatusChange}
                    />
                  ) : (
                    <View className="items-center justify-center rounded-3xl px-4 py-8" style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}>
                      <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                        Choose a recipe to edit.
                      </Text>
                    </View>
                  )}
                </View>
              </ScrollView>
            </View>

            <View
              className="px-5 pb-5 pt-4"
              style={{ borderTopWidth: 1, borderTopColor: colors.border }}
              onLayout={logEditLayout("footer")}
            >
              <TouchableOpacity
                onPress={handleConfirmSave}
                disabled={
                  !selectedRecipeId ||
                  editStatus.isSaving ||
                  !editStatus.isReady
                }
                className={`flex-row items-center justify-center rounded-full py-3 ${
                  !selectedRecipeId || editStatus.isSaving ? "opacity-60" : ""
                }`}
                style={{ backgroundColor: colors.accent }}
                activeOpacity={0.9}
              >
                {editStatus.isSaving ? (
                  <ActivityIndicator color={colors.bg} />
                ) : (
                  <Text className="text-base font-semibold" style={{ color: colors.bg }}>
                    Save Changes
                  </Text>
                )}
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );

  return (
    <SafeAreaView
      style={{ flex: 1, backgroundColor: colors.bg }}
      edges={["right", "left", "top"]}
    >
      <ScrollView
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={handleRefresh}
            tintColor={colors.accent}
            colors={[colors.accent]}
          />
        }
      >
        <View className="flex flex-row items-center justify-between p-6">
          {isLoading ? (
            <View className="flex flex-row items-center gap-4 flex-1">
              <Skeleton className="w-16 h-16 rounded-full" />
              <View className="flex gap-2">
                <Skeleton className="w-32 h-5 rounded-md" />
                <Skeleton className="w-48 h-4 rounded-md" />
              </View>
            </View>
          ) : (
            <View className="flex flex-row items-center gap-4 flex-1">
              <Avatar className="size-16">
                {avatarUrl ? (
                  <AvatarImage source={{ uri: avatarUrl }} alt="User avatar" />
                ) : null}
                <AvatarFallback style={{ backgroundColor: colors.accent }}>
                  <Text className="text-3xl" style={{ color: colors.bg }}>{userProfile?.nickname?.[0]?.toUpperCase() || "U"}</Text>
                </AvatarFallback>
              </Avatar>
              <View className="flex-1">
                <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>
                  {userProfile?.nickname || "User"}
                </Text>
                <Text className="text-sm" style={{ color: colors.textSecondary }} numberOfLines={1}>
                  {userProfile?.email || "User@user.com"}
                </Text>
              </View>
            </View>
          )}
          <TouchableOpacity
            className="w-10 h-10 rounded-full flex items-center justify-center transition-all"
            onPress={() => router.push("/settings")}
          >
            <Icon name="settings" size={24} color={colors.accent} />
          </TouchableOpacity>
        </View>

        {isLoading ? (
          // Loading skeleton for profile cards
          <>
            <Card className="mt-1 p-3">
              <CardTitle>Basic Info</CardTitle>
              <CardContent>
                <View className="flex-row flex-wrap gap-3">
                  {Array.from({ length: 4 }).map((_, index) => (
                    <Skeleton
                      key={`info-skeleton-${index}`}
                      className="h-12 w-[48%] rounded-lg"
                    />
                  ))}
                </View>
              </CardContent>
            </Card>
            <Card className="mb-4 p-3">
              <Skeleton className="h-8 w-40 rounded-lg mb-2" />
              <Skeleton className="h-6 w-32 rounded-full" />
            </Card>
          </>
        ) : isProfileMostlyEmpty ? (
          // Friendly "Complete Your Profile" prompt when profile is mostly empty
          <TouchableOpacity
            onPress={() => router.push({ pathname: "/updateprofile", params: { source: "profile" } })}
            activeOpacity={0.9}
            className="mx-4 mt-1 mb-4"
          >
            <View
              className="rounded-2xl p-5"
              style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
            >
              {/* Progress icon */}
              <View className="flex-row items-center gap-4">
                <View
                  className="w-14 h-14 rounded-full items-center justify-center"
                  style={{ backgroundColor: `${colors.accent}15` }}
                >
                  <Icon name="edit-3" size={24} color={colors.accent} />
                </View>
                <View className="flex-1">
                  <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                    Complete Your Profile
                  </Text>
                  <Text className="text-sm mt-0.5" style={{ color: colors.textSecondary }}>
                    {profileCompletion.percentage}% complete
                  </Text>
                </View>
                <Icon name="chevron-right" size={20} color={colors.textMuted} />
              </View>

              {/* Progress bar */}
              <View className="mt-4 h-2 rounded-full overflow-hidden" style={{ backgroundColor: `${colors.accent}20` }}>
                <View
                  className="h-full rounded-full"
                  style={{
                    backgroundColor: colors.accent,
                    width: `${profileCompletion.percentage}%`,
                  }}
                />
              </View>

              {/* Hint text */}
              <Text className="text-xs mt-3" style={{ color: colors.textMuted }}>
                Add your dietary preferences, allergies, and health goals for personalized recipe recommendations
              </Text>
            </View>
          </TouchableOpacity>
        ) : (
          // Normal profile cards when info is filled in
          <>
            <Card className="mt-1 p-3">
              <CardTitle>Basic Info</CardTitle>
              <CardContent>
                <View className="flex-row flex-wrap justify-between">
                  {infoItems.map(({ label, value }) =>
                    renderInfoItem(label, value),
                  )}
                </View>
              </CardContent>
            </Card>

            {goalAndDietary.length > 0 && (
              <Card className="p-3">
                <CardTitle>Goal / Dietary Preferences</CardTitle>
                <CardContent>
                  <ScrollView
                    horizontal
                    showsHorizontalScrollIndicator={false}
                    contentContainerStyle={{ gap: 8 }}
                  >
                    {goalAndDietary.map((item) =>
                      renderPill(
                        item.tagDisplayName || item.tagName,
                        item.tagId,
                        item.tagColor,
                      ),
                    )}
                  </ScrollView>
                </CardContent>
              </Card>
            )}

            {allergyPreferences.length > 0 && (
              <Card className="mb-4 p-3">
                <CardTitle>Allergies / Dietary Restrictions</CardTitle>
                <CardContent>
                  <ScrollView
                    horizontal
                    showsHorizontalScrollIndicator={false}
                    contentContainerStyle={{ gap: 8 }}
                  >
                    {allergyPreferences.map((item) =>
                      renderPill(
                        item.tagDisplayName || item.tagName,
                        item.tagId,
                        item.tagColor,
                      ),
                    )}
                  </ScrollView>
                </CardContent>
              </Card>
            )}

            {/* Show completion prompt if profile not 100% complete */}
            {profileCompletion.percentage < 100 && (
              <TouchableOpacity
                onPress={() => router.push({ pathname: "/updateprofile", params: { source: "profile" } })}
                activeOpacity={0.9}
                className="mx-4 mb-4"
              >
                <View
                  className="rounded-xl p-3 flex-row items-center gap-3"
                  style={{ backgroundColor: `${colors.accent}10`, borderWidth: 1, borderColor: `${colors.accent}30` }}
                >
                  <View
                    className="w-8 h-8 rounded-full items-center justify-center"
                    style={{ backgroundColor: `${colors.accent}20` }}
                  >
                    <Icon name="plus" size={16} color={colors.accent} />
                  </View>
                  <View className="flex-1">
                    <Text className="text-sm font-medium" style={{ color: colors.textPrimary }}>
                      Add more details
                    </Text>
                    <Text className="text-xs" style={{ color: colors.textSecondary }}>
                      {profileCompletion.percentage}% complete
                    </Text>
                  </View>
                  <Icon name="chevron-right" size={16} color={colors.accent} />
                </View>
              </TouchableOpacity>
            )}
          </>
        )}

        <Card>
          <CardContent className="flex flex-row justify-center gap-2">
            {[
              { value: count.published, label: "Published", color: colors.accent },
              { value: count.saved, label: "Saved", color: colors.success },
              { value: count.liked, label: "Liked", color: colors.error },
            ].map((item) => (
              <View
                key={item.label}
                className="flex-1 backdrop-blur-sm rounded-xl items-center justify-center min-w-0"
                style={{ backgroundColor: `${item.color}20`, borderWidth: 1, borderColor: `${item.color}40` }}
              >
                <StatCard
                  value={item.value}
                  label={item.label}
                  valueStyle={{ color: item.color }}
                  className="p-2"
                />
              </View>
            ))}
          </CardContent>
        </Card>

        <ToggleGroup
          type="single"
          value={activeTab}
          onValueChange={(value) => {
            // Prevent deselection - always keep one tab selected
            if (value) setActiveTab(value);
          }}
          variant="default"
          size="default"
          className="w-full h-14"
          style={{ borderTopWidth: 1, borderBottomWidth: 1, borderColor: colors.border }}
        >
          {tabs.map((tab) => (
            <ToggleGroupItem
              key={tab.name}
              value={tab.name}
              className="bg-transparent border-0"
            >
              <View
                style={{
                  borderBottomWidth: activeTab === tab.name ? 2 : 0,
                  borderBottomColor: colors.accent,
                  paddingBottom: 4,
                }}
              >
                <IconBadge
                  iconSet="MaterialCommunityIcons"
                  iconName={tab.icon}
                  iconSize={20}
                  iconColor={activeTab === tab.name ? colors.accent : colors.textMuted}
                  textClassName="text-base font-medium"
                  style={{ color: activeTab === tab.name ? colors.accent : colors.textMuted }}
                >
                  {tab.name}
                </IconBadge>
              </View>
            </ToggleGroupItem>
          ))}
        </ToggleGroup>

        <View className="flex-1 mb-4" style={{ minHeight: windowHeight }}>
          {activeTab === "Posts" ? (
            <PostsTab
              loading={recipesLoading}
              error={recipesError ? recipesErrorObject : null}
              allRecipes={myRecipes.filter((r) => r.type === "User")}
              publicRecipes={publicRecipes}
              privateRecipes={privateRecipes}
              mode={postsMode}
              onChangeMode={setPostsMode}
              onRefresh={refetchRecipes}
              onViewRecipe={handleOpenRecipeDetail}
              onEditRecipe={handleOpenRecipeEdit}
            />
          ) : activeTab === "Likes" ? (
            <LikesTab
              onOpenRecipe={(id) =>
                router.push({
                  pathname: "/recipe/[recipeId]" as const,
                  params: { recipeId: id, source: "me-likes", tab: "Likes" },
                })
              }
            />
          ) : activeTab === "Saves" ? (
            <SavesTab onOpenRecipe={handleOpenSavedRecipe} />
          ) : (
            renderPlaceholder()
          )}
        </View>
      </ScrollView>

      {renderRecipeDetailModal()}
      {renderRecipeEditModal()}
    </SafeAreaView>
  );
}

function renderPlaceholder() {
  const { colors } = useTheme();
  return (
    <View className="flex-1 items-center justify-center rounded-3xl px-4 py-10" style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}>
      <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Nothing here yet</Text>
      <Text className="mt-2 text-xs" style={{ color: colors.textMuted }}>
        Switch tabs to view your posts, likes, or saves.
      </Text>
    </View>
  );
}

type PostsTabProps = {
  loading: boolean;
  error: Error | null;
  allRecipes: RecipeCardDto[];
  publicRecipes: RecipeCardDto[];
  privateRecipes: RecipeCardDto[];
  mode: "all" | "public" | "private";
  onChangeMode: (mode: "all" | "public" | "private") => void;
  onRefresh: () => void;
  onViewRecipe: (id: string) => void;
  onEditRecipe: (id: string) => void;
};

function PostsTab({
  loading,
  error,
  allRecipes,
  publicRecipes,
  privateRecipes,
  mode,
  onChangeMode,
  onRefresh,
  onViewRecipe,
  onEditRecipe,
}: PostsTabProps) {
  const { colors } = useTheme();
  const filterOptions: {
    key: "all" | "private" | "public";
    label: string;
    icon?: string;
  }[] = [
    { key: "all", label: "All" },
    { key: "private", label: "Private", icon: "lock" },
    { key: "public", label: "Public", icon: "globe" },
  ];

  // Get recipes based on current filter
  const filteredRecipes =
    mode === "all"
      ? allRecipes
      : mode === "private"
        ? privateRecipes
        : publicRecipes;

  if (error) {
    return (
      <View className="rounded-3xl border px-4 py-6" style={{ borderColor: `${colors.error}40`, backgroundColor: `${colors.error}10` }}>
        <Text className="text-base font-semibold" style={{ color: colors.error }}>
          Unable to load your posts
        </Text>
        <Text className="mt-1 text-sm" style={{ color: `${colors.error}CC` }}>
          {error.message || "Please pull to refresh and try again."}
        </Text>
        <TouchableOpacity
          onPress={onRefresh}
          className="mt-4 rounded-full px-4 py-2"
          style={{ backgroundColor: colors.card }}
        >
          <Text className="text-center text-sm font-semibold" style={{ color: colors.textPrimary }}>
            Retry
          </Text>
        </TouchableOpacity>
      </View>
    );
  }

  const getEmptyMessage = () => {
    switch (mode) {
      case "private":
        return {
          title: "No private posts yet.",
          subtitle: "Recipes you publish as Private will appear here.",
        };
      case "public":
        return {
          title: "No public posts yet.",
          subtitle:
            "Share a recipe with visibility set to Public to see it here.",
        };
      default:
        return {
          title: "No posts yet.",
          subtitle: "Create a recipe to see it here.",
        };
    }
  };

  return (
    <FlatList
      data={loading ? [] : filteredRecipes}
      scrollEnabled={false}
      keyExtractor={(item) => item.id}
      contentContainerStyle={{
        paddingTop: 16,
        paddingBottom: 16,
        paddingHorizontal: 16,
      }}
      ListHeaderComponent={
        <View>
          <View className="flex-row gap-2 px-2">
            {filterOptions.map((option) => {
              const isActive = mode === option.key;
              const iconColor = isActive ? colors.bg : colors.textSecondary;

              return (
                <TouchableOpacity
                  key={option.key}
                  activeOpacity={0.85}
                  onPress={() => onChangeMode(option.key)}
                  className="rounded-full border px-3 py-1.5 flex-row items-center gap-1.5"
                  style={{
                    borderColor: isActive ? colors.accent : colors.border,
                    backgroundColor: isActive ? colors.accent : colors.card,
                  }}
                >
                  {option.icon && (
                    <Icon name={option.icon} size={14} color={iconColor} />
                  )}
                  <Text
                    className="text-xs font-semibold"
                    style={{ color: isActive ? colors.bg : colors.textSecondary }}
                  >
                    {option.label}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>
          <View className="h-3" />
        </View>
      }
      ListEmptyComponent={
        !loading ? (
          <View className="rounded-3xl border border-dashed px-4 py-6" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
            <Text className="text-center text-base font-semibold" style={{ color: colors.textPrimary }}>
              {getEmptyMessage().title}
            </Text>
            <Text className="mt-2 text-center text-sm" style={{ color: colors.textSecondary }}>
              {getEmptyMessage().subtitle}
            </Text>
          </View>
        ) : (
          <View className="rounded-3xl px-4 py-6" style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}>
            <Text className="text-center text-sm" style={{ color: colors.textSecondary }}>
              Loading posts...
            </Text>
          </View>
        )
      }
      renderItem={({ item }) => (
        <RecipeNoteCard
          recipe={item}
          onView={onViewRecipe}
          onEdit={onEditRecipe}
        />
      )}
      refreshing={loading}
      onRefresh={onRefresh}
    />
  );
}

type LikesTabProps = {
  onOpenRecipe: (id: string) => void;
};

function LikesTab({ onOpenRecipe }: LikesTabProps) {
  const { colors } = useTheme();
  const queryClient = useQueryClient();
  const pageSize = DEFAULT_ME_LIKES_PAGE_SIZE;
  const { data, isLoading, isError, error, refetch } = useAuthQuery<
    ApiResponse<MyLikedRecipeCardDto[]>
  >(
    ["me-likes", "page:1", `pageSize:${pageSize}`],
    `/api/me/likes?page=1&pageSize=${pageSize}`,
    { staleTime: 0 },
  );

  const likedRecipes = useMemo(() => data?.data ?? [], [data]);

  useEffect(() => {
    const nextIds = likedRecipes.map((recipe) => recipe.id);
    const previous =
      queryClient.getQueryData<string[]>(likedRecipeIdsKey) ?? [];
    setLikedRecipeIds(queryClient, [...previous, ...nextIds]);
  }, [likedRecipes, queryClient]);

  if (isLoading) {
    return (
      <View className="mt-6 rounded-3xl px-4 py-6" style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}>
        <Text className="text-center text-sm font-semibold" style={{ color: colors.textSecondary }}>
          Loading likes...
        </Text>
      </View>
    );
  }

  if (isError) {
    return (
      <View className="mt-6 rounded-3xl border px-4 py-6" style={{ borderColor: `${colors.error}40`, backgroundColor: `${colors.error}10` }}>
        <Text className="text-center text-base font-semibold" style={{ color: colors.error }}>
          Unable to load likes
        </Text>
        <Text className="mt-2 text-center text-sm" style={{ color: `${colors.error}CC` }}>
          {error?.message ?? "Please try again."}
        </Text>
        <TouchableOpacity
          onPress={() => refetch()}
          className="mt-4 rounded-full px-4 py-2"
          style={{ backgroundColor: colors.card }}
        >
          <Text className="text-center text-sm font-semibold" style={{ color: colors.textPrimary }}>
            Retry
          </Text>
        </TouchableOpacity>
      </View>
    );
  }

  if (likedRecipes.length === 0) {
    return (
      <View className="mt-6 rounded-3xl border border-dashed px-4 py-6" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
        <Text className="text-center text-base font-semibold" style={{ color: colors.textSecondary }}>
          No liked recipes yet.
        </Text>
        <Text className="mt-2 text-center text-sm" style={{ color: colors.textMuted }}>
          Tap the heart on a recipe to add it here.
        </Text>
      </View>
    );
  }

  return (
    <FlatList
      data={likedRecipes}
      scrollEnabled={false}
      keyExtractor={(item) => item.id}
      contentContainerStyle={{ paddingHorizontal: 16, paddingTop: 16 }}
      renderItem={({ item }) => {
        // Check for valid cover image - must be a valid http(s) URL
        const rawCover = item.coverImageUrl?.trim();
        const cover = rawCover && (rawCover.startsWith('http://') || rawCover.startsWith('https://')) ? rawCover : null;
        const author = item.authorName?.trim() ? item.authorName : "Unknown";

        // Different layout for items with and without images
        if (cover) {
          return (
            <TouchableOpacity
              onPress={() => onOpenRecipe(item.id)}
              activeOpacity={0.9}
              className="mb-3 flex-row gap-3 rounded-2xl p-3"
              style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}
            >
              <Image
                source={{ uri: cover }}
                className="h-20 w-20 rounded-xl"
                style={{ backgroundColor: colors.card }}
                resizeMode="cover"
              />
              <View className="flex-1 justify-center">
                <Text
                  className="text-base font-semibold"
                  style={{ color: colors.textPrimary }}
                  numberOfLines={2}
                >
                  {item.title}
                </Text>
                <Text className="mt-1 text-sm" style={{ color: colors.textSecondary }} numberOfLines={1}>
                  {author}
                </Text>
              </View>
            </TouchableOpacity>
          );
        }

        // Layout for items without images - text takes full width
        return (
          <TouchableOpacity
            onPress={() => onOpenRecipe(item.id)}
            activeOpacity={0.9}
            className="mb-3 rounded-2xl p-4"
            style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}
          >
            <Text
              className="text-base font-semibold"
              style={{ color: colors.textPrimary }}
              numberOfLines={2}
            >
              {item.title}
            </Text>
            <Text className="mt-1 text-sm" style={{ color: colors.textSecondary }} numberOfLines={1}>
              {author}
            </Text>
          </TouchableOpacity>
        );
      }}
    />
  );
}

type SavesTabProps = {
  onOpenRecipe: (id: string, type: RecipeType) => void;
};

function SavesTab({ onOpenRecipe }: SavesTabProps) {
  const { colors } = useTheme();
  const queryClient = useQueryClient();
  const pageSize = DEFAULT_ME_SAVES_PAGE_SIZE;
  const [activeFilter, setActiveFilter] = useState<
    "all" | "recommended" | "community" | "generated"
  >("all");
  const [filterRowWidth, setFilterRowWidth] = useState(0);
  const [filterRowContentWidth, setFilterRowContentWidth] = useState(0);
  const isFilterRowScrollable = filterRowContentWidth > filterRowWidth;

  // Build API URL with category filter for server-side filtering
  const apiUrl =
    activeFilter === "all"
      ? `/api/me/saves?page=1&pageSize=${pageSize}`
      : `/api/me/saves?page=1&pageSize=${pageSize}&category=${activeFilter}`;

  const { data, isLoading, isError, error, isFetching, refetch } = useAuthQuery<
    ApiResponse<MySavedRecipeCardDto[]>
  >(
    ["me-saves", "page:1", `pageSize:${pageSize}`, `category:${activeFilter}`],
    apiUrl,
    { staleTime: 0 },
  );

  const savedRecipes = data?.data ?? [];

  // No need for client-side filtering - server now handles it
  const filteredRecipes = savedRecipes;

  const filterOptions = [
    { key: "all", label: "All" },
    { key: "recommended", label: "Recommended" },
    { key: "community", label: "Community" },
    { key: "generated", label: "Generated" },
  ] as const;

  const filterIcons: Partial<
    Record<
      (typeof filterOptions)[number]["key"],
      { iconSet: "MaterialCommunityIcons"; iconName: string }
    >
  > = {
    recommended: {
      iconSet: "MaterialCommunityIcons",
      iconName: "star-outline",
    },
    community: {
      iconSet: "MaterialCommunityIcons",
      iconName: "account-group-outline",
    },
    generated: { iconSet: "MaterialCommunityIcons", iconName: "robot-outline" },
  };

  useEffect(() => {
    savedRecipes.forEach((recipe) => {
      upsertSavedRecipeId(queryClient, recipe.id, true);
    });
  }, [queryClient, savedRecipes]);

  if (isLoading) {
    return (
      <View className="mt-6 rounded-3xl px-4 py-6" style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}>
        <Text className="text-center text-sm font-semibold" style={{ color: colors.textSecondary }}>
          Loading saves...
        </Text>
      </View>
    );
  }

  if (isError) {
    return (
      <View className="mt-6 rounded-3xl border px-4 py-6" style={{ borderColor: `${colors.error}40`, backgroundColor: `${colors.error}10` }}>
        <Text className="text-center text-base font-semibold" style={{ color: colors.error }}>
          Unable to load saves
        </Text>
        <Text className="mt-2 text-center text-sm" style={{ color: `${colors.error}CC` }}>
          {error?.message ?? "Please try again."}
        </Text>
        <TouchableOpacity
          onPress={() => refetch()}
          className="mt-4 rounded-full px-4 py-2"
          style={{ backgroundColor: colors.card }}
        >
          <Text className="text-center text-sm font-semibold" style={{ color: colors.textPrimary }}>
            Retry
          </Text>
        </TouchableOpacity>
      </View>
    );
  }

  const renderEmptyState = () => (
    <View className="mt-6 rounded-3xl border border-dashed px-4 py-8" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
      <View className="items-center gap-2">
        <Icon name="bookmark" size={32} color={colors.textMuted} />
        <Text className="text-center text-base font-semibold" style={{ color: colors.textPrimary }}>
          No recipes saved yet
        </Text>
        <Text className="text-center text-sm" style={{ color: colors.textSecondary }}>
          Save recipes by clicking the save icon!
        </Text>
      </View>
    </View>
  );

  return (
    <FlatList
      data={filteredRecipes}
      scrollEnabled={false}
      keyExtractor={(item) => item.id}
      contentContainerStyle={{ paddingHorizontal: 16 }}
      refreshing={isFetching}
      onRefresh={refetch}
      ListHeaderComponent={
        <View>
          <ScrollView
            horizontal
            onLayout={(event) => {
              setFilterRowWidth(event.nativeEvent.layout.width);
            }}
            onContentSizeChange={(width) => {
              setFilterRowContentWidth(width);
            }}
            scrollEnabled={isFilterRowScrollable}
            showsHorizontalScrollIndicator={false}
            className="mt-4"
          >
            <View className="flex-row justify-between gap-1">
              {filterOptions.map((option) => {
                const isActive = activeFilter === option.key;
                const icon = filterIcons[option.key];
                const iconColor = isActive
                  ? colors.bg
                  : colors.textSecondary;

                return (
                  <TouchableOpacity
                    key={option.key}
                    activeOpacity={0.85}
                    onPress={() => setActiveFilter(option.key)}
                    className="rounded-full border px-3 py-1.5 flex-row items-center gap-1.5"
                    style={{
                      borderColor: isActive ? colors.accent : colors.border,
                      backgroundColor: isActive ? colors.accent : colors.card,
                    }}
                  >
                    {icon ? (
                      <IconBadge
                        iconSet={icon.iconSet}
                        iconName={icon.iconName}
                        iconSize={14}
                        iconColor={iconColor}
                        textClassName="text-xs font-semibold"
                        className="items-center"
                        style={{ color: isActive ? colors.bg : colors.textSecondary }}
                      >
                        {option.label}
                      </IconBadge>
                    ) : (
                      <Text
                        className="text-xs font-semibold"
                        style={{ color: isActive ? colors.bg : colors.textSecondary }}
                      >
                        {option.label}
                      </Text>
                    )}
                  </TouchableOpacity>
                );
              })}
            </View>
          </ScrollView>
          <View className="h-3" />
        </View>
      }
      ListEmptyComponent={renderEmptyState}
      renderItem={({ item }) => {
        // Check for valid cover image - must be a valid http(s) URL
        const rawCover = item.coverImageUrl?.trim();
        const isValidUrl =
          rawCover && (rawCover.startsWith("http://") || rawCover.startsWith("https://"));
        const cover = isValidUrl ? rawCover : null;
        const author = item.authorName?.trim() ? item.authorName : "Unknown";
        // Badge colors for different recipe types
        const source =
          item.type === "Model"
            ? {
                label: "Generated",
                icon: {
                  iconSet: "MaterialCommunityIcons" as const,
                  iconName: "robot-outline",
                },
                bgColor: "#8B5CF6", // Purple for AI-generated
                textColor: "#FFFFFF",
              }
            : item.type === "System"
              ? {
                  label: "Recommended",
                  icon: {
                    iconSet: "MaterialCommunityIcons" as const,
                    iconName: "star-outline",
                  },
                  bgColor: "#F59E0B", // Amber for recommended
                  textColor: "#FFFFFF",
                }
              : {
                  label: "Community",
                  icon: {
                    iconSet: "MaterialCommunityIcons" as const,
                    iconName: "account-group-outline",
                  },
                  bgColor: "#3B82F6", // Blue for community
                  textColor: "#FFFFFF",
                };

        // Different layout for items with and without images
        if (cover) {
          return (
            <TouchableOpacity
              onPress={() => onOpenRecipe(item.id, item.type)}
              activeOpacity={0.9}
              className="mb-3 flex-row gap-3 rounded-2xl p-3"
              style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}
            >
              <Image
                source={{ uri: cover }}
                className="h-20 w-20 rounded-xl"
                style={{ backgroundColor: colors.card }}
                resizeMode="cover"
              />
              <View className="flex-1 justify-center">
                <Text
                  className="text-base font-semibold"
                  style={{ color: colors.textPrimary }}
                  numberOfLines={2}
                >
                  {item.title}
                </Text>
                {item.type === "User" && (
                  <Text className="mt-1 text-sm" style={{ color: colors.textSecondary }} numberOfLines={1}>
                    {author}
                  </Text>
                )}
              </View>
              <View className="justify-center">
                <View className="rounded-full px-3 py-1.5" style={{ backgroundColor: source.bgColor }}>
                  <IconBadge
                    iconSet={source.icon.iconSet}
                    iconName={source.icon.iconName}
                    iconSize={14}
                    iconColor={source.textColor}
                    textClassName="text-xs font-semibold"
                    className="items-center"
                    style={{ color: source.textColor }}
                  >
                    {source.label}
                  </IconBadge>
                </View>
              </View>
            </TouchableOpacity>
          );
        }

        // Layout for items without images - text takes full width
        return (
          <TouchableOpacity
            onPress={() => onOpenRecipe(item.id, item.type)}
            activeOpacity={0.9}
            className="mb-3 rounded-2xl p-4"
            style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}
          >
            <View className="flex-row items-center justify-between">
              <View className="flex-1 mr-3">
                <Text
                  className="text-base font-semibold"
                  style={{ color: colors.textPrimary }}
                  numberOfLines={2}
                >
                  {item.title}
                </Text>
                {item.type === "User" && (
                  <Text className="mt-1 text-sm" style={{ color: colors.textSecondary }} numberOfLines={1}>
                    {author}
                  </Text>
                )}
              </View>
              <View className="rounded-full px-3 py-1.5" style={{ backgroundColor: source.bgColor }}>
                <IconBadge
                  iconSet={source.icon.iconSet}
                  iconName={source.icon.iconName}
                  iconSize={14}
                  iconColor={source.textColor}
                  textClassName="text-xs font-semibold"
                  className="items-center"
                  style={{ color: source.textColor }}
                >
                  {source.label}
                </IconBadge>
              </View>
            </View>
          </TouchableOpacity>
        );
      }}
    />
  );
}

const styles = StyleSheet.create({
  modalContainer: { flex: 1 },
  modalWrapper: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    paddingHorizontal: 20,
  },
  backdrop: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: "rgba(0,0,0,0.5)",
  },
  modalCard: {
    width: "100%",
    flexShrink: 1,
    flex: 1,
  },
  modalBody: {
    flex: 1,
    minHeight: 0,
  },
  modalScroll: {
    flex: 1,
  },
  modalScrollContent: {
    paddingBottom: 32,
    paddingHorizontal: 20,
    paddingTop: 16,
    flexGrow: 1,
  },
});
