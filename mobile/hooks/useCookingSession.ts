import { useAuthQuery } from "@/hooks/useApi";
import type { ApiResponse } from "@/types/api";

type CookingStepDto = {
  order: number;
  instruction: string;
  suggestedSeconds?: number | null;
};

export type CookingSessionDto = {
  recipeId: string;
  title: string;
  totalSteps: number;
  steps: CookingStepDto[];
};

export function useCookingSession(recipeId?: string) {
  return useAuthQuery<ApiResponse<CookingSessionDto>>(
    ["cook", recipeId ?? ""],
    recipeId ? `/api/recipes/${recipeId}/cook` : "",
    { enabled: Boolean(recipeId) },
  );
}
