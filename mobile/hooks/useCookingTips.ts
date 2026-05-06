import { useAuthQuery } from "./useApi";
import type { ApiResponse } from "@/types/api";

/**
 * Cooking tip article for the homepage ticker.
 */
export interface CookingTip {
  id: string;
  tagId: number;
  title: string;
  subtitle?: string | null;
}

interface FeaturedArticleDto {
  id: string;
  tagId: number;
  title: string;
  subtitle?: string | null;
  iconName?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface UseCookingTipsResult {
  /** Featured cooking tips for the ticker */
  tips: CookingTip[];
  /** Whether the tips are loading */
  isLoading: boolean;
  /** Error if fetching failed */
  error: Error | null;
  /** Refetch the tips */
  refetch: () => void;
}

/**
 * Hook to fetch featured cooking tips for the homepage ticker.
 * Fetches from /api/knowledgebase/featured endpoint.
 *
 * @param count - Number of tips to fetch (default 10)
 */
export function useCookingTips(count: number = 10): UseCookingTipsResult {
  const {
    data: response,
    isLoading,
    error,
    refetch,
  } = useAuthQuery<ApiResponse<FeaturedArticleDto[]>>(
    ["knowledgebase-featured", count],
    `/api/knowledgebase/featured?count=${count}`,
    {
      staleTime: 5 * 60 * 1000, // 5 minutes
      refetchOnWindowFocus: false,
    }
  );

  // Transform the response to CookingTip format
  const tips: CookingTip[] = (response?.data ?? []).map((article) => ({
    id: article.id,
    tagId: article.tagId,
    title: article.title,
    subtitle: article.subtitle,
  }));

  return {
    tips,
    isLoading,
    error: error ?? null,
    refetch,
  };
}

export default useCookingTips;
