import { useMemo } from "react";
import { useAuthQuery, useAuthMutation, fetcher } from "./useApi";
import { useQueryClient, useMutation, useQuery } from "@tanstack/react-query";
import { useAuth } from "@clerk/clerk-expo";
import { ApiResponse } from "@/types/api";

// Tag types for category colors
interface TagChip {
  id: number;
  name: string;
  displayName: string;
  type: string;
  color?: string;
}

interface TagGroup {
  type: string;
  typeDisplayName: string;
  tags: TagChip[];
}

// Types matching backend DTOs
export interface ChecklistItemDto {
  id: string;
  name: string;
  amount: number;
  unit: string;
  category?: string;
  isChecked: boolean;
  fromRecipeId?: string;
  fromRecipeName?: string;
  createdAt: string;
  updatedAt: string;
}

export interface ChecklistListDto {
  items: ChecklistItemDto[];
  totalCount: number;
}

export interface ChecklistSection {
  title: string;
  data: ChecklistItemDto[];
}

export interface ChecklistStatsDto {
  totalCount: number;
  purchasedCount: number;
  remainingCount: number;
}

export interface CreateChecklistItemDto {
  name: string;
  amount: number;
  unit: string;
  category?: string;
  fromRecipeId?: string;
}

export interface UpdateChecklistItemDto {
  name?: string;
  amount?: number;
  unit?: string;
  category?: string;
  isChecked?: boolean;
}

export interface BatchCreateChecklistItemsDto {
  items: CreateChecklistItemDto[];
  fromRecipeId?: string;
}

// Query key
const CHECKLIST_KEY = ["checklist"];

/**
 * Get all checklist items
 */
export function useChecklist() {
  const query = useAuthQuery<ApiResponse<ChecklistListDto>>(
    CHECKLIST_KEY,
    "/api/checklist"
  );
  const sections = useMemo<ChecklistSection[]>(() => {
    const items = query.data?.data?.items ?? [];
    const groups: Record<string, ChecklistItemDto[]> = {};
    items.forEach((item) => {
      const cat = item.category || "Other";
      if (!groups[cat]) groups[cat] = [];
      groups[cat].push(item);
    });
    return Object.entries(groups)
      .map(([category, grouped]) => ({
        title: category,
        data: grouped.sort((a, b) => a.name.localeCompare(b.name)),
      }))
      .sort((a, b) => {
        if (a.title === "Other" && b.title !== "Other") return 1;
        if (b.title === "Other" && a.title !== "Other") return -1;
        return a.title.localeCompare(b.title);
      });
  }, [query.data]);

  return { ...query, sections };
}

export function useChecklistStats() {
  return useAuthQuery<ApiResponse<ChecklistStatsDto>>(
    [...CHECKLIST_KEY, "stats"],
    "/api/checklist/stats"
  );
}

/**
 * Add a single item to checklist
 */
export function useAddChecklistItem() {
  const queryClient = useQueryClient();
  return useAuthMutation<
    ApiResponse<ChecklistItemDto>,
    CreateChecklistItemDto,
    { previousData?: ApiResponse<ChecklistListDto>; tempId?: string }
  >(
    "/api/checklist",
    "POST",
    {
      onMutate: async (payload) => {
        await queryClient.cancelQueries({ queryKey: CHECKLIST_KEY });
        const previousData =
          queryClient.getQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY);
        const tempId = `temp-${Date.now()}-${Math.random().toString(16).slice(2)}`;
        const optimisticItem: ChecklistItemDto = {
          id: tempId,
          name: payload.name,
          amount: payload.amount,
          unit: payload.unit,
          category: payload.category,
          isChecked: false,
          fromRecipeId: payload.fromRecipeId,
          fromRecipeName: undefined,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        };

        queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
          CHECKLIST_KEY,
          (old) => {
            if (!old?.data?.items) return old;
            return {
              ...old,
              data: {
                ...old.data,
                items: [optimisticItem, ...old.data.items],
                totalCount:
                  typeof old.data.totalCount === "number"
                    ? old.data.totalCount + 1
                    : old.data.totalCount,
              },
            };
          },
        );

        return { previousData };
      },
      onError: (
        _err,
        _vars,
        context: { previousData?: ApiResponse<ChecklistListDto> } | undefined,
      ) => {
        if (context?.previousData) {
          queryClient.setQueryData(CHECKLIST_KEY, context.previousData);
        }
      },
      onSuccess: (
        resp,
        _vars,
        context: {
          previousData?: ApiResponse<ChecklistListDto>;
          tempId?: string;
        } | undefined,
      ) => {
        if (resp.code !== 0 || !resp.data) {
          queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
          return;
        }
        const responseItem = resp.data;
        queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
          CHECKLIST_KEY,
          (old) => {
            if (!old?.data?.items) return old;
            const nextItems = context?.tempId
              ? old.data.items.filter((item) => item.id !== context.tempId)
              : old.data.items;
            return {
              ...old,
              data: {
                ...old.data,
                items: [responseItem, ...nextItems],
              },
            };
          },
        );
        queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
      },
    }
  );
}

/**
 * Add multiple items to checklist (from recipe)
 */
export function useAddChecklistBatch() {
  const queryClient = useQueryClient();
  return useAuthMutation<
    ApiResponse<ChecklistItemDto[]>,
    BatchCreateChecklistItemsDto,
    { previousData?: ApiResponse<ChecklistListDto>; tempIds?: string[] }
  >("/api/checklist/batch", "POST", {
    onMutate: async (payload) => {
      await queryClient.cancelQueries({ queryKey: CHECKLIST_KEY });
      const previousData =
        queryClient.getQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY);
      const timestamp = new Date().toISOString();
      const tempIds: string[] = [];
      const optimisticItems: ChecklistItemDto[] = payload.items.map((item) => {
        const tempId = `temp-${Date.now()}-${Math.random().toString(16).slice(2)}`;
        tempIds.push(tempId);
        return {
          id: tempId,
          name: item.name,
          amount: item.amount,
          unit: item.unit,
          category: item.category,
          isChecked: false,
          fromRecipeId: item.fromRecipeId ?? payload.fromRecipeId,
          fromRecipeName: undefined,
          createdAt: timestamp,
          updatedAt: timestamp,
        };
      });

      queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
        CHECKLIST_KEY,
        (old) => {
          if (!old?.data?.items) return old;
          return {
            ...old,
            data: {
              ...old.data,
              items: [...optimisticItems, ...old.data.items],
              totalCount:
                typeof old.data.totalCount === "number"
                  ? old.data.totalCount + optimisticItems.length
                  : old.data.totalCount,
            },
          };
        },
      );

      return { previousData, tempIds };
    },
    onError: (
      _err,
      _vars,
      context: { previousData?: ApiResponse<ChecklistListDto> } | undefined,
    ) => {
      if (context?.previousData) {
        queryClient.setQueryData(CHECKLIST_KEY, context.previousData);
      }
    },
    onSuccess: (
      resp,
      _vars,
      context: {
        previousData?: ApiResponse<ChecklistListDto>;
        tempIds?: string[];
      } | undefined,
    ) => {
      if (resp.code !== 0 || !resp.data) {
        queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
        return;
      }
      const responseItems = resp.data;
      queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
        CHECKLIST_KEY,
        (old) => {
          if (!old?.data?.items) return old;
          const nextItems = context?.tempIds?.length
            ? old.data.items.filter(
                (item) => !context.tempIds?.includes(item.id),
              )
            : old.data.items;
          return {
            ...old,
            data: {
              ...old.data,
              items: [...responseItems, ...nextItems],
            },
          };
        },
      );
      queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
    },
  });
}

/**
 * Update a checklist item with optimistic update for instant UI feedback
 */
export function useUpdateChecklistItem() {
  const queryClient = useQueryClient();
  const { getToken } = useAuth();

  return useMutation<
    ApiResponse<ChecklistItemDto>,
    Error,
    { id: string; data: UpdateChecklistItemDto },
    { previousData?: ApiResponse<ChecklistListDto> }
  >({
    mutationFn: async ({ id, data }) => {
      return fetcher<ApiResponse<ChecklistItemDto>>(
        `/api/checklist/${id}`,
        "PUT",
        getToken,
        data,
      );
    },
    // Optimistic update - immediately update UI before server response
    onMutate: async ({ id, data }) => {
      // Cancel any outgoing refetches
      await queryClient.cancelQueries({ queryKey: CHECKLIST_KEY });

      // Snapshot the previous value
      const previousData = queryClient.getQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY);

      // Optimistically update the cache
      queryClient.setQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY, (old) => {
        if (!old?.data?.items) return old;
        return {
          ...old,
          data: {
            ...old.data,
            items: old.data.items.map((item) =>
              item.id === id ? { ...item, ...data } : item
            ),
          },
        };
      });

      // Return context with the previous data for rollback
      return { previousData };
    },
    // On error, rollback to the previous value
    onError: (_err, _variables, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(CHECKLIST_KEY, context.previousData);
      }
    },
    // Always refetch after error or success
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
    },
  });
}

/**
 * Delete a checklist item
 */
export function useDeleteChecklistItem() {
  const queryClient = useQueryClient();
  const { getToken } = useAuth();

  return useMutation<
    ApiResponse<void>,
    Error,
    string,
    { previousData?: ApiResponse<ChecklistListDto> }
  >({
    mutationFn: async (id) => {
      return fetcher<ApiResponse<void>>(
        `/api/checklist/${id}`,
        "DELETE",
        getToken,
      );
    },
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: CHECKLIST_KEY });

      const previousData =
        queryClient.getQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY);

      queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
        CHECKLIST_KEY,
        (old) => {
          if (!old?.data?.items) return old;
          const items = old.data.items.filter((item) => item.id !== id);
          const totalCount =
            typeof old.data.totalCount === "number"
              ? Math.max(old.data.totalCount - 1, 0)
              : old.data.totalCount;
          return {
            ...old,
            data: {
              ...old.data,
              items,
              totalCount,
            },
          };
        },
      );

      return { previousData };
    },
    onError: (_err, _id, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(CHECKLIST_KEY, context.previousData);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
    },
  });
}

/**
 * Clear all checked items
 */
export function useClearCheckedItems() {
  const queryClient = useQueryClient();
  const { getToken } = useAuth();

  return useMutation<
    ApiResponse<number>,
    Error,
    void,
    { previousData?: ApiResponse<ChecklistListDto> }
  >({
    mutationFn: async () => {
      return fetcher<ApiResponse<number>>(
        "/api/checklist/clear-checked",
        "DELETE",
        getToken,
      );
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: CHECKLIST_KEY });

      const previousData =
        queryClient.getQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY);

      queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
        CHECKLIST_KEY,
        (old) => {
          if (!old?.data?.items) return old;
          const remainingItems = old.data.items.filter(
            (item) => !item.isChecked,
          );
          return {
            ...old,
            data: {
              ...old.data,
              items: remainingItems,
              totalCount: remainingItems.length,
            },
          };
        },
      );

      return { previousData };
    },
    onError: (_err, _vars, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(CHECKLIST_KEY, context.previousData);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
    },
  });
}

/**
 * Clear all checklist items
 */
export function useClearAllItems() {
  const queryClient = useQueryClient();
  const { getToken } = useAuth();

  return useMutation<
    ApiResponse<number>,
    Error,
    void,
    { previousData?: ApiResponse<ChecklistListDto> }
  >({
    mutationFn: async () => {
      return fetcher<ApiResponse<number>>(
        "/api/checklist/clear-all",
        "DELETE",
        getToken,
      );
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: CHECKLIST_KEY });

      const previousData =
        queryClient.getQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY);

      queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
        CHECKLIST_KEY,
        (old) => {
          if (!old?.data) return old;
          return {
            ...old,
            data: {
              ...old.data,
              items: [],
              totalCount: 0,
            },
          };
        },
      );

      return { previousData };
    },
    onError: (_err, _vars, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(CHECKLIST_KEY, context.previousData);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
      queryClient.invalidateQueries({ queryKey: [...CHECKLIST_KEY, "stats"] });
    },
  });
}

export interface MoveToInventoryResultDto {
  itemsMoved: number;
}

/**
 * Move all checked items to inventory
 */
export function useMoveCheckedToInventory() {
  const queryClient = useQueryClient();
  const { getToken } = useAuth();

  return useMutation<
    ApiResponse<MoveToInventoryResultDto>,
    Error,
    void,
    { previousData?: ApiResponse<ChecklistListDto> }
  >({
    mutationFn: async () => {
      return fetcher<ApiResponse<MoveToInventoryResultDto>>(
        "/api/checklist/move-to-inventory",
        "POST",
        getToken,
      );
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: CHECKLIST_KEY });

      const previousData =
        queryClient.getQueryData<ApiResponse<ChecklistListDto>>(CHECKLIST_KEY);

      queryClient.setQueryData<ApiResponse<ChecklistListDto>>(
        CHECKLIST_KEY,
        (old) => {
          if (!old?.data?.items) return old;
          const remainingItems = old.data.items.filter(
            (item) => !item.isChecked,
          );
          return {
            ...old,
            data: {
              ...old.data,
              items: remainingItems,
              totalCount: remainingItems.length,
            },
          };
        },
      );

      return { previousData };
    },
    onError: (_err, _vars, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(CHECKLIST_KEY, context.previousData);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: CHECKLIST_KEY });
      queryClient.invalidateQueries({ queryKey: ["inventory-item"] });
      queryClient.invalidateQueries({ queryKey: ["inventory-stats"] });
    },
  });
}

/**
 * Fetch category colors from ingredient_type tags
 */
export function useCategoryColors() {
  const { getToken } = useAuth();

  const query = useQuery<TagGroup[]>({
    queryKey: ["tags"],
    queryFn: async () => {
      const data = await fetcher<TagGroup[]>("/api/tags", "GET", getToken);
      return data;
    },
    staleTime: 1000 * 60 * 60, // Cache for 1 hour
  });

  const categoryColors = useMemo<Record<string, string>>(() => {
    const colors: Record<string, string> = {};
    const ingredientTypes = query.data?.find((g) => g.type === "ingredient_type");
    if (ingredientTypes) {
      ingredientTypes.tags.forEach((tag) => {
        if (tag.color) {
          colors[tag.displayName] = tag.color;
        }
      });
    }
    return colors;
  }, [query.data]);

  return { categoryColors, isLoading: query.isLoading };
}
