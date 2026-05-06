import { useCallback, useMemo } from "react";
import { useAuth } from "@clerk/clerk-expo";
import {
  useAuthInfiniteQuery,
  useAuthMutation,
  useAuthQuery,
  fetcher,
} from "@/hooks/useApi";
import { ApiResponse } from "@/types/api";
import {
  CreateInventoryItemRequest,
  InventoryStatsResponseDto,
  InventoryItemForm,
  InventoryItemResponseDto,
  InventoryListResponseDto,
  InventorySortBy,
  StorageMethodString,
  UpdateInventoryItemRequest,
  parseStorageMethod,
} from "@/types/Inventory";
import { InfiniteData, useMutation, useQueryClient } from "@tanstack/react-query";

function mapToInventoryItem(dto: InventoryItemResponseDto): InventoryItemForm {
  return {
    id: dto.id,
    name: dto.name,
    amount: dto.amount.toString(),
    unit: dto.unit,
    storage: dto.storageMethod,
    addedDate: dto.createdAt,
    expiryDays: dto.daysRemaining?.toString() ?? "",
  };
}

interface InventoryListOptions {
  sortBy: InventorySortBy;
  storageFilter: "all" | StorageMethodString;
  keyword: string;
}

export function useInventoryList({
  sortBy = "Expiring",
  storageFilter = "all",
  keyword = "",
}: Partial<InventoryListOptions> = {}) {
  const { getToken } = useAuth();
  const queryClient = useQueryClient();
  const queryKey = ["inventory-item", sortBy, storageFilter, keyword];
  const basePath = `/api/inventory?sortBy=${sortBy}${
    storageFilter !== "all" ? `&storageMethod=${storageFilter}` : ""
  }${keyword ? `&keyword=${keyword}` : ""}`;

  const query = useAuthInfiniteQuery<InventoryListResponseDto>(
    queryKey,
    basePath,
    getToken,
  );

  const statsQuery = useAuthQuery<ApiResponse<InventoryStatsResponseDto>>(
    ["inventory-stats"],
    "/api/inventory/stats",
  );

  const refreshInventory = useCallback(() => {
    query.refetch();
    statsQuery.refetch();
  }, [query.refetch, statsQuery.refetch]);

  const items = useMemo(
    () =>
      query.data?.pages.flatMap(
        (pageResponse) =>
          pageResponse?.data?.data?.map(mapToInventoryItem) ?? [],
      ) ?? [],
    [query.data],
  );

  const createInventoryItem = useMutation<
    ApiResponse<InventoryItemResponseDto>,
    Error,
    InventoryItemForm
  >({
    mutationFn: async (item) => {
      const payload: CreateInventoryItemRequest = {
        name: item.name.trim(),
        amount: Number(item.amount),
        unit: item.unit,
        storageMethod: parseStorageMethod(item.storage),
        expirationDays: item.expiryDays === "" ? null : Number(item.expiryDays),
      };
      return fetcher<ApiResponse<InventoryItemResponseDto>>(
        "/api/inventory",
        "POST",
        getToken,
        payload,
      );
    },
    onSuccess: (resp) => {
      if (resp.code === 0 && resp.data) {
        const responseItem = resp.data;
        queryClient.setQueryData<
          InfiniteData<ApiResponse<InventoryListResponseDto>>
        >(queryKey, (old) => {
          if (!old?.pages?.length) return old;
          const [first, ...rest] = old.pages;
          if (!first?.data?.data) return old;
          const nextFirst = {
            ...first,
            data: {
              ...first.data,
              data: [responseItem, ...first.data.data],
            },
          };
          return { ...old, pages: [nextFirst, ...rest] };
        });
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory-stats"] });
    },
  });

  const updateInventoryItem = useMutation<
    ApiResponse<InventoryItemResponseDto>,
    Error,
    InventoryItemForm,
    { previousData?: InfiniteData<ApiResponse<InventoryListResponseDto>> }
  >({
    mutationFn: async (item) => {
      const payload: UpdateInventoryItemRequest = {
        amount: Number(item.amount),
        unit: item.unit,
        storageMethod: parseStorageMethod(item.storage),
        expirationDays: item.expiryDays === "" ? null : Number(item.expiryDays),
      };
      return fetcher<ApiResponse<InventoryItemResponseDto>>(
        `/api/inventory/${item.id}`,
        "PATCH",
        getToken,
        payload,
      );
    },
    onMutate: async (item) => {
      await queryClient.cancelQueries({ queryKey });
      const previousData = queryClient.getQueryData<
        InfiniteData<ApiResponse<InventoryListResponseDto>>
      >(queryKey);

      queryClient.setQueryData<
        InfiniteData<ApiResponse<InventoryListResponseDto>>
      >(queryKey, (old) => {
        if (!old?.pages?.length) return old;
        return {
          ...old,
          pages: old.pages.map((page) => {
            if (!page?.data?.data) return page;
            return {
              ...page,
              data: {
                ...page.data,
                data: page.data.data.map((row) =>
                  row.id === item.id
                    ? {
                        ...row,
                        name: item.name,
                        amount: Number(item.amount),
                        unit: item.unit,
                        storageMethod: item.storage,
                      }
                    : row,
                ),
              },
            };
          }),
        };
      });

      return { previousData };
    },
    onError: (_err, _item, context) => {
      if (context?.previousData) {
        queryClient.setQueryData(queryKey, context.previousData);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey });
      queryClient.invalidateQueries({ queryKey: ["inventory-stats"] });
    },
  });

  const deleteInventoryItem = useAuthMutation<
    ApiResponse<void>,
    { id: string },
    { previousData?: InfiniteData<ApiResponse<InventoryListResponseDto>> }
  >(
    ({ id }) => `/api/inventory/${id}`,
    "DELETE",
    {
      onMutate: async ({ id }) => {
        await queryClient.cancelQueries({ queryKey });
        const previousData = queryClient.getQueryData<
          InfiniteData<ApiResponse<InventoryListResponseDto>>
        >(queryKey);
        queryClient.setQueryData<
          InfiniteData<ApiResponse<InventoryListResponseDto>>
        >(queryKey, (old) => {
          if (!old?.pages?.length) return old;
          return {
            ...old,
            pages: old.pages.map((page) => {
              if (!page?.data?.data) return page;
              return {
                ...page,
                data: {
                  ...page.data,
                  data: page.data.data.filter((row) => row.id !== id),
                  totalCount:
                    typeof page.data.totalCount === "number"
                      ? Math.max(page.data.totalCount - 1, 0)
                      : page.data.totalCount,
                },
              };
            }),
          };
        });
        return { previousData };
      },
      onError: (_err, _vars, context) => {
        if (context?.previousData) {
          queryClient.setQueryData(queryKey, context.previousData);
        }
      },
      onSettled: () => {
        queryClient.invalidateQueries({ queryKey });
        queryClient.invalidateQueries({ queryKey: ["inventory-stats"] });
      },
    },
  );

  return {
    ...query,
    items,
    statsQuery,
    refreshInventory,
    createInventoryItem,
    updateInventoryItem,
    deleteInventoryItem,
  };
}
