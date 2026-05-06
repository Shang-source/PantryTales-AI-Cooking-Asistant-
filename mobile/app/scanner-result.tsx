import React, { useState, useMemo, useEffect, useCallback } from "react";
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
  Bookmark,
  Plus,
  Trash2,
  Edit3,
  ChevronDown,
  ChevronUp,
  Share2,
} from "lucide-react-native";
import { RecipePosterPreviewModal } from "@/components/RecipePosterPreviewModal";
import type { RecipePosterData } from "@/components/RecipePoster";
import {
  RecognizedRecipe,
  RecipeNutrition,
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
import { useTheme } from "@/contexts/ThemeContext";

// ============ Types ============

interface LocalIngredient {
  id: string;
  name: string;
  quantity: number;
  unit: string;
  category?: string;
}

interface IngredientWithStatus extends LocalIngredient {
  isAvailable: boolean;
  availableAmount: number;
}

interface LocalRecipeState {
  title: string;
  description: string;
  imageUri?: string;
  tags: string[];
  cookTimeMinutes?: number;
  prepTimeMinutes?: number;
  difficulty: "None" | "Easy" | "Medium" | "Hard";
  servings: number;
  ingredients: LocalIngredient[];
  steps: string[];
}

interface DialogState {
  open: boolean;
  title: string;
  message: string;
  navigateOnClose?: string;
}

// ============ Helpers ============

function generateId(): string {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

function calculateDifficulty(
  recipe: RecognizedRecipe,
): "None" | "Easy" | "Medium" | "Hard" {
  const totalTime =
    (recipe.prepTimeMinutes ?? 0) + (recipe.cookTimeMinutes ?? 0);
  const stepsCount = recipe.steps?.length ?? 0;
  const score = totalTime + stepsCount;
  if (score < 30) return "Easy";
  if (score <= 60) return "Medium";
  return "Hard";
}

function createLocalStateFromRecipe(
  recipe: RecognizedRecipe,
  imageUri?: string,
): LocalRecipeState {
  return {
    title: recipe.title || "Untitled Recipe",
    description: recipe.description || "",
    imageUri,
    tags: [],
    cookTimeMinutes: recipe.cookTimeMinutes,
    prepTimeMinutes: recipe.prepTimeMinutes,
    difficulty: calculateDifficulty(recipe),
    servings: recipe.servings ?? 4,
    ingredients: (recipe.ingredients || []).map((ing) => ({
      id: generateId(),
      name: ing.name,
      quantity: ing.quantity,
      unit: ing.unit,
      category: ing.category,
    })),
    steps: recipe.steps || [],
  };
}

// ============ Main Component ============

export default function ScannerResultScreen() {
  const { colors } = useTheme();
  const { recipe: recipeParam, imageUri: imageUriParam } =
    useLocalSearchParams<{
      recipe: string;
      imageUri?: string;
    }>();

  const { getToken } = useAuth();

  // Parse recipe from params
  const parsedRecipe: RecognizedRecipe | null = useMemo(() => {
    if (!recipeParam) return null;
    try {
      return JSON.parse(recipeParam);
    } catch {
      return null;
    }
  }, [recipeParam]);

  // LOCAL STATE - not persisted anywhere, lost on navigation
  const [localState, setLocalState] = useState<LocalRecipeState | null>(null);
  const [nutrition, setNutrition] = useState<RecipeNutrition | null>(null);

  // Dialog state
  const [dialog, setDialog] = useState<DialogState>({
    open: false,
    title: "",
    message: "",
  });

  // Edit mode states
  const [editingTitle, setEditingTitle] = useState(false);
  const [editingIngredientId, setEditingIngredientId] = useState<string | null>(
    null,
  );
  const [showAddIngredient, setShowAddIngredient] = useState(false);
  const [newTag, setNewTag] = useState("");
  const [showTagInput, setShowTagInput] = useState(false);
  const [newIngredient, setNewIngredient] = useState({
    name: "",
    quantity: "",
    unit: "",
  });

  // Accordion states
  const [ingredientsExpanded, setIngredientsExpanded] = useState(false);
  const [stepsExpanded, setStepsExpanded] = useState(false);

  // Step editing states
  const [editingStepIndex, setEditingStepIndex] = useState<number | null>(null);
  const [editingStepText, setEditingStepText] = useState("");
  const [showAddStep, setShowAddStep] = useState(false);
  const [newStepText, setNewStepText] = useState("");
  const [isPosterPreviewVisible, setIsPosterPreviewVisible] = useState(false);

  // Initialize local state from parsed recipe
  useEffect(() => {
    if (parsedRecipe && !localState) {
      setLocalState(createLocalStateFromRecipe(parsedRecipe, imageUriParam));
      // Initialize nutrition from AI response
      if (parsedRecipe.nutrition) {
        setNutrition(parsedRecipe.nutrition);
      }
    }
  }, [parsedRecipe, imageUriParam, localState]);

  // Fetch inventory for comparison
  const { data: inventoryData } = useInventoryItems();
  const inventoryItems = useMemo(
    () => inventoryData?.data?.data ?? [],
    [inventoryData],
  );

  // Process ingredients with inventory comparison and sort
  const ingredientsWithStatus: IngredientWithStatus[] = useMemo(() => {
    if (!localState) return [];

    const processed = localState.ingredients.map((ing) => {
      const match = findInventoryMatch(ing.name, inventoryItems);
      const hasEnough = match ? match.availableAmount >= ing.quantity : false;
      return {
        ...ing,
        isAvailable: hasEnough,
        availableAmount: match?.availableAmount ?? 0,
      };
    });

    // Sort: missing items (not available) first, then available items
    return processed.sort((a, b) => {
      if (a.isAvailable === b.isAvailable) return 0;
      return a.isAvailable ? 1 : -1;
    });
  }, [localState, inventoryItems]);

  const missingIngredients = useMemo(
    () => ingredientsWithStatus.filter((i) => !i.isAvailable),
    [ingredientsWithStatus],
  );

  // Checklist mutation
  const addToChecklist = useAddChecklistBatch();

  // Recipe save mutation
  const [isSaved, setIsSaved] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const imageUpload = useImageUpload();
  const saveRecipeMutation = useAuthMutation<
    ApiResponse<RecipeDetailDto>,
    CreateRecipeRequest
  >("/api/recipes");

  // ============ Handlers ============

  // State update helpers
  const updateTitle = useCallback((title: string) => {
    setLocalState((prev) => (prev ? { ...prev, title } : null));
  }, []);

  const addTag = useCallback((tag: string) => {
    const trimmed = tag.trim();
    if (!trimmed) return;
    setLocalState((prev) => {
      if (!prev || prev.tags.includes(trimmed)) return prev;
      return { ...prev, tags: [...prev.tags, trimmed] };
    });
  }, []);

  const removeTag = useCallback((tag: string) => {
    setLocalState((prev) => {
      if (!prev) return null;
      return { ...prev, tags: prev.tags.filter((t) => t !== tag) };
    });
  }, []);

  const addIngredient = useCallback(
    (ingredient: Omit<LocalIngredient, "id">) => {
      setLocalState((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          ingredients: [
            ...prev.ingredients,
            { ...ingredient, id: generateId() },
          ],
        };
      });
    },
    [],
  );

  const updateIngredient = useCallback(
    (id: string, updates: Partial<Omit<LocalIngredient, "id">>) => {
      setLocalState((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          ingredients: prev.ingredients.map((ing) =>
            ing.id === id ? { ...ing, ...updates } : ing,
          ),
        };
      });
    },
    [],
  );

  const removeIngredient = useCallback((id: string) => {
    setLocalState((prev) => {
      if (!prev) return null;
      return {
        ...prev,
        ingredients: prev.ingredients.filter((ing) => ing.id !== id),
      };
    });
  }, []);

  const handleAddNewIngredient = useCallback(() => {
    if (!newIngredient.name.trim()) return;
    addIngredient({
      name: newIngredient.name.trim(),
      quantity: parseFloat(newIngredient.quantity) || 1,
      unit: newIngredient.unit.trim() || "unit",
    });
    setNewIngredient({ name: "", quantity: "", unit: "" });
    setShowAddIngredient(false);
  }, [newIngredient, addIngredient]);

  const handleAddTag = useCallback(() => {
    if (!newTag.trim()) return;
    // Split on both regular comma and Chinese comma
    const tags = newTag.split(/[,，]/).map((t) => t.trim()).filter(Boolean);
    tags.forEach((tag) => addTag(tag));
    setNewTag("");
    setShowTagInput(false);
  }, [newTag, addTag]);

  // Step handlers
  const addStep = useCallback((stepText: string) => {
    const trimmed = stepText.trim();
    if (!trimmed) return;
    setLocalState((prev) => {
      if (!prev) return null;
      return { ...prev, steps: [...prev.steps, trimmed] };
    });
  }, []);

  const updateStep = useCallback((index: number, newText: string) => {
    setLocalState((prev) => {
      if (!prev) return null;
      const newSteps = [...prev.steps];
      newSteps[index] = newText;
      return { ...prev, steps: newSteps };
    });
  }, []);

  const removeStep = useCallback((index: number) => {
    setLocalState((prev) => {
      if (!prev) return null;
      return { ...prev, steps: prev.steps.filter((_, i) => i !== index) };
    });
  }, []);

  const handleAddNewStep = useCallback(() => {
    if (!newStepText.trim()) return;
    addStep(newStepText.trim());
    setNewStepText("");
    setShowAddStep(false);
  }, [newStepText, addStep]);

  const handleSaveEditingStep = useCallback(() => {
    if (editingStepIndex !== null && editingStepText.trim()) {
      updateStep(editingStepIndex, editingStepText.trim());
    }
    setEditingStepIndex(null);
    setEditingStepText("");
  }, [editingStepIndex, editingStepText, updateStep]);

  const startEditingStep = useCallback((index: number, text: string) => {
    setEditingStepIndex(index);
    setEditingStepText(text);
  }, []);

  const handleAddToChecklist = useCallback(() => {
    if (missingIngredients.length === 0) {
      setDialog({
        open: true,
        title: "No Missing Ingredients",
        message: "All ingredients are already available in your inventory.",
      });
      return;
    }

    const items = missingIngredients.map((ing) => ({
      name: ing.name,
      amount: ing.quantity,
      unit: ing.unit,
      category: ing.category ?? "Other",
    }));

    addToChecklist.mutate(
      { items },
      {
        onSuccess: () => {
          setDialog({
            open: true,
            title: "Added to Checklist",
            message: `${items.length} missing ingredient(s) added to your shopping checklist.`,
          });
        },
        onError: (error) => {
          setDialog({
            open: true,
            title: "Error",
            message: error.message || "Failed to add items to checklist.",
          });
        },
      },
    );
  }, [missingIngredients, addToChecklist]);

  const handleSaveRecipe = useCallback(async () => {
    if (isSaved || isSaving || !localState) return;

    setIsSaving(true);

    const rawImageUri = localState.imageUri ?? imageUriParam;
    let uploadedImageUrl: string | undefined;

    if (rawImageUri) {
      const trimmed = rawImageUri.trim();
      const isRemote =
        trimmed.startsWith("http://") || trimmed.startsWith("https://");

      if (isRemote) {
        uploadedImageUrl = trimmed;
      } else {
        try {
          const uploadResult = await imageUpload.mutateAsync(trimmed);
          uploadedImageUrl = uploadResult.url;
        } catch (error: any) {
          setDialog({
            open: true,
            title: "Image Upload Failed",
            message: error?.message || "Could not upload the scanned image.",
          });
          setIsSaving(false);
          return;
        }
      }
    }

    const difficultyMap: Record<string, RecipeDifficulty> = {
      Easy: "Easy",
      Medium: "Medium",
      Hard: "Hard",
    };

    const totalTime =
      (localState.prepTimeMinutes ?? 0) + (localState.cookTimeMinutes ?? 0);

    const payload: CreateRecipeRequest = {
      title: localState.title,
      description: localState.description ?? "",
      steps: localState.steps,
      visibility: "Private",
      type: "Model",
      servings: localState.servings,
      totalTimeMinutes: totalTime > 0 ? totalTime : null,
      difficulty: difficultyMap[localState.difficulty ?? "None"] ?? "None",
      imageUrls: uploadedImageUrl ? [uploadedImageUrl] : undefined,
      tags: localState.tags.length > 0 ? localState.tags : undefined,
      // Include ingredients
      ingredients: localState.ingredients.map((ing) => ({
        name: ing.name,
        amount: ing.quantity,
        unit: ing.unit,
        isOptional: false,
        category: ing.category,
      })),
      // Include nutrition if calculated
      nutrition: nutrition
        ? {
            calories: nutrition.calories,
            carbohydrates: nutrition.carbohydrates,
            fat: nutrition.fat,
            protein: nutrition.protein,
            sugar: nutrition.sugar,
            sodium: nutrition.sodium,
            saturatedFat: nutrition.saturatedFat,
          }
        : undefined,
    };

    try {
      const result = await saveRecipeMutation.mutateAsync(payload);
      const createdRecipeId = result?.data?.id;

      // Add to user's saves so it appears in "Generated" tab
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
      setDialog({
        open: true,
        title: "Recipe Saved",
        message:
          "The recipe has been saved to your collection. You can find it in Me \u2192 Saves \u2192 Generated.",
        navigateOnClose: "back",
      });
    } catch (error: any) {
      setDialog({
        open: true,
        title: "Error",
        message: error.message || "Failed to save recipe.",
      });
    } finally {
      setIsSaving(false);
    }
  }, [
    localState,
    imageUriParam,
    imageUpload,
    isSaved,
    isSaving,
    saveRecipeMutation,
    getToken,
  ]);

  const handleStartCookingAssistant = useCallback(async () => {
    // Must save first before starting cooking assistant
    if (!isSaved) {
      setDialog({
        open: true,
        title: "Save Required",
        message:
          "Please save the recipe first before starting the cooking assistant.",
      });
      return;
    }

    // Navigate to cooking assistant
    router.push({
      pathname: "/cook",
      params: { source: "scanner-result" },
    });
  }, [isSaved]);

  // Poster preview handlers
  const handleOpenPosterPreview = useCallback(() => {
    if (!localState) return;
    setIsPosterPreviewVisible(true);
  }, [localState]);

  const handleClosePosterPreview = useCallback(() => {
    setIsPosterPreviewVisible(false);
  }, []);

  // Convert local state to poster data
  const posterData: RecipePosterData | null = useMemo(() => {
    if (!localState) return null;
    const totalTimeVal =
      (localState.prepTimeMinutes ?? 0) + (localState.cookTimeMinutes ?? 0);
    return {
      title: localState.title,
      description: localState.description,
      imageUrl: localState.imageUri,
      ingredients: localState.ingredients.map((ing) => ({
        name: ing.name,
        amount: ing.quantity,
        unit: ing.unit,
      })),
      steps: localState.steps,
      totalTimeMinutes: totalTimeVal > 0 ? totalTimeVal : null,
      servings: localState.servings,
      difficulty: localState.difficulty,
      tags: localState.tags,
      calories: nutrition?.calories,
      carbohydrates: nutrition?.carbohydrates,
      fat: nutrition?.fat,
      protein: nutrition?.protein,
      sugar: nutrition?.sugar,
      sodium: nutrition?.sodium,
      saturatedFat: nutrition?.saturatedFat,
    };
  }, [localState, nutrition]);

  // Calculate total time
  const totalTime = useMemo(
    () =>
      (localState?.prepTimeMinutes ?? 0) + (localState?.cookTimeMinutes ?? 0),
    [localState],
  );

  const displayImageUri = useMemo(() => {
    const uri = localState?.imageUri ?? imageUriParam;
    if (!uri) return null;
    if (
      uri.startsWith("file://") ||
      uri.startsWith("content://") ||
      uri.startsWith("ph://")
    ) {
      return uri;
    }
    return `file://${uri}`;
  }, [localState?.imageUri, imageUriParam]);

  // ============ Render ============

  if (!localState || !parsedRecipe) {
    return (
      <SafeAreaView className="flex-1 items-center justify-center" style={{ backgroundColor: colors.bg }}>
        <ActivityIndicator size="large" color={colors.accent} />
        <Text className="text-lg mt-4" style={{ color: colors.textPrimary }}>Loading...</Text>
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
              Recipe Details
            </Text>
            <View className="flex-row items-center gap-3">
              <TouchableOpacity
                onPress={handleOpenPosterPreview}
                disabled={!localState}
              >
                <Share2 size={22} color={colors.textPrimary} />
              </TouchableOpacity>
              <TouchableOpacity
                onPress={handleSaveRecipe}
                disabled={isSaved || isSaving}
              >
                {isSaving ? (
                  <ActivityIndicator size="small" color={colors.textPrimary} />
                ) : isSaved ? (
                  <Bookmark size={24} color={colors.accent} fill={colors.accent} />
                ) : (
                  <Bookmark size={24} color={colors.textPrimary} />
                )}
              </TouchableOpacity>
            </View>
          </View>

          <ScrollView className="flex-1" showsVerticalScrollIndicator={false}>
            {/* Recipe Image & Info Container */}
            <View className="mx-4 mb-4 rounded-2xl overflow-hidden" style={{ backgroundColor: colors.card }}>
              {/* Image */}
              <View className="h-48 items-center justify-center relative" style={{ backgroundColor: colors.bg }}>
                {displayImageUri ? (
                  <Image
                    source={{ uri: displayImageUri }}
                    className="w-full h-full"
                    resizeMode="cover"
                  />
                ) : (
                  <ImageIcon size={48} color="rgba(255,255,255,0.3)" />
                )}

                {typeof parsedRecipe.confidence === "number" &&
                  !Number.isNaN(parsedRecipe.confidence) && (
                    <View className="absolute bottom-2 right-2 z-10">
                      <View className="flex-row items-center gap-1 rounded-lg px-2 py-1" style={{ backgroundColor: `${colors.bg}E6`, borderWidth: 1, borderColor: colors.border }}>
                        <View
                          className={`w-2 h-2 rounded-full ${
                            parsedRecipe.confidence >= 0.8
                              ? "bg-green-400"
                              : parsedRecipe.confidence >= 0.5
                                ? "bg-yellow-400"
                                : "bg-orange-400"
                          }`}
                        />
                        <Text className="text-xs font-medium" style={{ color: colors.textSecondary }}>
                          {`${Math.round(parsedRecipe.confidence * 100)}% confident`}
                        </Text>
                      </View>
                    </View>
                  )}
              </View>

              {/* Recipe Title & Metadata */}
              <View className="p-4">
                {/* Editable Title */}
                {editingTitle ? (
                  <TextInput
                    value={localState.title}
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
                        {localState.title}
                      </Text>
                      <Edit3 size={16} color={colors.textMuted} />
                    </View>
                  </TouchableOpacity>
                )}

                {/* Tags Section */}
                <View className="flex-row items-center flex-wrap gap-2 mb-3">
                  {localState.tags.map((tag) => (
                    <View
                      key={tag}
                      className="flex-row items-center rounded-full px-3 py-1"
                      style={{ backgroundColor: colors.bg }}
                    >
                      <Text className="text-xs font-medium mr-1" style={{ color: colors.textPrimary }}>
                        {tag}
                      </Text>
                      <TouchableOpacity onPress={() => removeTag(tag)}>
                        <X size={12} color={colors.textSecondary} />
                      </TouchableOpacity>
                    </View>
                  ))}
                  {showTagInput ? (
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

                {/* Metadata Row */}
                <View className="flex-row items-center gap-4 flex-wrap">
                  <View className="flex-row items-center gap-1">
                    <Clock size={16} color={colors.textSecondary} />
                    <Text className="text-sm" style={{ color: colors.textSecondary }}>
                      {totalTime > 0 ? `${totalTime} mins` : "N/A"}
                    </Text>
                  </View>

                  <View className="flex-row items-center gap-1">
                    <Gauge size={16} color={colors.textSecondary} />
                    <Text className="text-sm" style={{ color: colors.textSecondary }}>
                      {localState.difficulty ?? "N/A"}
                    </Text>
                  </View>

                  <View className="flex-row items-center gap-1">
                    <Users size={16} color={colors.textSecondary} />
                    <Text className="text-sm" style={{ color: colors.textSecondary }}>
                      {localState.servings} servings
                    </Text>
                  </View>
                </View>
              </View>
            </View>

            {/* Ingredients Section - Collapsible Accordion */}
            <View className="mx-4 mb-4 rounded-2xl overflow-hidden" style={{ backgroundColor: colors.card }}>
              {/* Accordion Header */}
              <TouchableOpacity
                onPress={() => setIngredientsExpanded(!ingredientsExpanded)}
                className="flex-row items-center justify-between p-4"
                activeOpacity={0.7}
              >
                <View className="flex-row items-center flex-1">
                  <Text className="font-semibold text-base" style={{ color: colors.textPrimary }}>
                    Ingredients ({ingredientsWithStatus.length})
                  </Text>
                  <View className="ml-2">
                    {ingredientsExpanded ? (
                      <ChevronUp size={20} color={colors.textSecondary} />
                    ) : (
                      <ChevronDown size={20} color={colors.textSecondary} />
                    )}
                  </View>
                </View>
                <TouchableOpacity
                  onPress={(e) => {
                    e.stopPropagation();
                    setShowAddIngredient(true);
                    setIngredientsExpanded(true);
                  }}
                  className="flex-row items-center rounded-full px-3 py-1.5"
                  style={{ backgroundColor: colors.bg }}
                >
                  <Plus size={14} color={colors.accent} />
                  <Text className="text-sm ml-1 font-medium" style={{ color: colors.accent }}>
                    Add
                  </Text>
                </TouchableOpacity>
              </TouchableOpacity>

              {/* Expandable Content */}
              {ingredientsExpanded && (
                <View className="px-4 pb-4">
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
                        style={{ color: colors.textPrimary, backgroundColor: colors.card }}
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
                          style={{ color: colors.textPrimary, backgroundColor: colors.card }}
                        />
                        <TextInput
                          value={newIngredient.unit}
                          onChangeText={(text) =>
                            setNewIngredient((prev) => ({
                              ...prev,
                              unit: text,
                            }))
                          }
                          placeholder="Unit"
                          placeholderTextColor={colors.textMuted}
                          className="rounded-lg px-3 py-2 flex-1"
                          style={{ color: colors.textPrimary, backgroundColor: colors.card }}
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
                  <View className="rounded-xl overflow-hidden" style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}>
                    {ingredientsWithStatus.map((ingredient, index) => {
                      const isLast = index === ingredientsWithStatus.length - 1;
                      const isEditing = editingIngredientId === ingredient.id;
                      return (
                        <View
                          key={ingredient.id}
                          style={!isLast ? { borderBottomWidth: 1, borderBottomColor: colors.border } : undefined}
                        >
                          {isEditing ? (
                            <View className="p-3" style={{ backgroundColor: colors.card }}>
                              <TextInput
                                value={ingredient.name}
                                onChangeText={(text) =>
                                  updateIngredient(ingredient.id, {
                                    name: text,
                                  })
                                }
                                className="rounded-lg px-3 py-2 mb-2"
                                style={{ color: colors.textPrimary, backgroundColor: colors.bg }}
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
                                  style={{ color: colors.textPrimary, backgroundColor: colors.bg }}
                                />
                                <TextInput
                                  value={ingredient.unit}
                                  onChangeText={(text) =>
                                    updateIngredient(ingredient.id, {
                                      unit: text,
                                    })
                                  }
                                  className="rounded-lg px-3 py-2 flex-1"
                                  style={{ color: colors.textPrimary, backgroundColor: colors.bg }}
                                />
                              </View>
                              <View className="flex-row gap-2">
                                <TouchableOpacity
                                  onPress={() => setEditingIngredientId(null)}
                                  className="flex-1 rounded-lg py-2"
                                  style={{ backgroundColor: colors.accent }}
                                >
                                  <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                                    Done
                                  </Text>
                                </TouchableOpacity>
                                <TouchableOpacity
                                  onPress={() =>
                                    removeIngredient(ingredient.id)
                                  }
                                  className="flex-1 rounded-lg py-2"
                                  style={{ backgroundColor: colors.error }}
                                >
                                  <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                                    Delete
                                  </Text>
                                </TouchableOpacity>
                              </View>
                            </View>
                          ) : (
                            <TouchableOpacity
                              onLongPress={() =>
                                setEditingIngredientId(ingredient.id)
                              }
                              activeOpacity={0.8}
                              className="flex-row items-center justify-between px-4 py-3"
                            >
                              <View className="flex-row items-center gap-2">
                                {ingredient.isAvailable ? (
                                  <Check size={20} color="#34d399" />
                                ) : (
                                  <X size={20} color="#f87171" />
                                )}
                                <Text
                                  className="text-base font-medium"
                                  style={{ color: ingredient.isAvailable ? colors.textPrimary : "#f87171" }}
                                >
                                  {ingredient.name}
                                </Text>
                              </View>
                              <View className="flex-row items-center gap-3">
                                <Text className="text-sm" style={{ color: colors.textSecondary }}>
                                  {ingredient.quantity} {ingredient.unit}
                                </Text>
                                <TouchableOpacity
                                  onPress={() =>
                                    setEditingIngredientId(ingredient.id)
                                  }
                                  activeOpacity={0.7}
                                  className="p-1"
                                >
                                  <Edit3
                                    size={16}
                                    color={colors.textSecondary}
                                  />
                                </TouchableOpacity>
                                <TouchableOpacity
                                  onPress={() =>
                                    removeIngredient(ingredient.id)
                                  }
                                  activeOpacity={0.7}
                                  className="p-1"
                                >
                                  <Trash2
                                    size={16}
                                    color={colors.textSecondary}
                                  />
                                </TouchableOpacity>
                              </View>
                            </TouchableOpacity>
                          )}
                        </View>
                      );
                    })}
                  </View>

                  {/* Add to Checklist Button */}
                  <TouchableOpacity
                    onPress={handleAddToChecklist}
                    disabled={
                      addToChecklist.isPending ||
                      missingIngredients.length === 0
                    }
                    className="mt-4 flex-row items-center justify-center gap-2 rounded-xl py-4"
                    style={{
                      backgroundColor: missingIngredients.length === 0
                        ? `${colors.accent}80`
                        : colors.accent
                    }}
                  >
                    <ShoppingCart size={18} color={colors.bg} />
                    <Text className="font-semibold" style={{ color: colors.bg }}>
                      {addToChecklist.isPending
                        ? "Adding..."
                        : missingIngredients.length === 0
                          ? "All Ingredients Available"
                          : `Add to Checklist`}
                    </Text>
                  </TouchableOpacity>
                </View>
              )}
            </View>

            {/* Nutrition Section */}
            <View className="mx-4 mb-4 rounded-2xl p-4" style={{ backgroundColor: colors.card }}>
              <View className="flex-row items-center justify-between mb-3">
                <Text className="font-semibold text-base" style={{ color: colors.textPrimary }}>
                  Daily RDA Nutrition
                </Text>
              </View>

              {nutrition ? (
                <NutritionDisplay
                  nutrition={nutrition}
                  servings={localState.servings}
                  colors={colors}
                />
              ) : (
                <Text className="text-sm text-center py-4" style={{ color: colors.textMuted }}>
                  Nutrition data not available.
                </Text>
              )}
            </View>

            {/* Cooking Steps Section - Collapsible Accordion */}
            <View className="mx-4 mb-4 rounded-2xl overflow-hidden" style={{ backgroundColor: colors.card }}>
              {/* Accordion Header */}
              <TouchableOpacity
                onPress={() => setStepsExpanded(!stepsExpanded)}
                className="flex-row items-center justify-between p-4"
                activeOpacity={0.7}
              >
                <View className="flex-row items-center flex-1">
                  <Text className="font-semibold text-base" style={{ color: colors.textPrimary }}>
                    Cooking Steps ({localState.steps.length})
                  </Text>
                  <View className="ml-2">
                    {stepsExpanded ? (
                      <ChevronUp size={20} color={colors.textSecondary} />
                    ) : (
                      <ChevronDown size={20} color={colors.textSecondary} />
                    )}
                  </View>
                </View>
                <TouchableOpacity
                  onPress={(e) => {
                    e.stopPropagation();
                    setShowAddStep(true);
                    setStepsExpanded(true);
                  }}
                  className="flex-row items-center rounded-full px-3 py-1.5"
                  style={{ backgroundColor: colors.bg }}
                >
                  <Plus size={14} color={colors.accent} />
                  <Text className="text-sm ml-1 font-medium" style={{ color: colors.accent }}>
                    Add
                  </Text>
                </TouchableOpacity>
              </TouchableOpacity>

              {/* Expandable Content */}
              {stepsExpanded && (
                <View className="px-4 pb-4">
                  {/* Add Step Form */}
                  {showAddStep && (
                    <View className="rounded-xl p-3 mb-3" style={{ backgroundColor: colors.bg }}>
                      <TextInput
                        value={newStepText}
                        onChangeText={setNewStepText}
                        placeholder="Describe this cooking step..."
                        placeholderTextColor={colors.textMuted}
                        className="rounded-lg px-3 py-3 mb-2"
                        style={{ color: colors.textPrimary, backgroundColor: colors.card }}
                        multiline
                        numberOfLines={3}
                        textAlignVertical="top"
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
                  {localState.steps.length > 0 ? (
                    <View className="space-y-3">
                      {localState.steps.map((step, index) => (
                        <View key={`${step}-${index}`}>
                          {editingStepIndex === index ? (
                            <View className="rounded-xl p-3" style={{ backgroundColor: colors.bg }}>
                              <TextInput
                                value={editingStepText}
                                onChangeText={setEditingStepText}
                                className="rounded-lg px-3 py-3 mb-2"
                                style={{ color: colors.textPrimary, backgroundColor: colors.card }}
                                multiline
                                numberOfLines={3}
                                textAlignVertical="top"
                                autoFocus
                              />
                              <View className="flex-row gap-2">
                                <TouchableOpacity
                                  onPress={() => {
                                    setEditingStepIndex(null);
                                    setEditingStepText("");
                                  }}
                                  className="flex-1 rounded-lg py-2"
                                  style={{ backgroundColor: colors.card }}
                                >
                                  <Text className="text-center" style={{ color: colors.textPrimary }}>
                                    Cancel
                                  </Text>
                                </TouchableOpacity>
                                <TouchableOpacity
                                  onPress={handleSaveEditingStep}
                                  className="flex-1 rounded-lg py-2"
                                  style={{ backgroundColor: colors.accent }}
                                >
                                  <Text className="text-center font-semibold" style={{ color: colors.bg }}>
                                    Save
                                  </Text>
                                </TouchableOpacity>
                              </View>
                            </View>
                          ) : (
                            <View className="flex-row rounded-xl p-3" style={{ backgroundColor: `${colors.bg}99` }}>
                              {/* Step number circle */}
                              <View className="w-7 h-7 rounded-full items-center justify-center mr-3 mt-0.5" style={{ backgroundColor: colors.accent }}>
                                <Text className="font-bold text-sm" style={{ color: colors.bg }}>
                                  {index + 1}
                                </Text>
                              </View>

                              {/* Step description */}
                              <Text className="text-base flex-1 leading-6" style={{ color: colors.textPrimary }}>
                                {step}
                              </Text>

                              {/* Action buttons */}
                              <View className="flex-row items-start ml-2">
                                <TouchableOpacity
                                  onPress={() => startEditingStep(index, step)}
                                  className="p-1.5"
                                >
                                  <Edit3
                                    size={16}
                                    color={colors.textMuted}
                                  />
                                </TouchableOpacity>
                                <TouchableOpacity
                                  onPress={() => removeStep(index)}
                                  className="p-1.5"
                                >
                                  <Trash2
                                    size={16}
                                    color={colors.textMuted}
                                  />
                                </TouchableOpacity>
                              </View>
                            </View>
                          )}
                        </View>
                      ))}
                    </View>
                  ) : (
                    <Text className="text-sm text-center py-4" style={{ color: colors.textMuted }}>
                      No cooking steps added yet.
                    </Text>
                  )}

                  {/* Start Cooking Assistant Button */}
                  <TouchableOpacity
                    onPress={handleStartCookingAssistant}
                    className="flex-row items-center justify-center gap-2 rounded-xl py-4 mt-4"
                    style={{ backgroundColor: colors.accent }}
                  >
                    <ChefHat size={18} color={colors.bg} />
                    <Text className="font-semibold" style={{ color: colors.bg }}>
                      Start Cooking Assistant
                    </Text>
                  </TouchableOpacity>
                </View>
              )}
            </View>

            {/* Bottom Padding */}
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

      {/* Recipe Poster Preview Modal */}
      <RecipePosterPreviewModal
        visible={isPosterPreviewVisible}
        recipe={posterData}
        onClose={handleClosePosterPreview}
      />
    </>
  );
}

// ============ Nutrition Display Component ============

interface NutritionDisplayProps {
  nutrition: RecipeNutrition;
  servings: number;
  colors: any;
}

function NutritionDisplay({ nutrition, servings, colors }: NutritionDisplayProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  const dailyValues = {
    calories: 2000,
    carbs: 300,
    fat: 65,
    protein: 50,
    sugar: 50,
    sodium: 2300,
    saturatedFat: 20,
    fiber: 28,
  };

  const calcPercentage = (value: number, daily: number) =>
    Math.round((value / daily) * 100);

  // Calculate calorie percentages from macros
  const carbCals = (nutrition.carbohydrates ?? 0) * 4;
  const proteinCals = (nutrition.protein ?? 0) * 4;
  const fatCals = (nutrition.fat ?? 0) * 9;
  const totalMacroCals = carbCals + proteinCals + fatCals;

  const carbPercentage =
    totalMacroCals > 0 ? Math.round((carbCals / totalMacroCals) * 100) : 0;
  const proteinPercentage =
    totalMacroCals > 0 ? Math.round((proteinCals / totalMacroCals) * 100) : 0;
  const fatPercentage =
    totalMacroCals > 0 ? Math.round((fatCals / totalMacroCals) * 100) : 0;

  return (
    <View>
      {/* Summary Row */}
      <View className="flex-row items-center mb-4">
        {/* Calorie Circle */}
        <View className="items-center justify-center mr-4">
          <View className="w-20 h-20 rounded-full border-4 items-center justify-center" style={{ borderColor: colors.accent }}>
            <Text className="text-xl font-bold" style={{ color: colors.textPrimary }}>
              {Math.round(nutrition.calories ?? 0)}
            </Text>
            <Text className="text-xs" style={{ color: colors.textMuted }}>cals</Text>
          </View>
        </View>

        {/* Macro Summary */}
        <View className="flex-1 flex-row justify-around">
          <View className="items-center">
            <Text className="font-bold" style={{ color: colors.textPrimary }}>
              {Math.round(nutrition.carbohydrates ?? 0)}g
            </Text>
            <Text className="text-xs" style={{ color: colors.textMuted }}>Carbs</Text>
            <Text className="text-[#5B9BD5] text-xs">
              {carbPercentage}% cals
            </Text>
          </View>
          <View className="items-center">
            <Text className="font-bold" style={{ color: colors.textPrimary }}>
              {Math.round(nutrition.fat ?? 0)}g
            </Text>
            <Text className="text-xs" style={{ color: colors.textMuted }}>Total fat</Text>
            <Text className="text-[#FFD700] text-xs">
              {fatPercentage}% cals
            </Text>
          </View>
          <View className="items-center">
            <Text className="font-bold" style={{ color: colors.textPrimary }}>
              {Math.round(nutrition.protein ?? 0)}g
            </Text>
            <Text className="text-xs" style={{ color: colors.textMuted }}>Protein</Text>
            <Text className="text-xs" style={{ color: colors.accent }}>
              {proteinPercentage}% cals
            </Text>
          </View>
        </View>
      </View>

      {/* Detailed Nutrition */}
      <TouchableOpacity
        onPress={() => setIsExpanded(!isExpanded)}
        className="border-t pt-3 flex-row items-center justify-between"
        style={{ borderTopColor: colors.border }}
      >
        <Text className="font-medium" style={{ color: colors.textPrimary }}>Daily RDA Nutrition</Text>
        <Text style={{ color: colors.textMuted }}>{isExpanded ? "▲" : "▼"}</Text>
      </TouchableOpacity>

      {isExpanded && (
        <View className="pt-2">
          <NutritionRow label="Servings" value={servings} unit="" colors={colors} />
          <NutritionRow
            label="Calories"
            value={Math.round(nutrition.calories ?? 0)}
            unit="kcal"
            percentage={calcPercentage(
              nutrition.calories ?? 0,
              dailyValues.calories,
            )}
            colors={colors}
          />
          <NutritionRow
            label="Carbs"
            value={Math.round(nutrition.carbohydrates ?? 0)}
            unit="g"
            percentage={calcPercentage(
              nutrition.carbohydrates ?? 0,
              dailyValues.carbs,
            )}
            colors={colors}
          />
          <NutritionRow
            label="Protein"
            value={Math.round(nutrition.protein ?? 0)}
            unit="g"
            percentage={calcPercentage(
              nutrition.protein ?? 0,
              dailyValues.protein,
            )}
            colors={colors}
          />
          <NutritionRow
            label="Total fat"
            value={Math.round(nutrition.fat ?? 0)}
            unit="g"
            percentage={calcPercentage(nutrition.fat ?? 0, dailyValues.fat)}
            colors={colors}
          />
          <NutritionRow
            label="Saturated fat"
            value={Math.round(nutrition.saturatedFat ?? 0)}
            unit="g"
            percentage={calcPercentage(
              nutrition.saturatedFat ?? 0,
              dailyValues.saturatedFat,
            )}
            isSubItem
            colors={colors}
          />
          <NutritionRow
            label="Sugars"
            value={Math.round(nutrition.sugar ?? 0)}
            unit="g"
            percentage={calcPercentage(nutrition.sugar ?? 0, dailyValues.sugar)}
            colors={colors}
          />
          <NutritionRow
            label="Sodium"
            value={Math.round(nutrition.sodium ?? 0)}
            unit="mg"
            percentage={calcPercentage(
              nutrition.sodium ?? 0,
              dailyValues.sodium,
            )}
            colors={colors}
          />
        </View>
      )}
    </View>
  );
}

interface NutritionRowProps {
  label: string;
  value: number;
  unit: string;
  percentage?: number;
  isSubItem?: boolean;
  colors: any;
}

function NutritionRow({
  label,
  value,
  unit,
  percentage,
  isSubItem = false,
  colors,
}: NutritionRowProps) {
  return (
    <View className={`py-2 ${isSubItem ? "pl-4" : ""}`}>
      <View className="flex-row justify-between items-center mb-1">
        <Text className={isSubItem ? "text-sm" : "font-medium"} style={{ color: colors.textPrimary }}>
          {label}
        </Text>
        <Text className="text-sm" style={{ color: colors.textSecondary }}>
          {value} {unit}
          {percentage !== undefined ? ` (${percentage}%)` : ""}
        </Text>
      </View>
      {percentage !== undefined && (
        <View className="h-1.5 rounded-full overflow-hidden" style={{ backgroundColor: colors.border }}>
          <View
            className="h-full rounded-full"
            style={{ width: `${Math.min(100, percentage)}%`, backgroundColor: colors.accent }}
          />
        </View>
      )}
    </View>
  );
}
