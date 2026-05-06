import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from "react";
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { Image } from "expo-image";
import * as ImagePicker from "expo-image-picker";
import { useQueryClient } from "@tanstack/react-query";
import {
  ChevronDown,
  Globe2,
  Loader2,
  LockKeyhole,
  Plus,
  Trash2,
  Upload,
  X,
} from "lucide-react-native";

import { Button } from "@/components/Button";
import { Badge } from "@/components/badge";
import { Input } from "@/components/input";
import { toast } from "@/components/sonner";
import { UnitSelectorModal } from "@/components/UnitSelectorModal";
import { useAuthMutation, useAuthQuery, useImageUpload } from "@/hooks/useApi";
import type { ApiResponse } from "@/types/api";
import {
  CreateRecipeRequest,
  RecipeDetailDto,
  RecipeVisibility,
} from "@/types/recipes";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";
import { compressImageIfNeeded } from "@/utils/imageCompression";

type Step = { id: string; text: string };
type Ingredient = {
  id: string;
  name: string;
  amount: string;
  unit: string;
  isOptional: boolean;
};
type LocalImage = {
  id: string;
  uri: string;
  status: "uploading" | "uploaded" | "error";
  url?: string;
};

const SUGGESTED_TAGS = ["HomeStyle", "QuickMeal", "Healthy", "WeightLoss"];

type RecipeEditFormProps = {
  recipeId: string;
  onSaved?: () => void;
  onDeleted?: () => void;
  className?: string;
  hideActions?: boolean;
  onStatusChange?: (status: {
    isSaving: boolean;
    isDeleting: boolean;
    isReady?: boolean;
  }) => void;
};

export type RecipeEditFormRef = {
  save: () => void;
  delete: () => void;
  isSaving: boolean;
  isDeleting: boolean;
};

const sectionClass = "mt-4 gap-2";

const buildBasePayloadFromDetail = (
  detail: RecipeDetailDto,
): CreateRecipeRequest => {
  const sanitizedImages =
    detail.imageUrls?.filter((url): url is string => Boolean(url)) ?? [];
  return {
    title: detail.title,
    description: detail.description,
    steps: detail.steps ?? [],
    visibility: detail.visibility,
    imageUrls: sanitizedImages,
    tags: detail.tags ?? [],
    servings: detail.servings ?? undefined,
    totalTimeMinutes: detail.totalTimeMinutes ?? undefined,
    difficulty: detail.difficulty ?? undefined,
  };
};

export const RecipeEditForm = forwardRef<
  RecipeEditFormRef,
  RecipeEditFormProps
>(
  (
    { recipeId, onSaved, onDeleted, className, hideActions, onStatusChange },
    ref,
  ) => {
    const { colors } = useTheme();
    const queryClient = useQueryClient();
    const [title, setTitle] = useState("");
    const [description, setDescription] = useState("");
    const [steps, setSteps] = useState<Step[]>([{ id: "step-1", text: "" }]);
    const [ingredients, setIngredients] = useState<Ingredient[]>([
      { id: "ing-1", name: "", amount: "", unit: "", isOptional: false },
    ]);
    const [images, setImages] = useState<LocalImage[]>([]);
    const [tagInput, setTagInput] = useState("");
    const [tags, setTags] = useState<string[]>([]);
    const [visibility, setVisibility] = useState<RecipeVisibility>("Public");
    const [isPrefilled, setIsPrefilled] = useState(false);
    const [originalRecipe, setOriginalRecipe] =
      useState<RecipeDetailDto | null>(null);
    const tagInputRef = useRef("");
    const [unitDropdownId, setUnitDropdownId] = useState<string | null>(null);
    const MAX_IMAGES = 9;

    const { data, isLoading, isError, error, refetch } = useAuthQuery<
      ApiResponse<RecipeDetailDto>
    >(["recipe", recipeId], `/api/recipes/${recipeId}`, {
      enabled: Boolean(recipeId),
    });

    useEffect(() => {
      setIsPrefilled(false);
      setOriginalRecipe(null);
      setTitle("");
      setDescription("");
      setSteps([{ id: "step-1", text: "" }]);
      setIngredients([{ id: "ing-1", name: "", amount: "", unit: "", isOptional: false }]);
      setImages([]);
      setTags([]);
      setTagInput("");
      tagInputRef.current = "";
      setVisibility("Public");
    }, [recipeId]);

    useEffect(() => {
      if (!data?.data || isPrefilled) return;
      const detail = data.data;
      const nextTitle = detail.title ?? "";
      const nextDescription = detail.description ?? "";
      setTitle(nextTitle);
      setDescription(nextDescription);
      setOriginalRecipe(detail);
      setVisibility(detail.visibility);
      setTags(detail.tags ?? []);
      setSteps(
        (detail.steps ?? [""]).map((text, index) => ({
          id: `step-${index + 1}`,
          text,
        })),
      );
      setIngredients(
        (detail.ingredients ?? []).length > 0
          ? detail.ingredients!.map((ing, index) => ({
              id: `ing-${index + 1}`,
              name: ing.name ?? "",
              amount: ing.amount != null ? String(ing.amount) : "",
              unit: ing.unit ?? "",
              isOptional: ing.isOptional ?? false,
            }))
          : [{ id: "ing-1", name: "", amount: "", unit: "", isOptional: false }],
      );
      setImages(
        (detail.imageUrls ?? []).map((url, index) => ({
          id: `img-${index}`,
          uri: url,
          status: "uploaded",
          url,
        })),
      );
      setIsPrefilled(true);
    }, [data?.data, isPrefilled]);

    const uploadMutation = useImageUpload();
    const updateMutation = useAuthMutation<
      ApiResponse<RecipeDetailDto>,
      CreateRecipeRequest
    >(() => `/api/recipes/${recipeId}`, "PUT", {
      onSuccess: (response) => {
        console.log(
          "PUT success",
          (response as { status?: number })?.status ?? "ok",
        );
        toast.success("Recipe updated");
        queryClient.invalidateQueries({ queryKey: ["my-recipes"] });
        queryClient.invalidateQueries({ queryKey: ["recipe", recipeId] });
        queryClient.invalidateQueries({
          queryKey: ["community-recipes", "scope:community"],
        });
        queryClient.invalidateQueries({ queryKey: ["me-likes"] });
        queryClient.invalidateQueries({ queryKey: ["me-likes-count"] });
        queryClient.invalidateQueries({ queryKey: ["me-saves"] });
        queryClient.invalidateQueries({ queryKey: ["me-saves-count"] });
        onSaved?.();
      },
      onError: (err) => {
        console.log("PUT error", err?.message ?? "unknown");
        toast.error(err.message ?? "Failed to update recipe");
      },
    });
    const deleteMutation = useAuthMutation<ApiResponse<void>, void>(
      () => `/api/recipes/${recipeId}`,
      "DELETE",
      {
        onSuccess: () => {
          toast.success("Recipe deleted");
          queryClient.invalidateQueries({ queryKey: ["my-recipes"] });
          queryClient.invalidateQueries({ queryKey: ["recipe", recipeId] });
          queryClient.invalidateQueries({
            queryKey: ["community-recipes", "scope:community"],
          });
          onDeleted?.();
        },
        onError: (err) => {
          toast.error(err.message ?? "Failed to delete recipe");
        },
      },
    );

    const handlePickImages = async () => {
      if (images.length >= MAX_IMAGES) {
        Alert.alert(
          "Limit reached",
          `You can upload up to ${MAX_IMAGES} images.`,
        );
        return;
      }

      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ["images"],
        allowsMultipleSelection: true,
        selectionLimit: MAX_IMAGES - images.length,
        quality: 0.8,
      });

      if (result.canceled || !result.assets?.length) return;

      // Compress images if needed and track compression count
      let compressedCount = 0;
      const processedAssets: { id: string; uri: string }[] = [];

      for (const asset of result.assets) {
        const compressed = await compressImageIfNeeded(asset.uri, asset.fileSize);
        if (compressed.wasCompressed) {
          compressedCount++;
        }
        processedAssets.push({
          id: `img-${Date.now()}-${Math.random().toString(36).slice(2)}`,
          uri: compressed.uri,
        });
      }

      if (compressedCount > 0) {
        toast.info(
          compressedCount === 1
            ? "Image compressed for upload"
            : `${compressedCount} images compressed for upload`
        );
      }

      const newImages: LocalImage[] = processedAssets.map((asset) => ({
        id: asset.id,
        uri: asset.uri,
        status: "uploading",
      }));
      setImages((prev) => [...prev, ...newImages]);

      for (const image of newImages) {
        try {
          const response = await uploadMutation.mutateAsync(image.uri);
          setImages((prev) =>
            prev.map((img) =>
              img.id === image.id
                ? { ...img, status: "uploaded", url: response.url }
                : img,
            ),
          );
        } catch {
          setImages((prev) =>
            prev.map((img) =>
              img.id === image.id ? { ...img, status: "error" } : img,
            ),
          );
        }
      }
    };

    const handleRemoveImage = (id: string) => {
      setImages((prev) => prev.filter((img) => img.id !== id));
    };

    const handleAddStep = () => {
      setSteps((prev) => [...prev, { id: `step-${Date.now()}`, text: "" }]);
    };

    const handleUpdateStep = (id: string, text: string) => {
      setSteps((prev) =>
        prev.map((step) => (step.id === id ? { ...step, text } : step)),
      );
    };

    const handleRemoveStep = (id: string) => {
      setSteps((prev) =>
        prev.length === 1 ? prev : prev.filter((step) => step.id !== id),
      );
    };

    const handleAddIngredient = () => {
      setIngredients((prev) => [
        ...prev,
        { id: `ing-${Date.now()}`, name: "", amount: "", unit: "", isOptional: false },
      ]);
    };

    const handleUpdateIngredient = (
      id: string,
      field: keyof Ingredient,
      value: string | boolean,
    ) => {
      setIngredients((prev) =>
        prev.map((ing) => (ing.id === id ? { ...ing, [field]: value } : ing)),
      );
    };

    const handleRemoveIngredient = (id: string) => {
      setIngredients((prev) =>
        prev.length === 1 ? prev : prev.filter((ing) => ing.id !== id),
      );
    };

    const normalizeTag = (value: string) => value.trim().toLowerCase();
    const sanitizeTag = (value: string) => value.trim();
    // Split on both regular comma and Chinese comma
    const parseTagInputValue = (value: string) =>
      value
        .split(/[,，]/)
        .map((tag) => sanitizeTag(tag))
        .filter(Boolean);

    const mergeUniqueTags = (current: string[], incoming: string[]) => {
      if (incoming.length === 0) return current;
      const seen = new Set(current.map(normalizeTag));
      const result = [...current];
      incoming.forEach((raw) => {
        const sanitized = sanitizeTag(raw);
        if (!sanitized) return;
        const normalized = normalizeTag(sanitized);
        if (!seen.has(normalized)) {
          seen.add(normalized);
          result.push(sanitized);
        }
      });
      return result;
    };

    const commitTagInput = () => {
      const parsed = parseTagInputValue(tagInputRef.current);
      setTags((prev) => mergeUniqueTags(prev, parsed));
      tagInputRef.current = "";
      setTagInput("");
    };

    const toggleSuggestedTag = (value: string) => {
      setTags((prev) => {
        const normalized = normalizeTag(value);
        const exists = prev.some((tag) => normalizeTag(tag) === normalized);
        if (exists) {
          return prev.filter((tag) => normalizeTag(tag) !== normalized);
        }
        return mergeUniqueTags(prev, [value]);
      });
    };

    const isTagActive = (value: string) =>
      tags.some((tag) => normalizeTag(tag) === normalizeTag(value));

    const handleSave = useCallback(() => {
      if (!recipeId || !originalRecipe) {
        return;
      }

      const hasUploadingImages = images.some(
        (image) => image.status === "uploading",
      );
      if (hasUploadingImages) {
        toast.info("Please wait for images to finish uploading.");
        return;
      }

      const hasFailedUploads = images.some((image) => image.status === "error");
      if (hasFailedUploads) {
        toast.info("Images with upload errors will be skipped.");
      }

      const trimmedTitle = title.trim();
      const trimmedDescription = description.trim();
      const fallbackTitle = originalRecipe.title?.trim() ?? "";
      const fallbackDescription = originalRecipe.description?.trim() ?? "";
      const finalTitle = trimmedTitle || fallbackTitle;
      const finalDescription = trimmedDescription || fallbackDescription;

      if (!finalTitle || !finalDescription) {
        Alert.alert("Missing fields", "Title and description are required.");
        return;
      }

      const stepPayload = steps
        .map((step) => step.text.trim())
        .filter((text) => text.length > 0);
      if (stepPayload.length === 0) {
        Alert.alert("Missing steps", "Please add at least one cooking step.");
        return;
      }

      const uploadedImageUrls = images
        .filter((img) => img.status === "uploaded" && img.url)
        .map((img) => img.url as string);
      const normalizedTags = Array.from(
        new Set(tags.map((tag) => tag.trim()).filter(Boolean)),
      );

      const ingredientPayload = ingredients
        .filter((ing) => ing.name.trim().length > 0)
        .map((ing) => ({
          name: ing.name.trim(),
          amount: ing.amount.trim() ? parseFloat(ing.amount) : null,
          unit: ing.unit.trim() || null,
          isOptional: ing.isOptional,
        }));

      const basePayload = buildBasePayloadFromDetail(originalRecipe);
      const payload: CreateRecipeRequest = {
        ...basePayload,
        title: finalTitle,
        description: finalDescription,
        steps: stepPayload,
        ingredients: ingredientPayload,
        visibility,
        imageUrls: uploadedImageUrls,
        tags: normalizedTags,
      };
      console.log("SAVE recipeId", recipeId);
      console.log("finalPayload", payload);
      updateMutation.mutate(payload);
    }, [
      recipeId,
      originalRecipe,
      images,
      title,
      description,
      steps,
      ingredients,
      visibility,
      tags,
      updateMutation,
    ]);

    const handleDelete = useCallback(() => {
      if (!recipeId) return;
      Alert.alert(
        "Delete Recipe",
        "This action cannot be undone. Are you sure you want to delete this recipe?",
        [
          { text: "Cancel", style: "cancel" },
          {
            text: "Delete",
            style: "destructive",
            onPress: () => deleteMutation.mutate(),
          },
        ],
      );
    }, [recipeId, deleteMutation]);

    useImperativeHandle(
      ref,
      () => ({
        save: handleSave,
        delete: handleDelete,
        isSaving: updateMutation.isPending,
        isDeleting: deleteMutation.isPending,
      }),
      [
        handleSave,
        handleDelete,
        updateMutation.isPending,
        deleteMutation.isPending,
      ],
    );

    useEffect(() => {
      onStatusChange?.({
        isSaving: updateMutation.isPending,
        isDeleting: deleteMutation.isPending,
        isReady: Boolean(originalRecipe),
      });
    }, [
      onStatusChange,
      updateMutation.isPending,
      deleteMutation.isPending,
      originalRecipe,
    ]);

    if (!recipeId) {
      return (
        <View className="items-center justify-center rounded-3xl border px-4 py-8" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
          <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
            Invalid recipe.
          </Text>
        </View>
      );
    }

    if (isLoading && !isPrefilled) {
      return (
        <View className="items-center justify-center rounded-3xl border px-4 py-8" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
          <ActivityIndicator color={colors.accent} />
          <Text className="mt-3 text-sm" style={{ color: colors.textSecondary }}>Loading recipe...</Text>
        </View>
      );
    }

    if (isError) {
      return (
        <View className="items-center justify-center rounded-3xl border border-red-200/40 bg-red-200/10 px-4 py-8">
          <Text className="text-center text-base font-semibold text-red-200">
            Unable to load recipe
          </Text>
          <Text className="mt-2 text-center text-sm text-red-100">
            {error?.message ?? "Please try again later."}
          </Text>
          <TouchableOpacity
            onPress={() => refetch()}
            className="mt-4 rounded-full px-5 py-2"
            style={{ backgroundColor: colors.card }}
          >
            <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Retry</Text>
          </TouchableOpacity>
        </View>
      );
    }

    return (
    <View className={cn("rounded-2xl px-4 py-3", className)}>
        <ScrollView
          showsVerticalScrollIndicator={false}
          contentContainerClassName="pb-10"
        >
          <View className={sectionClass}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              Recipe Title *
            </Text>
            <Input
              value={title}
              onChangeText={setTitle}
              placeholder="Recipe name"
              placeholderTextColor={colors.textMuted}
              className="rounded-xl text-lg"
              style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
            />
          </View>

          <View className={sectionClass}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              Description *
            </Text>
            <Input
              value={description}
              onChangeText={setDescription}
              placeholder="Description"
              placeholderTextColor={colors.textMuted}
              multiline
              numberOfLines={3}
              className="min-h-[96px] rounded-xl px-3 py-3 text-lg"
              style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
            />
          </View>

          <View className={sectionClass}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>Images</Text>
            <View className="flex-row flex-wrap gap-3">
              {images.map((image) => (
                <View key={image.id} className="relative">
                  <Image
                    source={{ uri: image.uri }}
                    className="h-24 w-24 rounded-2xl border"
                    style={{ borderColor: colors.border }}
                  />
                  <TouchableOpacity
                    onPress={() => handleRemoveImage(image.id)}
                    className="absolute -right-2 -top-2 h-7 w-7 items-center justify-center rounded-full bg-black/60"
                  >
                    <X size={16} color={colors.overlayText} />
                  </TouchableOpacity>
                  {image.status === "uploading" ? (
                    <View className="absolute inset-0 items-center justify-center rounded-2xl bg-black/40">
                      <Loader2
                        size={18}
                        color={colors.overlayText}
                        className="animate-spin"
                      />
                    </View>
                  ) : null}
                </View>
              ))}
              {images.length < MAX_IMAGES ? (
                <TouchableOpacity
                  onPress={handlePickImages}
                  className="h-24 w-24 items-center justify-center rounded-xl border border-dashed"
                  style={{ borderColor: colors.border, backgroundColor: colors.card }}
                >
                  <Upload size={24} color={colors.textSecondary} />
                  <Text className="mt-2 text-base font-semibold" style={{ color: colors.textSecondary }}>
                    Upload
                  </Text>
                </TouchableOpacity>
              ) : null}
            </View>
            <Text className="text-sm" style={{ color: colors.textSecondary }}>Upload up to 9 images</Text>
          </View>

          <View className={sectionClass}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              Ingredients
            </Text>

            <View className="gap-3">
              {ingredients.map((ingredient, index) => (
                <View
                  key={ingredient.id}
                  className="rounded-2xl p-3"
                  style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
                >
                  <View className="flex-row items-center gap-2">
                    <View className="flex-1">
                      <Input
                        value={ingredient.name}
                        onChangeText={(text) => handleUpdateIngredient(ingredient.id, "name", text)}
                        placeholder="Ingredient name"
                        placeholderTextColor={colors.textMuted}
                        className="rounded-lg px-3 py-2 text-base"
                        style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
                      />
                    </View>
                    {ingredients.length > 1 ? (
                      <TouchableOpacity
                        onPress={() => handleRemoveIngredient(ingredient.id)}
                        className="h-10 w-10 items-center justify-center rounded-lg"
                        style={{ backgroundColor: colors.card }}
                      >
                        <Trash2 size={18} color="#FCA5A5" />
                      </TouchableOpacity>
                    ) : null}
                  </View>
                  <View className="mt-2 flex-row items-center gap-2">
                    <View className="flex-1">
                      <Input
                        value={ingredient.amount}
                        onChangeText={(text) => handleUpdateIngredient(ingredient.id, "amount", text)}
                        placeholder="Amount"
                        placeholderTextColor={colors.textMuted}
                        keyboardType="decimal-pad"
                        className="rounded-lg px-3 py-2 text-base"
                        style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
                      />
                    </View>
                    <View className="flex-1">
                      <TouchableOpacity
                        onPress={() => setUnitDropdownId(ingredient.id)}
                        className="h-10 flex-row items-center justify-between rounded-lg border px-3"
                        style={{ borderColor: colors.border, backgroundColor: colors.card }}
                      >
                        <Text
                          className="text-base"
                          style={{ color: ingredient.unit ? colors.textPrimary : colors.textMuted }}
                        >
                          {ingredient.unit || "Unit"}
                        </Text>
                        <ChevronDown size={16} color={colors.textSecondary} />
                      </TouchableOpacity>
                    </View>
                  </View>
                  <TouchableOpacity
                    onPress={() => handleUpdateIngredient(ingredient.id, "isOptional", !ingredient.isOptional)}
                    className="mt-2 flex-row items-center gap-2"
                  >
                    <View
                      className="h-5 w-5 items-center justify-center rounded border"
                      style={{
                        borderColor: ingredient.isOptional ? colors.accent : colors.border,
                        backgroundColor: ingredient.isOptional ? colors.accent : "transparent",
                      }}
                    >
                      {ingredient.isOptional && (
                        <Text className="text-xs font-bold" style={{ color: colors.bg }}>✓</Text>
                      )}
                    </View>
                    <Text className="text-sm" style={{ color: colors.textSecondary }}>Optional ingredient</Text>
                  </TouchableOpacity>
                </View>
              ))}
            </View>
            <Button
              variant="outline"
              className="mt-3 rounded-xl bg-transparent py-2"
              style={{ borderColor: colors.border }}
              onPress={handleAddIngredient}
            >
              <View className="flex-row items-center justify-center gap-2">
                <Plus size={16} color={colors.textPrimary} />
                <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                  Add Ingredient
                </Text>
              </View>
            </Button>
          </View>

          <View className={sectionClass}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              Cooking Steps *
            </Text>

            <View className="gap-3">
              {steps.map((step, index) => (
                <View
                  key={step.id}
                  className="flex-row items-start rounded-2xl border p-3"
                  style={{ backgroundColor: colors.card, borderColor: colors.border }}
                >
                  <View className="h-8 w-8 items-center justify-center rounded-full" style={{ backgroundColor: colors.accent }}>
                    <Text className="font-bold" style={{ color: colors.bg }}>
                      {index + 1}
                    </Text>
                  </View>
                  <View className="ml-3 flex-1">
                    <Input
                      value={step.text}
                      onChangeText={(text) => handleUpdateStep(step.id, text)}
                      placeholder={`Step ${index + 1}...`}
                      placeholderTextColor={colors.textMuted}
                      multiline
                      className="min-h-[64px] rounded-lg px-3 py-2 text-lg"
                      style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
                    />
                  </View>
                  {steps.length > 1 ? (
                    <TouchableOpacity
                      onPress={() => handleRemoveStep(step.id)}
                      className="ml-2 h-10 w-10 items-center justify-center rounded-lg"
                      style={{ backgroundColor: colors.card }}
                    >
                      <Trash2 size={18} color="#FCA5A5" />
                    </TouchableOpacity>
                  ) : null}
                </View>
              ))}
            </View>
            <Button
              variant="outline"
              className="mt-3 rounded-xl bg-transparent py-2"
              style={{ borderColor: colors.border }}
              onPress={handleAddStep}
            >
              <View className="flex-row items-center justify-center gap-2">
                <Plus size={16} color={colors.textPrimary} />
                <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                  Add Step
                </Text>
              </View>
            </Button>
          </View>

          <View className={sectionClass}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>Tags</Text>
            <Input
              value={tagInput}
              onChangeText={(value) => {
                // Check if value ends with a comma (regular or Chinese) or contains any comma (for paste)
                const endsWithComma = /[,，]$/.test(value);
                const containsComma = /[,，]/.test(value);

                if (endsWithComma || containsComma) {
                  // Parse and commit all tags from the input
                  const parsed = parseTagInputValue(value);
                  if (parsed.length > 0) {
                    setTags((prev) => mergeUniqueTags(prev, parsed));
                  }
                  // Clear the input
                  tagInputRef.current = "";
                  setTagInput("");
                } else {
                  tagInputRef.current = value;
                  setTagInput(value);
                }
              }}
              onSubmitEditing={commitTagInput}
              placeholder="Tag1, Tag2"
              placeholderTextColor={colors.textMuted}
              className="rounded-xl text-lg"
              style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
            />
            <Button
              variant="outline"
              className="mt-3 rounded-2xl border"
              style={{ borderColor: `${colors.accent}80`, backgroundColor: `${colors.accent}33` }}
              onPress={commitTagInput}
            >
              <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>Add Tags</Text>
            </Button>
            <View className="mt-3 flex-row flex-wrap gap-2">
              {tags.map((tag) => (
                <View
                  key={`${tag}-tag-pill`}
                  className="flex-row items-center gap-1 rounded-full border px-3 py-1"
                  style={{ backgroundColor: colors.card, borderColor: colors.border }}
                >
                  <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                    {tag}
                  </Text>
                  <TouchableOpacity
                    onPress={() =>
                      setTags((prev) => prev.filter((t) => t !== tag))
                    }
                  >
                    <X size={14} color={colors.textPrimary} />
                  </TouchableOpacity>
                </View>
              ))}
            </View>
            <View className="mt-3 flex-row flex-wrap gap-2">
              {SUGGESTED_TAGS.map((tag) => (
                <TouchableOpacity
                  key={`suggested-${tag}`}
                  onPress={() => toggleSuggestedTag(tag)}
                  activeOpacity={0.85}
                >
                  <Badge
                    className="border px-3 py-1"
                    style={{
                      borderColor: isTagActive(tag) ? `${colors.accent}B3` : colors.border,
                      backgroundColor: isTagActive(tag) ? `${colors.accent}59` : "transparent",
                    }}
                  >
                    <Text
                      className="text-sm font-semibold"
                      style={{ color: isTagActive(tag) ? colors.textPrimary : colors.textSecondary }}
                    >
                      {tag}
                    </Text>
                  </Badge>
                </TouchableOpacity>
              ))}
            </View>
          </View>

          <View className={sectionClass}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>Visibility</Text>
            <View className="mt-3 flex-row gap-3">
              <TouchableOpacity
                className="flex-1 items-center rounded-2xl border px-4 py-3"
                style={{
                  borderColor: visibility === "Public" ? colors.accent : colors.border,
                  backgroundColor: visibility === "Public" ? `${colors.accent}33` : "transparent",
                }}
                onPress={() => setVisibility("Public")}
              >
                <Globe2 size={20} color={colors.textSecondary} />
                <Text className="mt-1 text-base font-semibold" style={{ color: colors.textPrimary }}>
                  Public
                </Text>
                <Text className="text-sm" style={{ color: colors.textSecondary }}>Visible to all</Text>
              </TouchableOpacity>
              <TouchableOpacity
                className="flex-1 items-center rounded-2xl border px-4 py-3"
                style={{
                  borderColor: visibility === "Private" ? colors.accent : colors.border,
                  backgroundColor: visibility === "Private" ? `${colors.accent}33` : "transparent",
                }}
                onPress={() => setVisibility("Private")}
              >
                <LockKeyhole size={20} color={colors.textSecondary} />
                <Text className="mt-1 text-base font-semibold" style={{ color: colors.textPrimary }}>
                  Private
                </Text>
                <Text className="text-sm" style={{ color: colors.textSecondary }}>Only me</Text>
              </TouchableOpacity>
            </View>
          </View>

          {!hideActions && (
            <View className="mt-6 space-y-3">
              <Button
                className="rounded-full py-3"
                style={{ backgroundColor: colors.accent }}
                disabled={updateMutation.isPending}
                onPress={handleSave}
              >
                {updateMutation.isPending ? (
                  <View className="flex-row items-center justify-center gap-2">
                    <ActivityIndicator color={colors.bg} />
                    <Text className="text-base font-semibold" style={{ color: colors.bg }}>
                      Saving...
                    </Text>
                  </View>
                ) : (
                  <Text className="text-base font-semibold" style={{ color: colors.bg }}>
                    Save Changes
                  </Text>
                )}
              </Button>
              <Button
                variant="outline"
                className="rounded-full border py-3"
                style={{ borderColor: colors.border }}
                disabled={deleteMutation.isPending}
                onPress={handleDelete}
              >
                {deleteMutation.isPending ? (
                  <View className="flex-row items-center justify-center gap-2">
                    <ActivityIndicator color={colors.textSecondary} />
                    <Text className="text-base font-semibold" style={{ color: colors.textSecondary }}>
                      Deleting...
                    </Text>
                  </View>
                ) : (
                  <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                    Delete Recipe
                  </Text>
                )}
              </Button>
            </View>
          )}
        </ScrollView>

        <UnitSelectorModal
          visible={unitDropdownId !== null}
          currentUnit={ingredients.find((i) => i.id === unitDropdownId)?.unit}
          onSelect={(unit) => {
            if (unitDropdownId) {
              handleUpdateIngredient(unitDropdownId, "unit", unit);
            }
          }}
          onClose={() => setUnitDropdownId(null)}
        />
      </View>
    );
  },
);

RecipeEditForm.displayName = "RecipeEditForm";
