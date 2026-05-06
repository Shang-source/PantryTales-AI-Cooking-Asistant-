import { useState, useEffect, useCallback } from "react";
import AsyncStorage from "@react-native-async-storage/async-storage";

const SMART_RECIPES_PROMPT_DISMISSED_KEY = "@smart_recipes_prompt_dismissed";

/**
 * Hook to manage whether the user has dismissed the Smart Recipes servings prompt.
 * Persists the dismissed state to AsyncStorage so it survives app restarts.
 */
export function useSmartRecipesPrompt() {
  const [hasUserDismissedPrompt, setHasUserDismissedPrompt] = useState<boolean | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Load dismissed state from storage on mount
  useEffect(() => {
    loadDismissedState();
  }, []);

  const loadDismissedState = async () => {
    setIsLoading(true);
    try {
      const stored = await AsyncStorage.getItem(SMART_RECIPES_PROMPT_DISMISSED_KEY);
      setHasUserDismissedPrompt(stored === "true");
    } catch (error) {
      console.error("[useSmartRecipesPrompt] Failed to load dismissed state:", error);
      setHasUserDismissedPrompt(false);
    } finally {
      setIsLoading(false);
    }
  };

  /**
   * Mark the prompt as dismissed (user cancelled the dialog).
   * This will prevent the dialog from showing again.
   */
  const dismissPrompt = useCallback(async () => {
    setHasUserDismissedPrompt(true);
    try {
      await AsyncStorage.setItem(SMART_RECIPES_PROMPT_DISMISSED_KEY, "true");
    } catch (error) {
      console.error("[useSmartRecipesPrompt] Failed to save dismissed state:", error);
    }
  }, []);

  /**
   * Reset the dismissed state (e.g., when user explicitly regenerates recipes).
   * This allows the dialog to show again on next visit if no recipes exist.
   */
  const resetPrompt = useCallback(async () => {
    setHasUserDismissedPrompt(false);
    try {
      await AsyncStorage.removeItem(SMART_RECIPES_PROMPT_DISMISSED_KEY);
    } catch (error) {
      console.error("[useSmartRecipesPrompt] Failed to reset dismissed state:", error);
    }
  }, []);

  return {
    hasUserDismissedPrompt,
    isLoading,
    dismissPrompt,
    resetPrompt,
  };
}

export default useSmartRecipesPrompt;
