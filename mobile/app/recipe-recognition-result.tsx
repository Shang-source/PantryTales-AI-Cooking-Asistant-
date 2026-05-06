import React, { useState, useMemo, useEffect } from "react";
import {
  View,
  Text,
  TouchableOpacity,
  ScrollView,
  Image,
  ActivityIndicator,
  TextInput,
  KeyboardAvoidingView,
  Platform,
} from "react-native";
import { Stack, useLocalSearchParams, router } from "expo-router";
import { SafeAreaView } from "react-native-safe-area-context";
import {
  X,
  Check,
  Clock,
  Users,
  ShoppingCart,
  ChefHat,
  ImageIcon,
  Gauge,
  Save,
  Plus,
  Trash2,
  Edit3,
  Tag,
  RefreshCw,
  Globe2,
} from "lucide-react-native";
import {
  RecognizedRecipe,
  RecipeIngredient,
} from "@/hooks/useVisionRecognition";
import {
  useInventoryItems,
  findInventoryMatch,
} from "@/hooks/useInventoryItems";
import { useAddChecklistBatch } from "@/hooks/useChecklist";
import { useAuthMutation, fetcher, useImageUpload } from "@/hooks/useApi";
import { useAuth } from "@clerk/clerk-expo";
import { ApiResponse } from "@/types/api";
import {
  CreateRecipeRequest,
  RecipeDetailDto,
  RecipeDifficulty,
} from "@/types/recipes";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogAction,
} from "@/components/alert-dialog";
import { useRecipeDraft, DraftIngredient } from "@/hooks/useRecipeDraft";
import { useTheme } from "@/contexts/ThemeContext";

interface IngredientWithStatus extends DraftIngredient {
  isAvailable: boolean;
  availableAmount: number;
  selected: boolean;
}

interface DialogState {
  open: boolean;
  title: string;
  message: string;
  navigateOnClose?: string;
}

export default function RecipeRecognitionResultScreen() {
  const { recipe: recipeParam, imageUri: imageUriParam } =
    useLocalSearchParams<{
      recipe: string;
      imageUri?: string;
    }>();

  const { getToken } = useAuth();
  const { colors } = useTheme();

  // Draft management hook
  const {
    draft,
    isLoading: isDraftLoading,
    initDraft,
    updateTitle,
    updateServings,
    updateCookTime,
    addTag,
    removeTag,
    addIngredient,
    updateIngredient,
    removeIngredient,
    addStep,
    updateStep,
    removeStep,
    clearDraft,
  } = useRecipeDraft();

  // Dialog state
  const [dialog, setDialog] = useState<DialogState>({
    open: false,
    title: "",
    message: "",
  });

  // Edit mode states
  const [editingTitle, setEditingTitle] = useState(false);
  const [editingServings, setEditingServings] = useState(false);
  const [editingTime, setEditingTime] = useState(false);
  const [newTag, setNewTag] = useState("");
  const [showTagInput, setShowTagInput] = useState(false);
  const [editingIngredientId, setEditingIngredientId] = useState<string | null>(
    null,
  );
  const [editingStepIndex, setEditingStepIndex] = useState<number | null>(null);
  const [showAddIngredient, setShowAddIngredient] = useState(false);
  const [showAddStep, setShowAddStep] = useState(false);
  const [newStepText, setNewStepText] = useState("");
  const [newIngredient, setNewIngredient] = useState({
    name: "",
    quantity: "",
    unit: "",
  });

  // Parse recipe from params - memoize to prevent infinite re-renders
  const parsedRecipe: RecognizedRecipe | null = useMemo(() => {
    if (!recipeParam) return null;
    try {
      return JSON.parse(recipeParam);
    } catch {
      return null;
    }
  }, [recipeParam]);

  // Initialize draft when recipe is parsed (and no existing draft)
  useEffect(() => {
    if (parsedRecipe && !draft && !isDraftLoading) {
      initDraft(parsedRecipe, imageUriParam);
    }
  }, [parsedRecipe, draft, isDraftLoading, initDraft, imageUriParam]);

  // Fetch inventory for comparison
  const { data: inventoryData } = useInventoryItems();
  const inventoryItems = useMemo(
    () => inventoryData?.data?.data ?? [],
    [inventoryData],
  );

  // Process ingredients with inventory comparison
  const ingredientsWithStatus: IngredientWithStatus[] = useMemo(() => {
    if (!draft) return [];
    return draft.ingredients.map((ing) => {
      const match = findInventoryMatch(ing.name, inventoryItems);
      const hasEnough = match ? match.availableAmount >= ing.quantity : false;
      return {
        ...ing,
        isAvailable: hasEnough,
        availableAmount: match?.availableAmount ?? 0,
        selected: !hasEnough,
      };
    });
  }, [draft, inventoryItems]);

  const [selectedIngredients, setSelectedIngredients] = useState<Set<string>>(
    new Set(),
  );

  // Update selections when ingredients change
  useEffect(() => {
    const newSelected = new Set<string>();
    ingredientsWithStatus.forEach((ing) => {
      if (!ing.isAvailable) {
        newSelected.add(ing.id);
      }
    });
    setSelectedIngredients(newSelected);
  }, [ingredientsWithStatus]);

  // Checklist mutation
  const addToChecklist = useAddChecklistBatch();

  // Recipe save state and mutation
  const [isSaved, setIsSaved] = useState(false);
  const [savedRecipeId, setSavedRecipeId] = useState<string | null>(null);
  const imageUpload = useImageUpload();
  const saveRecipeMutation = useAuthMutation<
    ApiResponse<RecipeDetailDto>,
    CreateRecipeRequest
  >("/api/recipes");
  const isSaving = saveRecipeMutation.isPending || imageUpload.isPending;

  const handleSaveRecipe = async (
    showDialog = true,
    publishToPublic = false,
  ): Promise<string | null> => {
    if (isSaved && savedRecipeId && !publishToPublic) return savedRecipeId;
    if (isSaving || !draft) return null;

    const rawImageUri = draft.imageUri?.trim();
    let uploadedImageUrl: string | undefined;

    if (rawImageUri) {
      const isRemote =
        rawImageUri.startsWith("http://") ||
        rawImageUri.startsWith("https://");

      if (isRemote) {
        uploadedImageUrl = rawImageUri;
      } else {
        try {
          const uploadResult = await imageUpload.mutateAsync(rawImageUri);
          uploadedImageUrl = uploadResult.url;
        } catch (error: any) {
          if (showDialog) {
            setDialog({
              open: true,
              title: "Image Upload Failed",
              message: error?.message || "Could not upload the scanned image.",
            });
          }
          return null;
        }
      }
    }

    const difficultyMap: Record<string, RecipeDifficulty> = {
      Easy: "Easy",
      Medium: "Medium",
      Hard: "Hard",
    };

    const payload: CreateRecipeRequest = {
      title: draft.title,
      description: draft.description ?? "",
      steps: draft.steps,
      visibility: publishToPublic ? "Public" : "Private",
      type: "Model",
      servings: draft.servings,
      totalTimeMinutes: totalTime > 0 ? totalTime : null,
      difficulty: difficultyMap[draft.difficulty ?? "None"] ?? "None",
      imageUrls: uploadedImageUrl ? [uploadedImageUrl] : undefined,
      tags: draft.tags.length > 0 ? draft.tags : undefined,
    };

    try {
      const result = await saveRecipeMutation.mutateAsync(payload);
      const createdRecipeId = result?.data?.id;

      // Also add to user's saves so it appears in "Saves" tab
      if (createdRecipeId) {
        try {
          await fetcher(
            `/api/recipes/${createdRecipeId}/saves/toggle`,
            "POST",
            getToken,
          );
        } catch {
          // Ignore save toggle errors - recipe is still created
        }
      }

      setIsSaved(true);
      setSavedRecipeId(createdRecipeId ?? null);

      // Clear draft after successful save
      await clearDraft();

      if (showDialog) {
        setDialog({
          open: true,
          title: publishToPublic ? "Published to Community" : "Recipe Saved",
          message: publishToPublic
            ? "Your recipe has been published to the community! Others can now discover and cook your recipe."
            : "The recipe has been saved to your collection. You can find it in Me → Saves → Generated.",
          navigateOnClose: publishToPublic ? "/(tabs)/community" : "back",
        });
      }

      return createdRecipeId ?? null;
    } catch (error: any) {
      if (showDialog) {
        setDialog({
          open: true,
          title: "Error",
          message: error.message || "Failed to save recipe.",
        });
      }
      return null;
    }
  };

  const handlePublishToCommunity = () => {
    if (!draft) return;

    // Navigate to the publish form with pre-filled data
    router.push({
      pathname: "/(tabs)/add",
      params: {
        prefillTitle: draft.title,
        prefillDescription: draft.description ?? "",
        prefillSteps: JSON.stringify(draft.steps),
        prefillIngredients: JSON.stringify(
          ingredientsWithStatus.map((ing) => ({
            name: ing.name,
            amount: ing.quantity,
            unit: ing.unit,
          }))
        ),
        prefillTags: JSON.stringify(draft.tags),
        prefillImageUri: draft.imageUri ?? "",
      },
    });
  };

  const toggleIngredientSelection = (id: string) => {
    setSelectedIngredients((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  };

  const handleAddToChecklist = () => {
    const missingItems = ingredientsWithStatus
      .filter((ing) => selectedIngredients.has(ing.id) && !ing.isAvailable)
      .map((ing) => ({
        name: ing.name,
        amount: ing.quantity,
        unit: ing.unit,
        category: ing.category ?? "Other",
      }));

    if (missingItems.length === 0) {
      setDialog({
        open: true,
        title: "No Items to Add",
        message: "All selected ingredients are already in your inventory.",
      });
      return;
    }

    addToChecklist.mutate(
      { items: missingItems },
      {
        onSuccess: () => {
          setDialog({
            open: true,
            title: "Added to Checklist",
            message: `${missingItems.length} item(s) added to your shopping checklist.`,
            navigateOnClose: "/(tabs)/checklist",
          });
        },
        onError: (error) => {
          setDialog({
            open: true,
            title: "Error",
            message: error.message || "Failed to add items.",
          });
        },
      },
    );
  };

  const handleStartCookingAssistant = async () => {
    // First save the recipe if not already saved
    const recipeId = await handleSaveRecipe(false);

    if (!recipeId) {
      setDialog({
        open: true,
        title: "Error",
        message:
          "Please save the recipe first before starting the cooking assistant.",
      });
      return;
    }

    // Navigate to cooking assistant with the saved recipe ID
    router.push({
      pathname: "/cook",
      params: { recipeId, source: "recipe-scan" },
    });
  };

  // Handle adding new ingredient
  const handleAddNewIngredient = () => {
    if (!newIngredient.name.trim()) return;
    addIngredient({
      name: newIngredient.name.trim(),
      quantity: parseFloat(newIngredient.quantity) || 1,
      unit: newIngredient.unit.trim() || "unit",
    });
    setNewIngredient({ name: "", quantity: "", unit: "" });
    setShowAddIngredient(false);
  };

  // Handle adding new step
  const handleAddNewStep = () => {
    if (!newStepText.trim()) return;
    addStep(newStepText.trim());
    setNewStepText("");
    setShowAddStep(false);
  };

  // Handle adding new tag - supports comma-separated tags
  const handleAddTag = () => {
    if (!newTag.trim()) return;
    // Split on both regular comma and Chinese comma
    const tags = newTag.split(/[,，]/).map((t) => t.trim()).filter(Boolean);
    tags.forEach((tag) => addTag(tag));
    setNewTag("");
    setShowTagInput(false);
  };

  const missingCount = ingredientsWithStatus.filter(
    (i) => !i.isAvailable,
  ).length;
  const selectedMissingCount = ingredientsWithStatus.filter(
    (i) => selectedIngredients.has(i.id) && !i.isAvailable,
  ).length;

  // Calculate total time from draft
  const totalTime =
    (draft?.prepTimeMinutes ?? 0) + (draft?.cookTimeMinutes ?? 0);

  // Loading state
  if (isDraftLoading || !draft) {
    return (
      <SafeAreaView className="flex-1 items-center justify-center" style={{ backgroundColor: colors.bg }}>
        <ActivityIndicator size="large" color={colors.accent} />
        <Text className="text-lg mt-4" style={{ color: colors.textPrimary }}>Loading draft...</Text>
      </SafeAreaView>
    );
  }

  // Note: This condition checks if we have neither a parsed recipe nor a draft.
  // The loading state above handles the case where draft is still loading.
  if (!parsedRecipe && !draft) {
    return (
      <SafeAreaView className="flex-1 items-center justify-center" style={{ backgroundColor: colors.bg }}>
        <Text className="text-lg" style={{ color: colors.textPrimary }}>No recipe data found.</Text>
        <TouchableOpacity
          onPress={() => router.back()}
          className="mt-4 px-6 py-3 rounded-xl"
          style={{ backgroundColor: colors.accent }}
        >
          <Text className="font-semibold" style={{ color: colors.bg }}>Go Back</Text>
        </TouchableOpacity>
      </SafeAreaView>
    );
  }

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <KeyboardAvoidingView
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        className="flex-1"
      >
        <SafeAreaView className="flex-1" style={{ backgroundColor: colors.bg }}>
          {/* Header */}
          <View className="flex-row items-center justify-between px-4 py-3">
            <TouchableOpacity onPress={() => router.back()}>
              <X size={24} color={colors.textPrimary} />
            </TouchableOpacity>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              Recipe Draft
            </Text>
            <TouchableOpacity
              onPress={() => {
                void handleSaveRecipe();
              }}
              disabled={isSaved || isSaving}
            >
              {isSaving ? (
                <ActivityIndicator size="small" color={colors.accent} />
              ) : isSaved ? (
                <Check size={24} color={colors.success} />
              ) : (
                <Save size={24} color={colors.textPrimary} />
              )}
            </TouchableOpacity>
          </View>

          <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
            {/* Recipe Image & Info Container */}
            <View className="mx-4 mb-4 rounded-2xl overflow-hidden" style={{ backgroundColor: colors.card }}>
              {/* Image */}
              <View className="h-48 items-center justify-center relative" style={{ backgroundColor: colors.bg }}>
                {draft.imageUri ? (
                  <Image
                    source={{ uri: draft.imageUri }}
                    className="w-full h-full"
                    resizeMode="cover"
                  />
                ) : (
                  <ImageIcon size={48} color={colors.textMuted} />
                )}

                {typeof draft.confidence === "number" &&
                  !Number.isNaN(draft.confidence) && (
                    <View className="absolute bottom-2 right-2 z-10">
                      <View className="flex-row items-center gap-1 rounded-lg px-2 py-1" style={{ backgroundColor: `${colors.card}E6`, borderWidth: 1, borderColor: colors.border }}>
                        <View
                          className={`w-2 h-2 rounded-full ${
                            draft.confidence >= 0.8
                              ? "bg-green-400"
                              : draft.confidence >= 0.5
                                ? "bg-yellow-400"
                                : "bg-orange-400"
                          }`}
                        />
                        <Text className="text-xs font-medium" style={{ color: colors.textSecondary }}>
                          {Math.round(draft.confidence * 100)}% confident
                        </Text>
                      </View>
                    </View>
                  )}
              </View>

              {/* Recipe Title & Metadata - Editable */}
              <View className="p-4">
                {/* Editable Title */}
                {editingTitle ? (
                  <TextInput
                    value={draft.title}
                    onChangeText={updateTitle}
                    onBlur={() => setEditingTitle(false)}
                    autoFocus
                    className="text-xl font-bold mb-2 rounded-lg px-3 py-2"
                    style={{ backgroundColor: colors.bg, color: colors.textPrimary }}
                    placeholderTextColor={colors.textMuted}
                    placeholder="Recipe Title"
                  />
                ) : (
                  <TouchableOpacity onPress={() => setEditingTitle(true)}>
                    <View className="flex-row items-center gap-2 mb-2">
                      <Text className="text-xl font-bold flex-1" style={{ color: colors.textPrimary }}>
                        {draft.title}
                      </Text>
                      <Edit3 size={16} color={colors.textMuted} />
                    </View>
                  </TouchableOpacity>
                )}

                {/* Editable Metadata */}
                <View className="flex-row items-center">
                  {/* Left side - Time, Difficulty, Servings */}
                  <View className="flex-row items-center gap-4 flex-wrap flex-1">
                    {/* Time - Editable */}
                    <TouchableOpacity
                      onPress={() => setEditingTime(true)}
                      className="flex-row items-center gap-1"
                    >
                      <Clock size={16} color={colors.textSecondary} />
                      {editingTime ? (
                        <TextInput
                          value={String(totalTime || "")}
                          onChangeText={(text) => {
                            const val = parseInt(text) || 0;
                            updateCookTime(val);
                          }}
                          onBlur={() => setEditingTime(false)}
                          autoFocus
                          keyboardType="numeric"
                          className="text-sm rounded px-2 py-1 w-16"
                          style={{ backgroundColor: colors.bg, color: colors.textPrimary }}
                          placeholderTextColor={colors.textMuted}
                        />
                      ) : (
                        <Text className="text-sm" style={{ color: colors.textSecondary }}>
                          {totalTime > 0 ? `${totalTime} min` : "Set time"}
                        </Text>
                      )}
                    </TouchableOpacity>

                    {/* Difficulty */}
                    <View className="flex-row items-center gap-1">
                      <Gauge size={16} color={colors.textSecondary} />
                      <Text className="text-sm" style={{ color: colors.textSecondary }}>
                        {draft.difficulty ?? "N/A"}
                      </Text>
                    </View>

                    <TouchableOpacity
                      onPress={() => setEditingServings(true)}
                      className="flex-row items-center gap-1"
                    >
                      <Users size={16} color={colors.textSecondary} />
                      {editingServings ? (
                        <TextInput
                          value={String(draft.servings || "")}
                          onChangeText={(text) => {
                            const val = parseInt(text) || undefined;
                            updateServings(val);
                          }}
                          onBlur={() => setEditingServings(false)}
                          autoFocus
                          keyboardType="numeric"
                          className="text-sm rounded px-2 py-1 w-12"
                          style={{ backgroundColor: colors.bg, color: colors.textPrimary }}
                          placeholderTextColor={colors.textMuted}
                        />
                      ) : (
                        <Text className="text-sm" style={{ color: colors.textSecondary }}>
                          {draft.servings ?? "?"} servings
                        </Text>
                      )}
                    </TouchableOpacity>
                  </View>
                </View>

                {/* Tags Section */}
                <View className="mt-3">
                  <View className="flex-row items-center flex-wrap gap-2">
                    {draft.tags.map((tag) => (
                      <View
                        key={tag}
                        className="flex-row items-center rounded-full px-3 py-1"
                        style={{ backgroundColor: colors.accent }}
                      >
                        <Text className="text-xs font-medium mr-1" style={{ color: colors.bg }}>
                          {tag}
                        </Text>
                        <TouchableOpacity onPress={() => removeTag(tag)}>
                          <X size={12} color={colors.bg} />
                        </TouchableOpacity>
                      </View>
                    ))}
                    {showTagInput ? (
                      <View className="flex-row items-center">
                        <TextInput
                          value={newTag}
                          onChangeText={(value) => {
                            // Check if value ends with or contains a comma (regular or Chinese)
                            if (/[,，]/.test(value)) {
                              // Parse and add all tags
                              const tags = value.split(/[,，]/).map((t) => t.trim()).filter(Boolean);
                              tags.forEach((tag) => addTag(tag));
                              setNewTag("");
                            } else {
                              setNewTag(value);
                            }
                          }}
                          onSubmitEditing={handleAddTag}
                          onBlur={() => {
                            handleAddTag();
                            setShowTagInput(false);
                          }}
                          autoFocus
                          placeholder="Add tag"
                          placeholderTextColor={colors.textMuted}
                          className="text-xs rounded-full px-3 py-1 w-20"
                          style={{ backgroundColor: colors.bg, color: colors.textPrimary }}
                        />
                      </View>
                    ) : (
                      <TouchableOpacity
                        onPress={() => setShowTagInput(true)}
                        className="flex-row items-center rounded-full px-3 py-1"
                        style={{ backgroundColor: colors.bg }}
                      >
                        <Plus size={12} color={colors.textSecondary} />
                        <Text className="text-xs ml-1" style={{ color: colors.textSecondary }}>Tag</Text>
                      </TouchableOpacity>
                    )}
                  </View>
                </View>
              </View>
            </View>

            {/* Ingredients Section - Editable */}
            <View className="mx-4 mb-4 rounded-2xl p-4" style={{ backgroundColor: colors.card }}>
              <View className="flex-row items-center justify-between mb-3">
                <View className="flex-row items-center gap-2">
                  <ShoppingCart size={18} color={colors.accent} />
                  <Text className="font-semibold text-base" style={{ color: colors.textPrimary }}>
                    Ingredients ({ingredientsWithStatus.length})
                  </Text>
                </View>
                <TouchableOpacity
                  onPress={() => setShowAddIngredient(true)}
                  className="rounded-full p-1"
                  style={{ backgroundColor: colors.accent }}
                >
                  <Plus size={16} color={colors.bg} />
                </TouchableOpacity>
              </View>

              {/* Add Ingredient Form */}
              {showAddIngredient && (
                <View className="rounded-xl p-3 mb-3" style={{ backgroundColor: colors.bg }}>
                  <TextInput
                    value={newIngredient.name}
                    onChangeText={(text) =>
                      setNewIngredient((prev) => ({ ...prev, name: text }))
                    }
                    placeholder="Ingredient name"
                    placeholderTextColor={colors.textMuted}
                    className="rounded-lg px-3 py-2 mb-2"
                    style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                  />
                  <View className="flex-row gap-2 mb-2">
                    <TextInput
                      value={newIngredient.quantity}
                      onChangeText={(text) =>
                        setNewIngredient((prev) => ({
                          ...prev,
                          quantity: text,
                        }))
                      }
                      placeholder="Qty"
                      keyboardType="numeric"
                      placeholderTextColor={colors.textMuted}
                      className="rounded-lg px-3 py-2 flex-1"
                      style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                    />
                    <TextInput
                      value={newIngredient.unit}
                      onChangeText={(text) =>
                        setNewIngredient((prev) => ({ ...prev, unit: text }))
                      }
                      placeholder="Unit (e.g., cups)"
                      placeholderTextColor={colors.textMuted}
                      className="rounded-lg px-3 py-2 flex-1"
                      style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                    />
                  </View>
                  <View className="flex-row gap-2">
                    <TouchableOpacity
                      onPress={() => setShowAddIngredient(false)}
                      className="flex-1 rounded-lg py-2"
                      style={{ backgroundColor: colors.card }}
                    >
                      <Text className="text-center" style={{ color: colors.textPrimary }}>Cancel</Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      onPress={handleAddNewIngredient}
                      className="flex-1 rounded-lg py-2"
                      style={{ backgroundColor: colors.accent }}
                    >
                      <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                        Add
                      </Text>
                    </TouchableOpacity>
                  </View>
                </View>
              )}

              {/* Ingredients List */}
              <View className="rounded-xl overflow-hidden" style={{ backgroundColor: colors.bg }}>
                {[...ingredientsWithStatus]
                  .sort((a, b) => Number(a.isAvailable) - Number(b.isAvailable))
                  .map((ingredient, index) => (
                    <View key={ingredient.id}>
                      {editingIngredientId === ingredient.id ? (
                        <View className="p-3" style={{ backgroundColor: colors.bg }}>
                          <TextInput
                            value={ingredient.name}
                            onChangeText={(text) =>
                              updateIngredient(ingredient.id, { name: text })
                            }
                            className="rounded-lg px-3 py-2 mb-2"
                            style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                          />
                          <View className="flex-row gap-2 mb-2">
                            <TextInput
                              value={String(ingredient.quantity)}
                              onChangeText={(text) =>
                                updateIngredient(ingredient.id, {
                                  quantity: parseFloat(text) || 0,
                                })
                              }
                              keyboardType="numeric"
                              className="rounded-lg px-3 py-2 flex-1"
                              style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                            />
                            <TextInput
                              value={ingredient.unit}
                              onChangeText={(text) =>
                                updateIngredient(ingredient.id, { unit: text })
                              }
                              className="rounded-lg px-3 py-2 flex-1"
                              style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                            />
                          </View>
                          <TouchableOpacity
                            onPress={() => setEditingIngredientId(null)}
                            className="rounded-lg py-2"
                            style={{ backgroundColor: colors.accent }}
                          >
                            <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                              Done
                            </Text>
                          </TouchableOpacity>
                        </View>
                      ) : (
                        <TouchableOpacity
                          onPress={() =>
                            !ingredient.isAvailable &&
                            toggleIngredientSelection(ingredient.id)
                          }
                          onLongPress={() =>
                            setEditingIngredientId(ingredient.id)
                          }
                          className="flex-row items-center justify-between py-3 px-4"
                        >
                          <View className="flex-row items-center flex-1">
                            {ingredient.isAvailable ? (
                              <View className="w-6 h-6 items-center justify-center mr-3">
                                <Check size={18} color={colors.success} />
                              </View>
                            ) : (
                              <View
                                className="w-6 h-6 rounded border-2 items-center justify-center mr-3"
                                style={{
                                  backgroundColor: selectedIngredients.has(ingredient.id) ? colors.accent : "transparent",
                                  borderColor: selectedIngredients.has(ingredient.id) ? colors.accent : colors.border,
                                }}
                              >
                                {selectedIngredients.has(ingredient.id) && (
                                  <Check size={14} color={colors.bg} />
                                )}
                              </View>
                            )}
                            <Text
                              className="text-base flex-1"
                              style={{
                                color: colors.textPrimary,
                                fontWeight: ingredient.isAvailable ? "500" : "400",
                              }}
                            >
                              {ingredient.name}
                            </Text>
                          </View>
                          <View className="flex-row items-center gap-2">
                            <Text className="text-sm" style={{ color: colors.textSecondary }}>
                              {ingredient.quantity} {ingredient.unit}
                            </Text>
                            <TouchableOpacity
                              onPress={() => removeIngredient(ingredient.id)}
                              className="p-1"
                            >
                              <Trash2 size={14} color={colors.textMuted} />
                            </TouchableOpacity>
                          </View>
                        </TouchableOpacity>
                      )}
                      {index < ingredientsWithStatus.length - 1 && (
                        <View className="h-[1px] mx-4" style={{ backgroundColor: colors.border }} />
                      )}
                    </View>
                  ))}
              </View>

              {/* Editing hint */}
              <Text className="text-xs mt-2 text-center" style={{ color: colors.textMuted }}>
                Long press an ingredient to edit
              </Text>

              {/* Add to Checklist Button */}
              {missingCount > 0 && (
                <TouchableOpacity
                  onPress={handleAddToChecklist}
                  disabled={
                    addToChecklist.isPending || selectedMissingCount === 0
                  }
                  className="mt-4 flex-row items-center justify-center gap-2 rounded-xl py-4"
                  style={{
                    backgroundColor: selectedMissingCount === 0 ? `${colors.accent}80` : colors.accent,
                  }}
                >
                  <ShoppingCart size={18} color={colors.bg} />
                  <Text className="font-semibold" style={{ color: colors.bg }}>
                    {addToChecklist.isPending
                      ? "Adding..."
                      : `Add Missing Items to Checklist (${selectedMissingCount})`}
                  </Text>
                </TouchableOpacity>
              )}
            </View>

            {/* Cooking Steps Section - Editable */}
            <View className="mx-4 mb-4 rounded-2xl p-4" style={{ backgroundColor: colors.card }}>
              <View className="flex-row items-center justify-between mb-3">
                <View className="flex-row items-center gap-2">
                  <ChefHat size={18} color={colors.accent} />
                  <Text className="font-semibold text-base" style={{ color: colors.textPrimary }}>
                    Cooking Steps ({draft.steps.length})
                  </Text>
                </View>
                <TouchableOpacity
                  onPress={() => setShowAddStep(true)}
                  className="rounded-full p-1"
                  style={{ backgroundColor: colors.accent }}
                >
                  <Plus size={16} color={colors.bg} />
                </TouchableOpacity>
              </View>

              {/* Add Step Form */}
              {showAddStep && (
                <View className="rounded-xl p-3 mb-3" style={{ backgroundColor: colors.bg }}>
                  <TextInput
                    value={newStepText}
                    onChangeText={setNewStepText}
                    placeholder="Describe the step..."
                    placeholderTextColor={colors.textMuted}
                    multiline
                    numberOfLines={3}
                    textAlignVertical="top"
                    className="rounded-lg px-3 py-2 mb-2 min-h-[80px]"
                    style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                  />
                  <View className="flex-row gap-2">
                    <TouchableOpacity
                      onPress={() => {
                        setShowAddStep(false);
                        setNewStepText("");
                      }}
                      className="flex-1 rounded-lg py-2"
                      style={{ backgroundColor: colors.card }}
                    >
                      <Text className="text-center" style={{ color: colors.textPrimary }}>Cancel</Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      onPress={handleAddNewStep}
                      className="flex-1 rounded-lg py-2"
                      style={{ backgroundColor: colors.accent }}
                    >
                      <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                        Add Step
                      </Text>
                    </TouchableOpacity>
                  </View>
                </View>
              )}

              {/* Steps List */}
              <View className="rounded-xl overflow-hidden" style={{ backgroundColor: colors.bg }}>
                {draft.steps.map((step, index) => (
                  <View key={`step-${index}`}>
                    {editingStepIndex === index ? (
                      <View className="p-3" style={{ backgroundColor: colors.bg }}>
                        <TextInput
                          value={step}
                          onChangeText={(text) => updateStep(index, text)}
                          multiline
                          numberOfLines={3}
                          textAlignVertical="top"
                          className="rounded-lg px-3 py-2 mb-2 min-h-[80px]"
                          style={{ backgroundColor: colors.card, color: colors.textPrimary }}
                        />
                        <TouchableOpacity
                          onPress={() => setEditingStepIndex(null)}
                          className="rounded-lg py-2"
                          style={{ backgroundColor: colors.accent }}
                        >
                          <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                            Done
                          </Text>
                        </TouchableOpacity>
                      </View>
                    ) : (
                      <TouchableOpacity
                        onLongPress={() => setEditingStepIndex(index)}
                        className="flex-row py-3 px-4"
                      >
                        <View className="w-7 h-7 rounded-full items-center justify-center mr-3 mt-0.5" style={{ backgroundColor: colors.accent }}>
                          <Text className="font-bold text-sm" style={{ color: colors.bg }}>
                            {index + 1}
                          </Text>
                        </View>
                        <View className="flex-1">
                          <Text className="text-sm leading-5" style={{ color: colors.textPrimary }}>
                            {step}
                          </Text>
                        </View>
                        <TouchableOpacity
                          onPress={() => removeStep(index)}
                          className="p-1 ml-2"
                        >
                          <Trash2 size={14} color={colors.textMuted} />
                        </TouchableOpacity>
                      </TouchableOpacity>
                    )}
                    {index < draft.steps.length - 1 && (
                      <View className="h-[1px] mx-4" style={{ backgroundColor: colors.border }} />
                    )}
                  </View>
                ))}
              </View>

              {/* Editing hint */}
              <Text className="text-xs mt-2 text-center" style={{ color: colors.textMuted }}>
                Long press a step to edit
              </Text>
            </View>

            {/* Action Buttons Section */}
            <View className="mx-4 mb-4 gap-3">
              {/* Start Cooking Assistant Button */}
              <TouchableOpacity
                onPress={handleStartCookingAssistant}
                className="flex-row items-center justify-center gap-2 rounded-xl py-4"
                style={{ backgroundColor: colors.accent }}
              >
                <ChefHat size={18} color={colors.bg} />
                <Text className="font-semibold" style={{ color: colors.bg }}>
                  Start Cooking Assistant
                </Text>
              </TouchableOpacity>

              {/* Share to Community Button */}
              <TouchableOpacity
                onPress={handlePublishToCommunity}
                className="flex-row items-center justify-center gap-2 rounded-xl py-4 border"
                style={{
                  borderColor: colors.accent,
                  backgroundColor: "transparent",
                }}
              >
                <Globe2 size={18} color={colors.accent} />
                <Text className="font-semibold" style={{ color: colors.accent }}>
                  Share to Community
                </Text>
              </TouchableOpacity>
            </View>

            {/* Nutrition Section (if available) */}
            {(draft.nutrition.calories ||
              draft.nutrition.protein ||
              draft.nutrition.carbohydrates ||
              draft.nutrition.fat) && (
              <View className="mx-4 mb-4 rounded-2xl p-4" style={{ backgroundColor: colors.card }}>
                <View className="flex-row items-center gap-2 mb-3">
                  <RefreshCw size={18} color={colors.accent} />
                  <Text className="font-semibold text-base" style={{ color: colors.textPrimary }}>
                    Nutrition (per serving)
                  </Text>
                </View>
                <View className="flex-row flex-wrap gap-2">
                  {draft.nutrition.calories != null && (
                    <View className="rounded-lg px-3 py-2" style={{ backgroundColor: colors.bg }}>
                      <Text className="text-xs" style={{ color: colors.textSecondary }}>Calories</Text>
                      <Text className="font-semibold" style={{ color: colors.textPrimary }}>
                        {draft.nutrition.calories}
                      </Text>
                    </View>
                  )}
                  {draft.nutrition.protein != null && (
                    <View className="rounded-lg px-3 py-2" style={{ backgroundColor: colors.bg }}>
                      <Text className="text-xs" style={{ color: colors.textSecondary }}>Protein</Text>
                      <Text className="font-semibold" style={{ color: colors.textPrimary }}>
                        {draft.nutrition.protein}%
                      </Text>
                    </View>
                  )}
                  {draft.nutrition.carbohydrates != null && (
                    <View className="rounded-lg px-3 py-2" style={{ backgroundColor: colors.bg }}>
                      <Text className="text-xs" style={{ color: colors.textSecondary }}>Carbs</Text>
                      <Text className="font-semibold" style={{ color: colors.textPrimary }}>
                        {draft.nutrition.carbohydrates}%
                      </Text>
                    </View>
                  )}
                  {draft.nutrition.fat != null && (
                    <View className="rounded-lg px-3 py-2" style={{ backgroundColor: colors.bg }}>
                      <Text className="text-xs" style={{ color: colors.textSecondary }}>Fat</Text>
                      <Text className="font-semibold" style={{ color: colors.textPrimary }}>
                        {draft.nutrition.fat}%
                      </Text>
                    </View>
                  )}
                </View>
              </View>
            )}

            {/* Spacer for bottom padding */}
            <View className="h-8" />
          </ScrollView>
        </SafeAreaView>
      </KeyboardAvoidingView>

      {/* Alert Dialog */}
      <AlertDialog
        open={dialog.open}
        onOpenChange={(open) => {
          if (!open && dialog.navigateOnClose) {
            if (dialog.navigateOnClose === "back") {
              router.back();
            } else {
              router.replace(dialog.navigateOnClose as any);
            }
          }
          setDialog((prev) => ({ ...prev, open }));
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{dialog.title}</AlertDialogTitle>
            <AlertDialogDescription>{dialog.message}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogAction>OK</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
