export const PROFILE_CACHE_KEY = "user_profile_cache";
export const PROFILE_CACHE_TTL_MS = 10 * 60 * 1000; // 10 minutes

export const API_FALLBACK_BASE_IOS = "http://localhost:5158";
export const API_FALLBACK_BASE_ANDROID = "http://10.0.2.2:5158";
export const DEFAULT_TOKEN_TEMPLATE =
  process.env.EXPO_PUBLIC_CLERK_TOKEN_TEMPLATE?.trim() || "PantryTales";

// Key for storing pending invitation ID when user needs to login first
export const PENDING_INVITATION_KEY = "pending_invitation_id";

// Key for storing pending invitation token when user needs to login first
export const PENDING_INVITATION_TOKEN_KEY = "pending_invitation_token";
