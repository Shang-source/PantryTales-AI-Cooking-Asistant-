import { useState, useCallback, useRef, useEffect } from "react";
import { Platform } from "react-native";
import { useAuth } from "@clerk/clerk-expo";
import EventSource from "react-native-sse";
import {
  API_FALLBACK_BASE_ANDROID,
  API_FALLBACK_BASE_IOS,
  DEFAULT_TOKEN_TEMPLATE,
} from "@/constants/constants";
import { SmartRecipeDto } from "./useSmartRecipes";

// Resolve base URL (same as useApi.tsx)
const envBase = process.env.EXPO_PUBLIC_API_BASE_URL?.replace(/\/$/, "");
const fallbackBase =
  Platform.OS === "android" ? API_FALLBACK_BASE_ANDROID : API_FALLBACK_BASE_IOS;
const BASE_URL = (envBase || fallbackBase).replace(/\/$/, "");

// Development token handling (same as useApi.tsx)
const DEV_TOKEN = "dev-token";
const isDev = process.env.NODE_ENV !== "production";
const useRealAuth = process.env.EXPO_PUBLIC_USE_REAL_AUTH === "true";

// Backend sends enum as numbers: Start=0, Recipe=1, Complete=2, Error=3
export type SseEventType =
  | 0
  | 1
  | 2
  | 3
  | "start"
  | "recipe"
  | "complete"
  | "error";

// Enum values matching backend SmartRecipeSseEventType
const SSE_EVENT_TYPE = {
  START: 0,
  RECIPE: 1,
  COMPLETE: 2,
  ERROR: 3,
} as const;

export interface SmartRecipeSseEvent {
  type: SseEventType;
  recipe?: SmartRecipeDto;
  totalExpected?: number;
  currentIndex?: number;
  errorMessage?: string;
}

export interface StreamProgress {
  current: number;
  total: number;
}

export interface UseSmartRecipesStreamResult {
  /** Recipes received so far */
  recipes: SmartRecipeDto[];
  /** Whether streaming is currently in progress */
  isStreaming: boolean;
  /** Progress of recipe generation (current/total) */
  progress: StreamProgress | null;
  /** Error message if streaming failed */
  error: string | null;
  /** Start streaming recipe generation */
  startStreaming: (servings?: number) => Promise<void>;
  /** Cancel the current streaming request */
  cancelStreaming: () => void;
  /** Reset the stream state */
  resetStream: () => void;
}

/**
 * Hook for streaming smart recipe generation via SSE.
 * Recipes are received one-by-one as they are generated.
 * Uses react-native-sse for proper SSE support in React Native.
 */
export function useSmartRecipesStream(): UseSmartRecipesStreamResult {
  const { getToken } = useAuth();
  const [recipes, setRecipes] = useState<SmartRecipeDto[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [progress, setProgress] = useState<StreamProgress | null>(null);
  const [error, setError] = useState<string | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);
  const isCancelledRef = useRef(false);

  // Cleanup on unmount to prevent memory leaks
  useEffect(() => {
    return () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
        eventSourceRef.current = null;
      }
    };
  }, []);

  const resetStream = useCallback(() => {
    setRecipes([]);
    setIsStreaming(false);
    setProgress(null);
    setError(null);
  }, []);

  const cancelStreaming = useCallback(() => {
    isCancelledRef.current = true;
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
    setIsStreaming(false);
  }, []);

  const startStreaming = useCallback(
    async (servings?: number) => {
      // Reset state and cancellation flag
      isCancelledRef.current = false;
      setRecipes([]);
      setIsStreaming(true);
      // Initialize progress immediately to avoid flash of "Loading..." screen
      setProgress({ current: 0, total: 7 });
      setError(null);

      // Get auth token
      const tokenOptions = DEFAULT_TOKEN_TEMPLATE
        ? { template: DEFAULT_TOKEN_TEMPLATE }
        : undefined;

      let token: string | null = null;
      try {
        token = (await getToken(tokenOptions)) ?? (await getToken());
      } catch {
        token = null;
      }

      // Use dev token in development mode
      if (isDev && !useRealAuth && !token) {
        token = DEV_TOKEN;
      }

      const url = `${BASE_URL}/api/smart-recipes/stream${servings ? `?servings=${servings}` : ""}`;

      console.log("[useSmartRecipesStream] Starting SSE stream:", {
        url,
        hasToken: !!token,
        servings,
      });

      // Create EventSource with headers
      const es = new EventSource(url, {
        headers: {
          Authorization: token ? `Bearer ${token}` : "",
          Accept: "text/event-stream",
        },
      });

      eventSourceRef.current = es;

      // Handle incoming messages
      es.addEventListener("message", (event) => {
        // Skip processing if streaming was cancelled
        if (isCancelledRef.current || !event.data) return;

        try {
          const sseEvent = JSON.parse(event.data) as SmartRecipeSseEvent;
          console.log("[useSmartRecipesStream] Received event:", sseEvent.type);

          // Handle both numeric (from C# enum) and string event types
          const eventType = sseEvent.type;

          if (eventType === SSE_EVENT_TYPE.START || eventType === "start") {
            setProgress({ current: 0, total: sseEvent.totalExpected ?? 7 });
          } else if (
            eventType === SSE_EVENT_TYPE.RECIPE ||
            eventType === "recipe"
          ) {
            console.log("[useSmartRecipesStream] Recipe event received:", {
              hasRecipe: !!sseEvent.recipe,
              recipeTitle: sseEvent.recipe?.title,
              currentIndex: sseEvent.currentIndex,
            });
            if (sseEvent.recipe) {
              setRecipes((prev) => {
                const newRecipes = [...prev, sseEvent.recipe!];
                console.log(
                  "[useSmartRecipesStream] Recipes count:",
                  newRecipes.length,
                );
                return newRecipes;
              });
              setProgress({
                current: sseEvent.currentIndex ?? 0,
                total: sseEvent.totalExpected ?? 7,
              });
            } else {
              console.warn(
                "[useSmartRecipesStream] Recipe event missing recipe data",
              );
            }
          } else if (
            eventType === SSE_EVENT_TYPE.COMPLETE ||
            eventType === "complete"
          ) {
            console.log("[useSmartRecipesStream] Generation complete");
            setProgress({
              current: sseEvent.currentIndex ?? 0,
              total: sseEvent.totalExpected ?? 7,
            });
            setIsStreaming(false);
            es.close();
            eventSourceRef.current = null;
          } else if (
            eventType === SSE_EVENT_TYPE.ERROR ||
            eventType === "error"
          ) {
            console.error(
              "[useSmartRecipesStream] Error:",
              sseEvent.errorMessage,
            );
            setError(sseEvent.errorMessage ?? "Unknown error occurred");
            setIsStreaming(false);
            es.close();
            eventSourceRef.current = null;
          }
        } catch (parseError) {
          console.error(
            "[useSmartRecipesStream] Failed to parse event:",
            event.data,
            parseError,
          );
        }
      });

      // Handle errors
      es.addEventListener("error", (event) => {
        // Skip processing if streaming was cancelled
        if (isCancelledRef.current) return;

        console.error("[useSmartRecipesStream] EventSource error:", event);
        setError("Connection error occurred");
        setIsStreaming(false);
        es.close();
        eventSourceRef.current = null;
      });

      // Handle connection open
      es.addEventListener("open", () => {
        console.log("[useSmartRecipesStream] Connection opened");
      });
    },
    [getToken],
  );

  return {
    recipes,
    isStreaming,
    progress,
    error,
    startStreaming,
    cancelStreaming,
    resetStream,
  };
}
