import { useState, useCallback, useEffect, useRef } from "react";
import AsyncStorage from "@react-native-async-storage/async-storage";
import { RecognizedRecipe } from "./useVisionRecognition";

const DRAFT_STORAGE_KEY = "@recipe_draft";
const DRAFT_EXPIRY_MS = 24 * 60 * 60 * 1000; // 24 hours

/**
 * Represents an editable draft ingredient with a unique ID.
 */
export interface DraftIngredient {
  id: string;
  name: string;
  quantity: number;
  unit: string;
  category?: string;
}

/**
 * Nutrition info that can be calculated from ingredients.
 */
export interface DraftNutrition {
  calories?: number | null;
  carbohydrates?: number | null;
  fat?: number | null;
  protein?: number | null;
  sugar?: number | null;
  sodium?: number | null;
  saturatedFat?: number | null;
}

/**
 * Full recipe draft state for scanner results.
 */
export interface RecipeDraft {
  id: string;
  title: string;
  description: string;
  imageUri?: string;
  tags: string[];
  cookTimeMinutes?: number;
  prepTimeMinutes?: number;
  difficulty?: "None" | "Easy" | "Medium" | "Hard";
  servings?: number;
  confidence?: number; // AI confidence score (0-1)
  ingredients: DraftIngredient[];
  steps: string[];
  nutrition: DraftNutrition;
  createdAt: number;
  updatedAt: number;
}

interface DraftStorageData {
  draft: RecipeDraft;
  expiresAt: number;
}

/**
 * Generates a unique ID for ingredients/steps.
 */
function generateId(): string {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * Converts RecognizedRecipe from scanner to editable RecipeDraft.
 */
export function createDraftFromRecognizedRecipe(
  recipe: RecognizedRecipe,
  imageUri?: string,
): RecipeDraft {
  const now = Date.now();
  return {
    id: generateId(),
    title: recipe.title || "Untitled Recipe",
    description: recipe.description || "",
    imageUri,
    tags: [],
    cookTimeMinutes: recipe.cookTimeMinutes,
    prepTimeMinutes: recipe.prepTimeMinutes,
    difficulty: calculateDifficulty(recipe),
    servings: recipe.servings,
    confidence: recipe.confidence,
    ingredients: (recipe.ingredients || []).map((ing) => ({
      id: generateId(),
      name: ing.name,
      quantity: ing.quantity,
      unit: ing.unit,
      category: ing.category,
    })),
    steps: recipe.steps || [],
    nutrition: {},
    createdAt: now,
    updatedAt: now,
  };
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

interface UseRecipeDraftOptions {
  /** Auto-save delay in ms (default 500ms) */
  autoSaveDelay?: number;
}

/**
 * Hook for managing recipe draft with AsyncStorage persistence.
 */
export function useRecipeDraft(options: UseRecipeDraftOptions = {}) {
  const { autoSaveDelay = 500 } = options;
  const [draft, setDraftState] = useState<RecipeDraft | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const saveTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Load draft from storage on mount
  useEffect(() => {
    loadDraft();
  }, []);

  const loadDraft = useCallback(async () => {
    setIsLoading(true);
    try {
      const stored = await AsyncStorage.getItem(DRAFT_STORAGE_KEY);
      if (stored) {
        const data: DraftStorageData = JSON.parse(stored);
        if (data.expiresAt > Date.now()) {
          setDraftState(data.draft);
        } else {
          // Expired, clear it
          await AsyncStorage.removeItem(DRAFT_STORAGE_KEY);
        }
      }
    } catch (error) {
      console.error("[useRecipeDraft] Failed to load draft:", error);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const saveDraft = useCallback(async (draftToSave: RecipeDraft) => {
    setIsSaving(true);
    try {
      const data: DraftStorageData = {
        draft: { ...draftToSave, updatedAt: Date.now() },
        expiresAt: Date.now() + DRAFT_EXPIRY_MS,
      };
      await AsyncStorage.setItem(DRAFT_STORAGE_KEY, JSON.stringify(data));
    } catch (error) {
      console.error("[useRecipeDraft] Failed to save draft:", error);
    } finally {
      setIsSaving(false);
    }
  }, []);

  const debouncedSave = useCallback(
    (draftToSave: RecipeDraft) => {
      if (saveTimeoutRef.current) {
        clearTimeout(saveTimeoutRef.current);
      }
      saveTimeoutRef.current = setTimeout(() => {
        saveDraft(draftToSave);
      }, autoSaveDelay);
    },
    [autoSaveDelay, saveDraft],
  );

  // Wrapper to update draft and auto-save
  const setDraft = useCallback(
    (
      updater: RecipeDraft | ((prev: RecipeDraft | null) => RecipeDraft | null),
    ) => {
      setDraftState((prev) => {
        const newDraft =
          typeof updater === "function" ? updater(prev) : updater;
        if (newDraft) {
          debouncedSave(newDraft);
        }
        return newDraft;
      });
    },
    [debouncedSave],
  );

  /**
   * Initialize a new draft from recognized recipe.
   */
  const initDraft = useCallback(
    (recipe: RecognizedRecipe, imageUri?: string) => {
      const newDraft = createDraftFromRecognizedRecipe(recipe, imageUri);
      setDraftState(newDraft);
      saveDraft(newDraft);
      return newDraft;
    },
    [saveDraft],
  );

  /**
   * Clear the current draft.
   */
  const clearDraft = useCallback(async () => {
    if (saveTimeoutRef.current) {
      clearTimeout(saveTimeoutRef.current);
    }
    setDraftState(null);
    try {
      await AsyncStorage.removeItem(DRAFT_STORAGE_KEY);
    } catch (error) {
      console.error("[useRecipeDraft] Failed to clear draft:", error);
    }
  }, []);

  // ============ Field Update Helpers ============

  const updateTitle = useCallback(
    (title: string) => {
      setDraft((prev) => (prev ? { ...prev, title } : null));
    },
    [setDraft],
  );

  const updateDescription = useCallback(
    (description: string) => {
      setDraft((prev) => (prev ? { ...prev, description } : null));
    },
    [setDraft],
  );

  const updateServings = useCallback(
    (servings: number | undefined) => {
      setDraft((prev) => (prev ? { ...prev, servings } : null));
    },
    [setDraft],
  );

  const updateCookTime = useCallback(
    (cookTimeMinutes: number | undefined) => {
      setDraft((prev) => (prev ? { ...prev, cookTimeMinutes } : null));
    },
    [setDraft],
  );

  const updatePrepTime = useCallback(
    (prepTimeMinutes: number | undefined) => {
      setDraft((prev) => (prev ? { ...prev, prepTimeMinutes } : null));
    },
    [setDraft],
  );

  const updateDifficulty = useCallback(
    (difficulty: "None" | "Easy" | "Medium" | "Hard") => {
      setDraft((prev) => (prev ? { ...prev, difficulty } : null));
    },
    [setDraft],
  );

  // ============ Tags Helpers ============

  const addTag = useCallback(
    (tag: string) => {
      const trimmed = tag.trim();
      if (!trimmed) return;
      setDraft((prev) => {
        if (!prev) return null;
        if (prev.tags.includes(trimmed)) return prev;
        return { ...prev, tags: [...prev.tags, trimmed] };
      });
    },
    [setDraft],
  );

  const removeTag = useCallback(
    (tag: string) => {
      setDraft((prev) => {
        if (!prev) return null;
        return { ...prev, tags: prev.tags.filter((t) => t !== tag) };
      });
    },
    [setDraft],
  );

  // ============ Ingredients Helpers ============

  const addIngredient = useCallback(
    (ingredient: Omit<DraftIngredient, "id">) => {
      setDraft((prev) => {
        if (!prev) return null;
        const newIngredient: DraftIngredient = {
          ...ingredient,
          id: generateId(),
        };
        return { ...prev, ingredients: [...prev.ingredients, newIngredient] };
      });
    },
    [setDraft],
  );

  const updateIngredient = useCallback(
    (id: string, updates: Partial<Omit<DraftIngredient, "id">>) => {
      setDraft((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          ingredients: prev.ingredients.map((ing) =>
            ing.id === id ? { ...ing, ...updates } : ing,
          ),
        };
      });
    },
    [setDraft],
  );

  const removeIngredient = useCallback(
    (id: string) => {
      setDraft((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          ingredients: prev.ingredients.filter((ing) => ing.id !== id),
        };
      });
    },
    [setDraft],
  );

  // ============ Steps Helpers ============

  const addStep = useCallback(
    (step: string) => {
      const trimmed = step.trim();
      if (!trimmed) return;
      setDraft((prev) => {
        if (!prev) return null;
        return { ...prev, steps: [...prev.steps, trimmed] };
      });
    },
    [setDraft],
  );

  const updateStep = useCallback(
    (index: number, step: string) => {
      setDraft((prev) => {
        if (!prev) return null;
        const newSteps = [...prev.steps];
        newSteps[index] = step;
        return { ...prev, steps: newSteps };
      });
    },
    [setDraft],
  );

  const removeStep = useCallback(
    (index: number) => {
      setDraft((prev) => {
        if (!prev) return null;
        return { ...prev, steps: prev.steps.filter((_, i) => i !== index) };
      });
    },
    [setDraft],
  );

  const reorderSteps = useCallback(
    (fromIndex: number, toIndex: number) => {
      setDraft((prev) => {
        if (!prev) return null;
        const newSteps = [...prev.steps];
        const [removed] = newSteps.splice(fromIndex, 1);
        newSteps.splice(toIndex, 0, removed);
        return { ...prev, steps: newSteps };
      });
    },
    [setDraft],
  );

  // ============ Nutrition Helpers ============

  const updateNutrition = useCallback(
    (nutrition: Partial<DraftNutrition>) => {
      setDraft((prev) => {
        if (!prev) return null;
        return { ...prev, nutrition: { ...prev.nutrition, ...nutrition } };
      });
    },
    [setDraft],
  );

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (saveTimeoutRef.current) {
        clearTimeout(saveTimeoutRef.current);
      }
    };
  }, []);

  return {
    // State
    draft,
    isLoading,
    isSaving,

    // Core operations
    initDraft,
    setDraft,
    loadDraft,
    saveDraft,
    clearDraft,

    // Field updates
    updateTitle,
    updateDescription,
    updateServings,
    updateCookTime,
    updatePrepTime,
    updateDifficulty,

    // Tags
    addTag,
    removeTag,

    // Ingredients
    addIngredient,
    updateIngredient,
    removeIngredient,

    // Steps
    addStep,
    updateStep,
    removeStep,
    reorderSteps,

    // Nutrition
    updateNutrition,
  };
}

export default useRecipeDraft;
