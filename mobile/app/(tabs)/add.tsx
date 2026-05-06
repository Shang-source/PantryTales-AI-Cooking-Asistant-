import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import {
  ActivityIndicator,
  ScrollView,
  Text,
  TouchableOpacity,
  View,
  Alert,
  Modal,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { useRouter, useLocalSearchParams } from "expo-router";
import { Image } from "expo-image";
import * as ImagePicker from "expo-image-picker";
import {
  ChevronDown,
  Eraser,
  Globe2,
  LockKeyhole,
  Plus,
  Trash2,
  Upload,
  X,
  Loader2,
  Sparkles,
} from "lucide-react-native";

import { Button } from "@/components/Button";
import { Badge } from "@/components/badge";
import { Input } from "@/components/input";
import { toast } from "@/components/sonner";
import { UnitSelectorModal } from "@/components/UnitSelectorModal";
import { useQueryClient } from "@tanstack/react-query";
import { useAuthMutation, useImageUpload } from "@/hooks/useApi";
import { useGenerateRecipeContent } from "@/hooks/useVisionRecognition";
import { ApiResponse } from "@/types/api";
import {
  CreateRecipeRequest,
  RecipeCardDto,
  RecipeDetailDto,
  RecipeVisibility,
} from "@/types/recipes";
import { cn } from "@/utils/cn";
import { communityRecipesKey, myRecipesKey } from "@/hooks/useRecipeLikes";
import { useTheme } from "@/contexts/ThemeContext";
import { compressImageIfNeeded } from "@/utils/imageCompression";

type Step = { id: string; text: string };
type Ingredient = {
  id: string;
  name: string;
  amount: string;
  unit?: string;
};
type LocalImage = {
  id: string;
  uri: string;
  status: "uploading" | "uploaded" | "error";
  url?: string;
};

const SUGGESTED_TAGS = ["HomeStyle", "QuickMeal", "Healthy", "WeightLoss"];

export default function AddScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const { colors } = useTheme();
  const params = useLocalSearchParams<{
    prefillTitle?: string;
    prefillDescription?: string;
    prefillSteps?: string;
    prefillIngredients?: string;
    prefillTags?: string;
    prefillImageUri?: string;
  }>();
  const palette = {
    bg: colors.bg,
    border: colors.border,
    panel: colors.card,
    text: "#E5E7EB",
    accent: colors.accent,
  };
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [steps, setSteps] = useState<Step[]>([]);
  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [images, setImages] = useState<LocalImage[]>([]);
  const [tagInput, setTagInput] = useState("");
  const [tags, setTags] = useState<string[]>([]);
  const tagInputRef = useRef("");
  const clearTagInput = () => {
    tagInputRef.current = "";
    setTagInput("");
  };
  const handleTagInputChange = (value: string) => {
    // Check if value ends with a comma (regular or Chinese)
    const endsWithComma = /[,，]$/.test(value);
    // Check if value contains any comma (for paste handling)
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
  };
  const [visibility, setVisibility] = useState<RecipeVisibility>("Public");
  const [showReplaceDialog, setShowReplaceDialog] = useState(false);
  const [showClearDialog, setShowClearDialog] = useState(false);
  const [prefillApplied, setPrefillApplied] = useState(false);
  const [unitDropdownId, setUnitDropdownId] = useState<string | null>(null);

  // Image upload state
  const MAX_IMAGES = 9;
  const uploadMutation = useImageUpload();

  // Pre-fill form from navigation params (e.g., from recipe scan)
  useEffect(() => {
    if (prefillApplied) return;

    const hasPrefillData = params.prefillTitle || params.prefillSteps || params.prefillIngredients;
    if (!hasPrefillData) return;

    // Mark as applied to prevent re-running
    setPrefillApplied(true);

    // Pre-fill title
    if (params.prefillTitle) {
      setTitle(params.prefillTitle);
    }

    // Pre-fill description
    if (params.prefillDescription) {
      setDescription(params.prefillDescription);
    }

    // Pre-fill steps
    if (params.prefillSteps) {
      try {
        const parsedSteps = JSON.parse(params.prefillSteps) as string[];
        if (Array.isArray(parsedSteps) && parsedSteps.length > 0) {
          setSteps(
            parsedSteps.map((text, index) => ({
              id: `step-${Date.now()}-${index}`,
              text,
            }))
          );
        }
      } catch {
        // Ignore parse errors
      }
    }

    // Pre-fill ingredients
    if (params.prefillIngredients) {
      try {
        const parsedIngredients = JSON.parse(params.prefillIngredients) as {
          name: string;
          amount?: number;
          unit?: string;
        }[];
        if (Array.isArray(parsedIngredients) && parsedIngredients.length > 0) {
          setIngredients(
            parsedIngredients.map((ing, index) => ({
              id: `ing-${Date.now()}-${index}`,
              name: ing.name,
              amount: ing.amount?.toString() ?? "",
              unit: ing.unit,
            }))
          );
        }
      } catch {
        // Ignore parse errors
      }
    }

    // Pre-fill tags
    if (params.prefillTags) {
      try {
        const parsedTags = JSON.parse(params.prefillTags) as string[];
        if (Array.isArray(parsedTags) && parsedTags.length > 0) {
          setTags(parsedTags);
        }
      } catch {
        // Ignore parse errors
      }
    }

    // Pre-fill image (upload it)
    let isMounted = true;
    if (params.prefillImageUri && params.prefillImageUri.length > 0) {
      const imageUri = params.prefillImageUri;
      const imageId = `img-prefill-${Date.now()}`;

      // Add image with uploading status
      setImages([{
        id: imageId,
        uri: imageUri,
        status: "uploading",
      }]);

      // Start upload with unmount safety
      uploadMutation.mutateAsync(imageUri)
        .then((response) => {
          if (!isMounted) return;
          setImages((prev) =>
            prev.map((img) =>
              img.id === imageId
                ? { ...img, status: "uploaded" as const, url: response.url }
                : img
            )
          );
        })
        .catch(() => {
          if (!isMounted) return;
          setImages((prev) =>
            prev.map((img) =>
              img.id === imageId ? { ...img, status: "error" as const } : img
            )
          );
        });
    }

    return () => {
      isMounted = false;
    };
  }, [params, prefillApplied, uploadMutation]);

  // AI content generation
  const generateContentMutation = useGenerateRecipeContent();

  const createRecipeMutation = useAuthMutation<
    ApiResponse<RecipeDetailDto>,
    CreateRecipeRequest
  >("/api/recipes");

  const isPublishing = createRecipeMutation.isPending;
  const isGenerating = generateContentMutation.isPending;

  // Check if AI Generate button should be shown
  const canShowAIGenerate = useMemo(() => {
    const hasUploadedImages = images.some(
      (img) => img.status === "uploaded" && img.url,
    );
    const hasTitle = title.trim().length > 0;
    return hasUploadedImages && hasTitle;
  }, [images, title]);

  // Check if there's existing content that would be replaced
  const hasExistingContent = useMemo(() => {
    const hasDescription = description.trim().length > 0;
    const hasIngredients = ingredients.length > 0;
    const hasSteps = steps.some((s) => s.text.trim().length > 0);
    const hasTags = tags.length > 0;
    return hasDescription || hasIngredients || hasSteps || hasTags;
  }, [description, ingredients, steps, tags]);

  // Perform the AI generation
  const performAIGeneration = async () => {
    setShowReplaceDialog(false);

    const imageUrls = images
      .filter((img) => img.status === "uploaded" && img.url)
      .map((img) => img.url!);

    if (imageUrls.length === 0) {
      toast.error("Please upload at least one image first.");
      return;
    }

    if (!title.trim()) {
      toast.error("Please enter a recipe title first.");
      return;
    }

    try {
      const result = await generateContentMutation.mutateAsync({
        imageUrls,
        title: title.trim(),
        description: description.trim() || undefined,
      });

      if (!result.data?.success) {
        // Error message might be in result.data.errorMessage or result.message (for 400 responses)
        const errorMsg =
          result.data?.errorMessage || result.message || "Failed to generate content.";
        toast.error(errorMsg);
        return;
      }

      const {
        description: generatedDescription,
        ingredients: generatedIngredients,
        steps: generatedSteps,
        tags: generatedTags,
      } = result.data;

      // Update description
      if (generatedDescription) {
        setDescription(generatedDescription);
      }

      // Update ingredients
      if (generatedIngredients && generatedIngredients.length > 0) {
        setIngredients(
          generatedIngredients.map((ing, index) => ({
            id: `ing-${Date.now()}-${index}`,
            name: ing.name,
            amount: ing.amount?.toString() ?? "",
            unit: ing.unit,
          })),
        );
      }

      // Update steps
      if (generatedSteps && generatedSteps.length > 0) {
        setSteps(
          generatedSteps.map((text, index) => ({
            id: `step-${Date.now()}-${index}`,
            text,
          })),
        );
      }

      // Update tags
      if (generatedTags && generatedTags.length > 0) {
        setTags(generatedTags);
      }

      toast.success(
        `Generated ${generatedIngredients?.length ?? 0} ingredients, ${generatedSteps?.length ?? 0} steps, and ${generatedTags?.length ?? 0} tags`,
      );
    } catch (error: any) {
      const message =
        typeof error?.message === "string"
          ? error.message
          : "Failed to generate content.";
      toast.error(message);
    }
  };

  // Handle AI Generate button press
  const handleAIGenerate = () => {
    if (hasExistingContent) {
      setShowReplaceDialog(true);
    } else {
      performAIGeneration();
    }
  };

  // Image handling
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

    // Add processed images to state
    const newImages: LocalImage[] = processedAssets.map((asset) => ({
      id: asset.id,
      uri: asset.uri,
      status: "uploading" as const,
    }));

    setImages((prev) => [...prev, ...newImages]);

    // Upload each image
    for (const img of newImages) {
      try {
        const response = await uploadMutation.mutateAsync(img.uri);
        setImages((prev) =>
          prev.map((i) =>
            i.id === img.id
              ? { ...i, status: "uploaded" as const, url: response.url }
              : i,
          ),
        );
      } catch {
        setImages((prev) =>
          prev.map((i) =>
            i.id === img.id ? { ...i, status: "error" as const } : i,
          ),
        );
      }
    }
  };

  const handleRemoveImage = (id: string) => {
    setImages((prev) => prev.filter((img) => img.id !== id));
  };

  const handleClear = () => {
    setShowClearDialog(true);
  };

  const performClear = () => {
    setShowClearDialog(false);
    setTitle("");
    setDescription("");
    setSteps([]);
    setIngredients([]);
    setImages([]);
    setTags([]);
    clearTagInput();
    setVisibility("Public");
  };

  const handleAddStep = () => {
    setSteps((prev) => [
      ...prev,
      {
        id: `step-${Date.now()}`,
        text: "",
      },
    ]);
  };

  const handleUpdateStep = (id: string, text: string) => {
    setSteps((prev) =>
      prev.map((step) => (step.id === id ? { ...step, text } : step)),
    );
  };

  const handleRemoveStep = (id: string) => {
    setSteps((prev) => {
      if (prev.length === 1) return prev;
      return prev.filter((step) => step.id !== id);
    });
  };

  const handleAddIngredient = () => {
    setIngredients((prev) => [
      ...prev,
      { id: `ing-${Date.now()}`, name: "", amount: "", unit: "" },
    ]);
  };

  const handleUpdateIngredient = (
    id: string,
    field: keyof Omit<Ingredient, "id">,
    value: string | number | undefined,
  ) => {
    setIngredients((prev) =>
      prev.map((ing) => (ing.id === id ? { ...ing, [field]: value } : ing)),
    );
  };

  const handleRemoveIngredient = (id: string) => {
    setIngredients((prev) => prev.filter((ing) => ing.id !== id));
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
    if (incoming.length === 0) {
      return current;
    }

    const seen = new Set(current.map(normalizeTag));
    const result = [...current];

    incoming.forEach((raw) => {
      const sanitized = sanitizeTag(raw);
      if (!sanitized) {
        return;
      }

      const normalized = normalizeTag(sanitized);
      if (!seen.has(normalized)) {
        seen.add(normalized);
        result.push(sanitized);
      }
    });

    return result;
  };

  const commitTagInput = () => {
    const rawValue = tagInputRef.current;
    const parsed = parseTagInputValue(rawValue);
    if (rawValue.length) {
      clearTagInput();
    }

    if (!parsed.length) {
      return;
    }

    setTags((prev) => mergeUniqueTags(prev, parsed));
  };

  const removeTag = (rawTag: string) => {
    const normalized = normalizeTag(rawTag);
    setTags((prev) => prev.filter((tag) => normalizeTag(tag) !== normalized));
  };

  const toggleSuggestedTag = (rawTag: string) => {
    const sanitized = sanitizeTag(rawTag);
    if (!sanitized) {
      return;
    }

    const rawInput = tagInputRef.current;
    const pendingFromInput = parseTagInputValue(rawInput);
    const normalized = normalizeTag(sanitized);

    setTags((prev) => {
      const base = pendingFromInput.length
        ? mergeUniqueTags(prev, pendingFromInput)
        : prev;
      const exists = base.some((tag) => normalizeTag(tag) === normalized);
      if (exists) {
        return base.filter((tag) => normalizeTag(tag) !== normalized);
      }
      return [...base, sanitized];
    });

    if (rawInput.length) {
      clearTagInput();
    }
  };

  const isTagActive = (rawTag: string) =>
    tags.some((tag) => normalizeTag(tag) === normalizeTag(rawTag));

  const normalizedSteps = useMemo(
    () => steps.map((step) => step.text.trim()).filter(Boolean),
    [steps],
  );

  const hasUploadingImages = images.some(
    (image) => image.status === "uploading",
  );
  const hasFailedUploads = images.some((image) => image.status === "error");
  const uploadedImageUrls = images
    .filter((image) => image.status === "uploaded" && image.url)
    .map((image) => image.url!) as string[];

  const isPublishDisabled = isPublishing || hasUploadingImages;

  const resetForm = () => {
    setTitle("");
    setDescription("");
    setIngredients([]);
    setSteps([]);
    setImages([]);
    setTags([]);
    clearTagInput();
    setVisibility("Public");
  };

  const handlePublish = async () => {
    if (hasUploadingImages)
      return toast.info("Please wait for images to finish uploading.");
    if (hasFailedUploads) {
      toast.info(
        "Failed images will be skipped. You can remove and re-upload if needed.",
      );
    }

    if (!title.trim()) {
      return toast.error("Please fill in the recipe title.");
    }

    if (normalizedSteps.length === 0) {
      return toast.error("Please add at least one cooking step.");
    }

    const rawInput = tagInputRef.current;
    const pendingInputTags = parseTagInputValue(rawInput);
    const combinedTags = pendingInputTags.length
      ? mergeUniqueTags(tags, pendingInputTags)
      : tags;

    if (pendingInputTags.length) {
      setTags(combinedTags);
    }
    if (rawInput.length) {
      clearTagInput();
    }

    const normalizedTags = Array.from(
      new Set(combinedTags.map((tag) => tag.trim()).filter(Boolean)),
    );

    // Normalize ingredients - filter out empty names
    const normalizedIngredients = ingredients
      .filter((ing) => ing.name.trim().length > 0)
      .map((ing) => {
        const parsedAmount = ing.amount ? parseFloat(ing.amount) : NaN;
        return {
          name: ing.name.trim(),
          amount: !Number.isNaN(parsedAmount) ? parsedAmount : null,
          unit: ing.unit?.trim() || null,
          isOptional: false,
        };
      });

    const payload: CreateRecipeRequest = {
      title: title.trim(),
      description: description.trim(),
      steps: normalizedSteps,
      visibility,
      tags: normalizedTags.length ? normalizedTags : undefined,
      imageUrls: uploadedImageUrls.length ? uploadedImageUrls : undefined,
      ingredients: normalizedIngredients.length ? normalizedIngredients : undefined,
    };

    try {
      const result = await createRecipeMutation.mutateAsync(payload);
      const createdRecipe = result?.data;

      if (createdRecipe) {
        const createdCard = mapDetailToRecipeCard(createdRecipe);

        queryClient.setQueryData<ApiResponse<RecipeCardDto[]> | undefined>(
          myRecipesKey,
          (prev) => {
            const current = prev?.data ?? [];
            const next = [createdCard, ...current];
            if (prev) {
              return { ...prev, data: next };
            }
            return { code: 0, message: "Ok", data: next };
          },
        );

        if (createdRecipe.visibility === "Public") {
          queryClient.setQueryData<ApiResponse<RecipeCardDto[]> | undefined>(
            communityRecipesKey,
            (prev) => {
              if (prev?.data?.some((item) => item.id === createdCard.id)) {
                return prev;
              }
              const current = prev?.data ?? [];
              const next = [createdCard, ...current];
              if (prev) {
                return { ...prev, data: next };
              }
              return { code: 0, message: "Ok", data: next };
            },
          );
        }
      }

      toast.success("Recipe published");
      resetForm();
      router.replace("/");
    } catch (error: any) {
      const message =
        typeof error?.message === "string"
          ? error.message
          : "Failed to publish recipe.";
      toast.error(message);
    }
  };

  const bottomPadding = Math.max(insets.bottom, 16);

  return (
    <View
      className="flex-1"
      style={{ paddingTop: insets.top + 8, backgroundColor: palette.bg }}
    >
      <ScrollView
        className="flex-1"
        showsVerticalScrollIndicator
        keyboardShouldPersistTaps="handled"
      >
        <View className="w-full items-center px-4 pt-5 pb-9">
          <View className="w-full max-w-[760px] overflow-hidden rounded-2xl border" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
            {/* Header */}
            <View className="flex-row items-center justify-between px-5 py-4">
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                Publish Recipe
              </Text>
              <View className="flex-row items-center gap-3">
                <TouchableOpacity
                  accessibilityLabel="Clear form"
                  className="p-1"
                  activeOpacity={0.7}
                  onPress={handleClear}
                >
                  <Eraser size={20} color={colors.textMuted} />
                </TouchableOpacity>
                <TouchableOpacity
                  accessibilityLabel="Close"
                  className="p-1"
                  activeOpacity={0.7}
                  onPress={() => router.replace("/")}
                >
                  <X size={20} color={colors.textPrimary} />
                </TouchableOpacity>
              </View>
            </View>

            <View className="h-px w-full" style={{ backgroundColor: colors.border }} />

            <View className="gap-6 px-5 py-6">
              {/* Title */}
              <Section label="Recipe Title *">
                <Input
                  value={title}
                  onChangeText={setTitle}
                  placeholder="Recipe name"
                  placeholderTextColor={colors.textSecondary}
                  className="rounded-xl"
                  style={{ color: colors.textPrimary, borderColor: colors.border, backgroundColor: colors.bg }}
                />
              </Section>

              {/* Description */}
              <Section label="Description">
                <Input
                  value={description}
                  onChangeText={setDescription}
                  multiline
                  placeholder="Description"
                  placeholderTextColor={colors.textSecondary}
                  className="rounded-xl px-3 py-3"
                  style={{ color: colors.textPrimary, borderColor: colors.border, backgroundColor: colors.bg }}
                />
              </Section>

              {/* Images */}
              <View className="gap-2">
                <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                  Images
                </Text>
                <View className="flex-row flex-wrap gap-3">
                  {/* Uploaded/Selected Images */}
                  {images.map((img) => (
                    <View
                      key={img.id}
                      className="relative h-36 w-32 overflow-hidden rounded-xl border"
                      style={{ borderColor: colors.border }}
                    >
                      <Image
                        source={{ uri: img.url || img.uri }}
                        style={{ width: "100%", height: "100%" }}
                        contentFit="cover"
                      />
                      {/* Loading overlay */}
                      {img.status === "uploading" && (
                        <View className="absolute inset-0 items-center justify-center bg-black/50">
                          <Loader2
                            size={24}
                            color={colors.overlayText}
                            className="animate-spin"
                          />
                        </View>
                      )}
                      {/* Error indicator */}
                      {img.status === "error" && (
                        <View className="absolute bottom-0 left-0 right-0 bg-red-500/90 py-1">
                          <Text className="text-center text-xs text-white">
                            Upload failed
                          </Text>
                        </View>
                      )}
                      {/* Remove button */}
                      <TouchableOpacity
                        onPress={() => handleRemoveImage(img.id)}
                        className="absolute right-1 top-1 h-6 w-6 items-center justify-center rounded-full bg-black/60"
                      >
                        <X size={14} color={colors.overlayText} />
                      </TouchableOpacity>
                    </View>
                  ))}

                  {/* Upload button */}
                  {images.length < MAX_IMAGES && (
                    <TouchableOpacity
                      activeOpacity={0.85}
                      onPress={handlePickImages}
                      className="h-36 w-32 items-center justify-center rounded-xl"
                      style={{ borderWidth: 1, borderStyle: "dashed", borderColor: colors.border, backgroundColor: colors.bg }}
                    >
                      <Upload size={24} color={colors.textSecondary} />
                      <Text className="mt-2 text-sm font-semibold" style={{ color: colors.textSecondary }}>
                        Upload
                      </Text>
                    </TouchableOpacity>
                  )}
                </View>
                <Text className="mt-1 text-xs" style={{ color: colors.textSecondary }}>
                  Upload up to {MAX_IMAGES} images ({images.length}/{MAX_IMAGES}
                  )
                </Text>

                {/* AI Generate Button */}
                {canShowAIGenerate && (
                  <Button
                    variant="outline"
                    className="mt-3 rounded-xl"
                    style={{ borderColor: `${colors.accent}60`, backgroundColor: `${colors.accent}15` }}
                    onPress={handleAIGenerate}
                    disabled={isGenerating}
                  >
                    <View className="flex-row items-center justify-center gap-2">
                      {isGenerating ? (
                        <ActivityIndicator size="small" color={colors.accent} />
                      ) : (
                        <Sparkles size={16} color={colors.accent} />
                      )}
                      <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>
                        {isGenerating
                          ? "Generating..."
                          : "AI Generate Content"}
                      </Text>
                    </View>
                  </Button>
                )}
              </View>

              {/* Ingredients */}
              <Section label="Ingredients">
                {ingredients.length > 0 ? (
                  <View className="gap-2">
                    {ingredients.map((ing) => (
                      <View
                        key={ing.id}
                        className="flex-row items-center gap-2 rounded-xl p-3"
                        style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.bg }}
                      >
                        <View className="flex-1">
                          <Input
                            value={ing.name}
                            onChangeText={(text) =>
                              handleUpdateIngredient(ing.id, "name", text)
                            }
                            placeholder="Ingredient name"
                            placeholderTextColor={colors.textSecondary}
                            className="rounded-lg"
                            style={{ color: colors.textPrimary, borderColor: colors.border, backgroundColor: colors.card }}
                          />
                        </View>
                        <View className="w-16">
                          <Input
                            value={ing.amount}
                            onChangeText={(text) =>
                              handleUpdateIngredient(ing.id, "amount", text)
                            }
                            placeholder="Qty"
                            placeholderTextColor={colors.textSecondary}
                            keyboardType="decimal-pad"
                            className="rounded-lg"
                            style={{ color: colors.textPrimary, borderColor: colors.border, backgroundColor: colors.card }}
                          />
                        </View>
                        <View className="w-24">
                          <TouchableOpacity
                            onPress={() => setUnitDropdownId(ing.id)}
                            className="h-10 flex-row items-center justify-between rounded-lg border px-2"
                            style={{ borderColor: colors.border, backgroundColor: colors.card }}
                          >
                            <Text
                              className="flex-1 text-sm"
                              style={{ color: ing.unit ? colors.textPrimary : colors.textSecondary }}
                              numberOfLines={1}
                            >
                              {ing.unit || "Unit"}
                            </Text>
                            <ChevronDown size={14} color={colors.textMuted} />
                          </TouchableOpacity>
                        </View>
                        <TouchableOpacity
                          onPress={() => handleRemoveIngredient(ing.id)}
                          className="h-10 w-10 items-center justify-center rounded-lg"
                          style={{ backgroundColor: `${colors.error}15` }}
                        >
                          <Trash2 size={18} color={colors.error} />
                        </TouchableOpacity>
                      </View>
                    ))}
                  </View>
                ) : (
                  <Text className="text-sm" style={{ color: colors.textMuted }}>
                    No ingredients added yet. Add manually or use AI Generate.
                  </Text>
                )}
                <TouchableOpacity
                  activeOpacity={0.8}
                  className="mt-3 w-full rounded-xl bg-transparent py-3"
                  style={{ borderWidth: 1, borderColor: colors.border }}
                  onPress={handleAddIngredient}
                >
                  <View className="flex-row items-center justify-center gap-2">
                    <Plus size={16} color={colors.textPrimary} />
                    <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>
                      Add Ingredient
                    </Text>
                  </View>
                </TouchableOpacity>
              </Section>

              {/* Steps */}
              <Section label="Cooking Steps *">
                <View className="gap-3">
                  {steps.map((step, index) => (
                    <StepCard
                      key={step.id}
                      index={index}
                      value={step.text}
                      onChange={(text) => handleUpdateStep(step.id, text)}
                      onRemove={() => handleRemoveStep(step.id)}
                      canRemove={steps.length > 1}
                      accentColor={colors.accent}
                      bgColor={colors.bg}
                      borderColor={colors.border}
                      cardColor={colors.card}
                      textColor={colors.textPrimary}
                      mutedColor={colors.textMuted}
                      errorColor={colors.error}
                    />
                  ))}
                </View>
                <TouchableOpacity
                  activeOpacity={0.8}
                  className="mt-3 w-full rounded-xl bg-transparent py-3"
                  style={{ borderWidth: 1, borderColor: colors.border }}
                  onPress={handleAddStep}
                >
                  <View className="flex-row items-center justify-center gap-2">
                    <Plus size={16} color={colors.textPrimary} />
                    <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>
                      Add Step
                    </Text>
                  </View>
                </TouchableOpacity>
              </Section>

              {/* Tags */}
              <Section label="Tags">
                <Input
                  value={tagInput}
                  onChangeText={handleTagInputChange}
                  placeholder="Tag1, Tag2"
                  placeholderTextColor={colors.textSecondary}
                  onSubmitEditing={() => commitTagInput()}
                  onBlur={() => commitTagInput()}
                  returnKeyType="done"
                  className="rounded-xl"
                  style={{ color: colors.textPrimary, borderColor: colors.border, backgroundColor: colors.bg }}
                />
                <TouchableOpacity
                  activeOpacity={0.8}
                  className="mt-3 w-full rounded-xl bg-transparent py-3"
                  style={{ borderWidth: 1, borderColor: colors.border }}
                  onPress={() => commitTagInput()}
                >
                  <View className="flex-row items-center justify-center gap-2">
                    <Plus size={16} color={colors.textPrimary} />
                    <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>
                      Add Tags
                    </Text>
                  </View>
                </TouchableOpacity>
                <View className="mt-3 flex-row flex-wrap gap-2">
                  {tags.map((tag) => (
                    <TouchableOpacity
                      key={tag.toLowerCase()}
                      onPress={() => removeTag(tag)}
                      activeOpacity={0.85}
                    >
                      <View className="flex-row items-center rounded-full px-3 py-1" style={{ backgroundColor: colors.accent }}>
                        <Text style={{ color: colors.bg }} className="text-xs font-medium">
                          {`#${tag}`}
                        </Text>
                      </View>
                    </TouchableOpacity>
                  ))}
                  {SUGGESTED_TAGS.filter((tag) => !isTagActive(tag)).map(
                    (tag) => (
                      <TouchableOpacity
                        key={`suggested-${tag}`}
                        onPress={() => toggleSuggestedTag(tag)}
                        activeOpacity={0.85}
                      >
                        <View className="flex-row items-center rounded-full px-3 py-1" style={{ borderWidth: 1, borderColor: colors.border }}>
                          <Text style={{ color: colors.textSecondary }} className="text-xs font-medium">
                            {`#${tag}`}
                          </Text>
                        </View>
                      </TouchableOpacity>
                    ),
                  )}
                </View>
              </Section>

              {/* Visibility */}
              <Section label="Visibility">
                <View className="flex-row flex-wrap gap-3">
                  <VisibilityOption
                    label="Public"
                    description="Visible to all"
                    icon={<Globe2 size={18} color={visibility === "Public" ? colors.bg : colors.textSecondary} />}
                    active={visibility === "Public"}
                    onPress={() => setVisibility("Public")}
                    accent={colors.accent}
                    borderColor={colors.border}
                    bgColor={colors.bg}
                    textColor={colors.textPrimary}
                    mutedColor={colors.textSecondary}
                  />
                  <VisibilityOption
                    label="Private"
                    description="Only you"
                    icon={<LockKeyhole size={18} color={visibility === "Private" ? colors.bg : colors.textSecondary} />}
                    active={visibility === "Private"}
                    onPress={() => setVisibility("Private")}
                    accent={colors.accent}
                    borderColor={colors.border}
                    bgColor={colors.bg}
                    textColor={colors.textPrimary}
                    mutedColor={colors.textSecondary}
                  />
                </View>
              </Section>

              {/* Publish button */}
              <Button
                disabled={isPublishDisabled}
                onPress={handlePublish}
                className="mt-2 h-12 w-full rounded-xl"
                style={{ backgroundColor: isPublishDisabled ? "rgba(255,255,255,0.25)" : colors.accent }}
              >
                {isPublishing ? (
                  <ActivityIndicator color={colors.bg} />
                ) : (
                  <Text className="text-base font-semibold" style={{ color: colors.bg }}>
                    Publish
                  </Text>
                )}
              </Button>
            </View>
          </View>
        </View>
      </ScrollView>

      {/* Replace Content Confirmation Dialog */}
      <Modal
        visible={showReplaceDialog}
        transparent
        animationType="fade"
        onRequestClose={() => setShowReplaceDialog(false)}
      >
        <View className="flex-1 items-center justify-center bg-black/60 px-6">
          <View className="w-full max-w-sm rounded-2xl p-5" style={{ backgroundColor: colors.card }}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              Replace Existing Content?
            </Text>
            <Text className="mt-2 text-sm" style={{ color: colors.textSecondary }}>
              AI generation will replace your current description, ingredients,
              steps, and tags. This action cannot be undone.
            </Text>
            <View className="mt-5 flex-row gap-3">
              <Button
                variant="outline"
                className="flex-1 bg-transparent"
                style={{ borderColor: colors.border }}
                onPress={() => setShowReplaceDialog(false)}
              >
                <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Cancel</Text>
              </Button>
              <Button
                className="flex-1"
                style={{ backgroundColor: colors.accent }}
                onPress={performAIGeneration}
              >
                <Text className="text-sm font-semibold" style={{ color: colors.bg }}>
                  Replace
                </Text>
              </Button>
            </View>
          </View>
        </View>
      </Modal>

      {/* Clear Form Confirmation Dialog */}
      <Modal
        visible={showClearDialog}
        transparent
        animationType="fade"
        onRequestClose={() => setShowClearDialog(false)}
      >
        <View className="flex-1 items-center justify-center bg-black/60 px-6">
          <View className="w-full max-w-sm rounded-2xl p-5" style={{ backgroundColor: colors.card }}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
              Clear Form?
            </Text>
            <Text className="mt-2 text-sm" style={{ color: colors.textSecondary }}>
              This will clear all your recipe content including title, description,
              images, ingredients, steps, and tags.
            </Text>
            <View className="mt-5 flex-row gap-3">
              <Button
                variant="outline"
                className="flex-1 bg-transparent"
                style={{ borderColor: colors.border }}
                onPress={() => setShowClearDialog(false)}
              >
                <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Cancel</Text>
              </Button>
              <Button
                className="flex-1"
                style={{ backgroundColor: colors.error }}
                onPress={performClear}
              >
                <Text className="text-sm font-semibold" style={{ color: colors.bg }}>
                  Clear
                </Text>
              </Button>
            </View>
          </View>
        </View>
      </Modal>

      {/* Unit Selection Modal */}
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
}

function Section({ label, children }: { label: string; children: ReactNode }) {
  const { colors } = useTheme();
  return (
    <View className="gap-2">
      <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>{label}</Text>
      {children}
    </View>
  );
}

function StepCard({
  index,
  value,
  onChange,
  onRemove,
  canRemove,
  accentColor,
  bgColor,
  borderColor,
  cardColor,
  textColor,
  mutedColor,
  errorColor,
}: {
  index: number;
  value: string;
  onChange: (text: string) => void;
  onRemove: () => void;
  canRemove: boolean;
  accentColor: string;
  bgColor: string;
  borderColor: string;
  cardColor: string;
  textColor: string;
  mutedColor: string;
  errorColor: string;
}) {
  return (
    <View className="flex-row items-start rounded-2xl p-3" style={{ borderWidth: 1, borderColor, backgroundColor: bgColor }}>
      <View className="h-8 w-8 items-center justify-center rounded-full" style={{ backgroundColor: accentColor }}>
        <Text className="font-bold" style={{ color: bgColor }}>{index + 1}</Text>
      </View>
      <View className="ml-3 flex-1">
        <Input
          value={value}
          onChangeText={onChange}
          multiline
          placeholder={`Step ${index + 1}...`}
          placeholderTextColor={mutedColor}
          className="min-h-[64px] rounded-lg px-3 py-2"
          style={{ color: textColor, borderColor, backgroundColor: cardColor }}
        />
      </View>
      {canRemove && (
        <TouchableOpacity
          accessibilityRole="button"
          accessibilityLabel={`Delete step ${index + 1}`}
          onPress={onRemove}
          className="ml-2 h-10 w-10 items-center justify-center rounded-lg"
          style={{ backgroundColor: `${errorColor}15` }}
        >
          <Trash2 size={18} color={errorColor} />
        </TouchableOpacity>
      )}
    </View>
  );
}

function VisibilityOption({
  label,
  description,
  icon,
  active,
  onPress,
  accent,
  borderColor,
  bgColor,
  textColor,
  mutedColor,
}: {
  label: string;
  description: string;
  icon: ReactNode;
  active: boolean;
  onPress: () => void;
  accent: string;
  borderColor: string;
  bgColor: string;
  textColor: string;
  mutedColor: string;
}) {
  return (
    <TouchableOpacity
      activeOpacity={0.9}
      onPress={onPress}
      className="flex-1 rounded-xl px-4 py-4"
      style={{
        borderWidth: 1,
        borderColor: active ? "transparent" : borderColor,
        backgroundColor: active ? accent : bgColor,
      }}
    >
      <View className="mb-2 flex-row items-center gap-2">
        <View
          className="rounded-full p-2"
          style={{ backgroundColor: active ? "rgba(255,255,255,0.2)" : "transparent" }}
        >
          {icon}
        </View>
        <Text
          className="text-sm font-semibold"
          style={{ color: active ? bgColor : textColor }}
        >
          {label}
        </Text>
      </View>
      <Text
        className="text-xs"
        style={{ color: active ? bgColor : mutedColor }}
      >
        {description}
      </Text>
    </TouchableOpacity>
  );
}

function mapDetailToRecipeCard(detail: RecipeDetailDto): RecipeCardDto {
  const coverImageUrl = detail.imageUrls?.find(Boolean) ?? null;
  const authorNickname = detail.author?.nickname ?? "Unknown";
  return {
    id: detail.id,
    authorId: detail.authorId ?? null,
    authorAvatarUrl: detail.author?.avatarUrl ?? null,
    authorNickname,
    title: detail.title,
    description: detail.description,
    coverImageUrl,
    visibility: detail.visibility,
    type: detail.type,
    likesCount: detail.likesCount,
    likedByMe: detail.likedByMe,
    commentsCount: detail.commentsCount,
    savedCount: detail.savedCount,
    savedByMe: detail.savedByMe,
    createdAt: detail.createdAt,
    updatedAt: detail.updatedAt,
    tags: detail.tags ?? [],
  };
}
