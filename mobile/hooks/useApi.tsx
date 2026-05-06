import { Platform } from "react-native";
import { useAuth } from "@clerk/clerk-expo";
import {
  useQuery,
  useMutation,
  UseQueryOptions,
  UseMutationOptions,
  useInfiniteQuery,
  UseInfiniteQueryOptions,
  UseInfiniteQueryResult,
  InfiniteData,
  QueryKey
} from "@tanstack/react-query";
import {
  API_FALLBACK_BASE_ANDROID,
  API_FALLBACK_BASE_IOS,
  DEFAULT_TOKEN_TEMPLATE,
} from "@/constants/constants";
import { ApiResponse, HasPagination } from "@/types/api";

// Resolve base URL with sensible fallbacks for simulators/emulators.
const envBase = process.env.EXPO_PUBLIC_API_BASE_URL?.replace(/\/$/, "");
const fallbackBase =
  Platform.OS === "android" ? API_FALLBACK_BASE_ANDROID : API_FALLBACK_BASE_IOS;
const BASE_URL = (envBase || fallbackBase).replace(/\/$/, "");
type GetTokenFn = (opts?: { template?: string }) => Promise<string | null>;

// Helper: Construct the full URL
const getUrl = (path: string) => `${BASE_URL}/${path.replace(/^\//, "")}`;

// In development on web, the backend expects a simple "dev-token" string
// rather than a real Clerk JWT. This matches DevelopmentAuthenticationDefaults.DefaultBearerToken.
const DEV_TOKEN = "dev-token";
const isDev = process.env.NODE_ENV !== "production";
// Allow using real auth in development by setting EXPO_PUBLIC_USE_REAL_AUTH=true
const useRealAuth = process.env.EXPO_PUBLIC_USE_REAL_AUTH === "true";

// A pure function isolated from hooks, reusable by both Query and Mutation.
// Handles Token injection and JSON parsing.
export async function fetcher<T>(
  path: string,
  method: string,
  getToken: GetTokenFn,
  body?: unknown,
): Promise<T> {
  const tokenOptions = DEFAULT_TOKEN_TEMPLATE
    ? { template: DEFAULT_TOKEN_TEMPLATE }
    : undefined;

  const tryGetClerkToken = async () => {
    try {
      return (await getToken(tokenOptions)) ?? (await getToken());
    } catch {
      return null;
    }
  };

  const url = getUrl(path);
  
  // Validate URL before making request
  if (!path || path.trim() === "") {
    throw new Error("API path is empty");
  }

  const isFormData =
    typeof FormData !== "undefined" && body instanceof FormData;
  const requestPayload = isFormData
    ? (body as FormData)
    : body
      ? JSON.stringify(body)
      : undefined;

  const maskAuthorization = (headers: Record<string, string>) => {
    if (!("Authorization" in headers)) return headers;
    return { ...headers, Authorization: "Bearer ***" };
  };

  const formatHttpErrorMessage = (status: number, payload: unknown) => {
    if (typeof payload !== "string") {
      const maybeMessage =
        payload &&
        typeof payload === "object" &&
        "message" in payload &&
        typeof (payload as any).message === "string"
          ? ((payload as any).message as string)
          : null;
      return maybeMessage?.trim() || `HTTP ${status}`;
    }

    const raw = payload.trim();
    if (!raw) return `HTTP ${status}`;

    const firstLine = raw.split(/\r?\n/, 1)[0] ?? "";
    const looksLikeStackTrace =
      /\bat\s+\S+/.test(raw) ||
      raw.includes("Microsoft.") ||
      raw.includes("System.") ||
      raw.includes("Exception") ||
      raw.includes("Stack trace");

    const compact = firstLine.length > 180 ? `${firstLine.slice(0, 180)}…` : firstLine;
    return looksLikeStackTrace ? `HTTP ${status}` : compact;
  };

  const doRequest = async (token: string | null) => {
    const headers: Record<string, string> = {
      Accept: "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    };

    // Only set Content-Type for JSON requests, let browser set it for FormData
    if (requestPayload && !isFormData) {
      headers["Content-Type"] = "application/json";
    }

    console.log("[fetcher] Request:", {
      url,
      method,
      path,
      baseUrl: BASE_URL,
      headers: maskAuthorization(headers),
      isFormData,
      bodyType: typeof requestPayload,
    });

    const res = await fetch(url, {
      method,
      headers,
      body: requestPayload as BodyInit | undefined,
    });

    console.log("[fetcher] Response status:", res.status, res.statusText);

    // Handle different content types (JSON vs Text)
    const ct = res.headers.get("content-type") || "";
    const payload = ct.includes("application/json")
      ? await res.json()
      : await res.text();

    return { res, payload };
  };

  let token: string | null = null;
  let clerkToken: string | null = null;

  if (isDev && !useRealAuth) {
    clerkToken = await tryGetClerkToken();
    token = clerkToken ?? DEV_TOKEN;
    console.log(
      `[fetcher] Development mode auth: ${clerkToken ? "Clerk token" : "dev-token"}`,
    );
  } else {
    token = await tryGetClerkToken();
  }

  let { res, payload } = await doRequest(token);

  if (
    isDev &&
    !useRealAuth &&
    res.status === 401 &&
    token &&
    token !== DEV_TOKEN
  ) {
    console.warn(
      "[fetcher] 401 with Clerk token in dev; retrying with dev-token",
    );
    ({ res, payload } = await doRequest(DEV_TOKEN));
  }

  if (!res.ok) {
    const message = formatHttpErrorMessage(res.status, payload);
    const payloadPreview =
      typeof payload === "string"
        ? (payload.trim().split(/\r?\n/, 1)[0] ?? "").slice(0, 200)
        : payload && typeof payload === "object" && "message" in payload
          ? String((payload as any).message).slice(0, 200)
          : null;

    // Suppress logging for expected errors:
    // - 403 "not a member" (user left household, race condition)
    // - 400 "cannot be accepted" (invitation already accepted, e.g., on Expo reload)
    // - 404 "not found" (household deleted or user left, race condition)
    const isExpected403 = res.status === 403 && payloadPreview?.includes("not a member");
    const isExpected400 = res.status === 400 && payloadPreview?.includes("cannot be accepted");
    const isExpected404 = res.status === 404 && payloadPreview?.includes("not found");
    if (!isExpected403 && !isExpected400 && !isExpected404) {
      console.error("[fetcher] Error response:", {
        status: res.status,
        payloadPreview,
      });
    }

    const error = new Error(message) as Error & { status?: number; payload?: unknown };
    error.status = res.status;
    error.payload = payload;
    throw error;
  }

  return payload as T;
}

export function useAuthQuery<TResp = unknown>(
  key: QueryKey, // React Query Cache Key, e.g., ['users', id]
  path: string, // API Endpoint, e.g., 'users/1'
  options?: Omit<UseQueryOptions<TResp, Error>, "queryKey" | "queryFn">,
) {
  const { getToken } = useAuth();

  return useQuery<TResp, Error>({
    queryKey: key,
    queryFn: () => fetcher<TResp>(path, "GET", getToken),
    ...options,
  });
}

export function useAuthLazyQuery<TResp = unknown>(
  key: QueryKey, // React Query Cache Key, e.g., ['users', id]
  path: string, // API Endpoint, e.g., 'users/1'
  options?: Omit<UseQueryOptions<TResp, Error>, "queryKey" | "queryFn">,
) {
  const { getToken } = useAuth();

  return useQuery<TResp, Error>({
    queryKey: key,
    queryFn: () => fetcher<TResp>(path, "GET", getToken),
    ...options,
    enabled: false, // Important: disable automatic fetching
  });
}

export function useAuthMutation<
  TResp = unknown,
  TBody = unknown,
  TContext = unknown
>(
  path: string | ((body: TBody) => string),
  method: "POST" | "PUT" | "PATCH" | "DELETE" = "POST",
  options?: UseMutationOptions<TResp, Error, TBody, TContext>
) {
  const { getToken } = useAuth();

  return useMutation<TResp, Error, TBody, TContext>({
    mutationFn: (body: TBody) => { 
      const finalPath = typeof path === "function" ? path(body) : path;
      if (!finalPath || finalPath.trim() === "") {
        return Promise.reject(new Error("API path is empty - check route parameters"));
      }
      return fetcher<TResp>(finalPath, method, getToken, body);
    },
    ...options,
  });
}

export function useAuthInfiniteQuery<TData extends HasPagination>(
  key: any[],
  basePath: string,
  getToken: () => Promise<string | null>,
  options?: Omit<UseInfiniteQueryOptions<ApiResponse<TData>, Error, InfiniteData<ApiResponse<TData>>>, "queryKey" | "queryFn" | "initialPageParam" | "getNextPageParam">
): UseInfiniteQueryResult<InfiniteData<ApiResponse<TData>>, Error> {
  return useInfiniteQuery<ApiResponse<TData>, Error>({
    queryKey: key,
    queryFn: async ({ pageParam = 1 }) => {
      if (!basePath) {
        throw new Error("Invalid API path for infinite query");
      }
      const connector = basePath.includes("?") ? "&" : "?";
      const url = `${basePath}${connector}page=${pageParam}`;
      return fetcher<ApiResponse<TData>>(url, "GET", getToken);
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const { page, totalPages } = lastPage.data ?? { page: 0, totalPages: 0 };
      return page < totalPages ? page + 1 : undefined;
    },
    ...options,
    enabled: !!basePath && (options?.enabled !== false),
  });
}

// Response type for image upload
export interface ImageUploadResponse {
  url: string;
}

// Hook for uploading images - handles FormData creation
export function useImageUpload(
  options?: UseMutationOptions<ImageUploadResponse, Error, string>,
) {
  const { getToken } = useAuth();

  return useMutation<ImageUploadResponse, Error, string>({
    mutationFn: async (imageUri: string) => {
      console.log("[useImageUpload] Starting upload", {
        platform: Platform.OS,
        imageUri: imageUri.substring(0, 100) + "...",
      });

      const formData = new FormData();

      // Handle different platforms
      if (Platform.OS === "web") {
        // For web, fetch the blob from the URI and append it
        const response = await fetch(imageUri);
        const blob = await response.blob();
        console.log("[useImageUpload] Web blob created", {
          size: blob.size,
          type: blob.type,
        });
        formData.append("file", blob, "image.jpg");
      } else {
        // For native, use the file URI approach
        const filename = imageUri.split("/").pop() || "image.jpg";
        const match = /\.([\w]+)$/.exec(filename);
        const extension = match ? match[1].toLowerCase() : "jpg";

        // Handle HEIC/HEIF images (common on iOS) - convert extension to proper MIME type
        let type: string;
        if (extension === "heic" || extension === "heif") {
          type = "image/heic";
        } else if (extension === "png") {
          type = "image/png";
        } else if (extension === "gif") {
          type = "image/gif";
        } else if (extension === "webp") {
          type = "image/webp";
        } else {
          type = "image/jpeg";
        }

        // Ensure the URI has the correct format for the platform
        // On iOS, ImagePicker returns URIs like 'file:///...' or 'ph://'
        // On Android, it can be 'content://' or 'file://'
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
        console.log("[useImageUpload] Native file object:", fileObject);
        formData.append("file", fileObject as unknown as Blob);
      }

      return fetcher<ImageUploadResponse>(
        "/api/images/upload",
        "POST",
        getToken,
        formData,
      );
    },
    ...options,
  });
}
