import { Platform } from "react-native";
import { useAuth } from "@clerk/clerk-expo";
import { useMutation, UseMutationOptions } from "@tanstack/react-query";
import {
  API_FALLBACK_BASE_ANDROID,
  API_FALLBACK_BASE_IOS,
  DEFAULT_TOKEN_TEMPLATE,
} from "@/constants/constants";

// Resolve base URL
const envBase = process.env.EXPO_PUBLIC_API_BASE_URL?.replace(/\/$/, "");
const fallbackBase =
  Platform.OS === "android" ? API_FALLBACK_BASE_ANDROID : API_FALLBACK_BASE_IOS;
const BASE_URL = (envBase || fallbackBase).replace(/\/$/, "");

const DEV_TOKEN = "dev-token";
const isDev = process.env.NODE_ENV !== "production";
const useRealAuth = process.env.EXPO_PUBLIC_USE_REAL_AUTH === "true";

// Types for recognized ingredients
export interface RecognizedIngredient {
  name: string;
  quantity: number;
  unit: string;
  confidence: number;
  suggestedStorageMethod?: string;
  suggestedExpirationDays?: number;
  originalReceiptText?: string;
}

export interface FilteredItem {
  text: string;
  reason: string;
}

export interface IngredientRecognitionResponse {
  success: boolean;
  imageType: "receipt" | "ingredients" | "unknown";
  ingredients: RecognizedIngredient[];
  storeName?: string;
  filteredItems?: FilteredItem[];
  notes?: string;
  errorMessage?: string;
}

export interface ApiResponse<T> {
  code: number;
  message: string;
  data?: T;
}

// Hook for ingredient recognition from image
export function useIngredientRecognition(
  options?: UseMutationOptions<
    ApiResponse<IngredientRecognitionResponse>,
    Error,
    string
  >,
) {
  const { getToken } = useAuth();

  return useMutation<ApiResponse<IngredientRecognitionResponse>, Error, string>(
    {
      mutationFn: async (imageUri: string) => {
        console.log("[useIngredientRecognition] Starting recognition", {
          platform: Platform.OS,
          imageUri: imageUri.substring(0, 100) + "...",
        });

        const formData = new FormData();

        // Handle different platforms
        if (Platform.OS === "web") {
          const response = await fetch(imageUri);
          const blob = await response.blob();
          formData.append("image", blob, "image.jpg");
        } else {
          const filename = imageUri.split("/").pop() || "image.jpg";
          const match = /\.([\\w]+)$/.exec(filename);
          const extension = match ? match[1].toLowerCase() : "jpg";

          let type: string;
          if (extension === "heic" || extension === "heif") {
            type = "image/heic";
          } else if (extension === "png") {
            type = "image/png";
          } else {
            type = "image/jpeg";
          }

          const normalizedUri =
            imageUri.startsWith("file://") ||
            imageUri.startsWith("content://") ||
            imageUri.startsWith("ph://")
              ? imageUri
              : `file://${imageUri}`;

          const fileObject = {
            uri: normalizedUri,
            name: filename,
            type,
          };
          formData.append("image", fileObject as unknown as Blob);
        }

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

        if (isDev && !useRealAuth && !token) {
          token = DEV_TOKEN;
        }

        const headers: Record<string, string> = {
          Accept: "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        };

        const url = `${BASE_URL}/api/vision/recognize-ingredients`;
        console.log("[useIngredientRecognition] Sending request to:", url);

        const res = await fetch(url, {
          method: "POST",
          headers,
          body: formData,
        });

        console.log(
          "[useIngredientRecognition] Response status:",
          res.status,
          res.statusText,
        );

        const payload = await res.json();

        if (!res.ok) {
          // Treat 400s as a valid "recognition failed" response so callers can
          // show a friendly message without triggering LogBox via console.error.
          if (res.status === 400) {
            return payload as ApiResponse<IngredientRecognitionResponse>;
          }

          throw new Error(payload.message || `HTTP ${res.status}`);
        }

        return payload as ApiResponse<IngredientRecognitionResponse>;
      },
      ...options,
    },
  );
}

// Types for recipe recognition (for future use)
export interface RecipeIngredient {
  name: string;
  quantity: number;
  unit: string;
  category?: string;
}

export interface RecipeNutrition {
  calories?: number;
  carbohydrates?: number;
  fat?: number;
  protein?: number;
  sugar?: number;
  sodium?: number;
  saturatedFat?: number;
}

export interface RecognizedRecipe {
  title: string;
  description: string;
  ingredients: RecipeIngredient[];
  steps: string[];
  prepTimeMinutes?: number;
  cookTimeMinutes?: number;
  servings?: number;
  confidence: number;
  nutrition?: RecipeNutrition;
}

export interface RecipeRecognitionResponse {
  success: boolean;
  recipe?: RecognizedRecipe;
  errorMessage?: string;
}

export function useRecipeRecognition(
  options?: UseMutationOptions<
    ApiResponse<RecipeRecognitionResponse>,
    Error,
    string
  >,
) {
  const { getToken } = useAuth();

  return useMutation<ApiResponse<RecipeRecognitionResponse>, Error, string>({
    mutationFn: async (imageUri: string) => {
      console.log("[useRecipeRecognition] Starting recognition");

      const formData = new FormData();

      if (Platform.OS === "web") {
        const response = await fetch(imageUri);
        const blob = await response.blob();
        formData.append("image", blob, "image.jpg");
      } else {
        const filename = imageUri.split("/").pop() || "image.jpg";
        const match = /\.([\\w]+)$/.exec(filename);
        const extension = match ? match[1].toLowerCase() : "jpg";

        const type =
          extension === "png"
            ? "image/png"
            : extension === "heic" || extension === "heif"
              ? "image/heic"
              : "image/jpeg";

        const normalizedUri =
          imageUri.startsWith("file://") ||
          imageUri.startsWith("content://") ||
          imageUri.startsWith("ph://")
            ? imageUri
            : `file://${imageUri}`;

        formData.append("image", {
          uri: normalizedUri,
          name: filename,
          type,
        } as unknown as Blob);
      }

      const tokenOptions = DEFAULT_TOKEN_TEMPLATE
        ? { template: DEFAULT_TOKEN_TEMPLATE }
        : undefined;

      let token: string | null = null;
      try {
        token = (await getToken(tokenOptions)) ?? (await getToken());
      } catch {
        token = null;
      }

      if (isDev && !useRealAuth && !token) {
        token = DEV_TOKEN;
      }

      const headers: Record<string, string> = {
        Accept: "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      };

      const url = `${BASE_URL}/api/vision/recognize-recipe`;
      const res = await fetch(url, {
        method: "POST",
        headers,
        body: formData,
      });

      const payload = await res.json();

      if (!res.ok) {
        // Treat 400s as a valid "recognition failed" response so callers can
        // show a friendly message without triggering LogBox via console.error.
        if (res.status === 400) {
          return payload as ApiResponse<RecipeRecognitionResponse>;
        }

        throw new Error(payload.message || `HTTP ${res.status}`);
      }

      return payload as ApiResponse<RecipeRecognitionResponse>;
    },
    ...options,
  });
}

// Types for AI-generated recipe content
export interface GeneratedIngredient {
  name: string;
  amount?: number;
  unit?: string;
  category?: string;
}

export interface GenerateRecipeContentRequest {
  imageUrls: string[];
  title: string;
  description?: string;
}

export interface GenerateRecipeContentResponse {
  success: boolean;
  description?: string;
  steps?: string[];
  tags?: string[];
  ingredients?: GeneratedIngredient[];
  confidence?: number;
  errorMessage?: string;
}

export function useGenerateRecipeContent(
  options?: UseMutationOptions<
    ApiResponse<GenerateRecipeContentResponse>,
    Error,
    GenerateRecipeContentRequest
  >,
) {
  const { getToken } = useAuth();

  return useMutation<
    ApiResponse<GenerateRecipeContentResponse>,
    Error,
    GenerateRecipeContentRequest
  >({
    mutationFn: async (request: GenerateRecipeContentRequest) => {
      console.log("[useGenerateRecipeContent] Starting content generation", {
        title: request.title,
        imageCount: request.imageUrls.length,
      });

      const tokenOptions = DEFAULT_TOKEN_TEMPLATE
        ? { template: DEFAULT_TOKEN_TEMPLATE }
        : undefined;

      let token: string | null = null;
      try {
        token = (await getToken(tokenOptions)) ?? (await getToken());
      } catch {
        token = null;
      }

      if (isDev && !useRealAuth && !token) {
        token = DEV_TOKEN;
      }

      const headers: Record<string, string> = {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      };

      const url = `${BASE_URL}/api/vision/generate-recipe-content`;
      console.log("[useGenerateRecipeContent] Sending request to:", url);

      const res = await fetch(url, {
        method: "POST",
        headers,
        body: JSON.stringify({
          imageUrls: request.imageUrls,
          title: request.title,
          description: request.description,
        }),
      });

      console.log(
        "[useGenerateRecipeContent] Response status:",
        res.status,
        res.statusText,
      );

      // Handle empty responses safely
      const text = await res.text();
      if (!text) {
        throw new Error(`Empty response from server (HTTP ${res.status})`);
      }

      let payload: ApiResponse<GenerateRecipeContentResponse>;
      try {
        payload = JSON.parse(text);
      } catch (parseError) {
        console.error("[useGenerateRecipeContent] JSON parse error:", text.substring(0, 200));
        throw new Error("Invalid response from server");
      }

      if (!res.ok) {
        if (res.status === 400) {
          return payload;
        }
        throw new Error(payload.message || `HTTP ${res.status}`);
      }

      return payload;
    },
    ...options,
  });
}
