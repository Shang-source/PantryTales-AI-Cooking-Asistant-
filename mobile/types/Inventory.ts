export enum StorageMethod { 
  RoomTemp = 1, 
  Refrigerated = 2,
  Frozen = 3,
  Other = 9,
}
export type StorageMethodString = "RoomTemp" | "Refrigerated" | "Frozen";

export type IngredientResolveStatus = "Pending" | "Resolved" | "NeedsReview" | "Failed";

export type InventorySortBy = "Expiring" | "DateAdded" | "Name";

export type SortOrder = "Asc" | "Desc";

export const STORAGE_FILTERS: {
  value: StorageMethodString;
  label: string;
  icon: string;
}[] = [
  { value: "RoomTemp", label: "Room Temp", icon: "home" },
  {
    value: "Refrigerated",
    label: "Refrigerated",
    icon: "fridge",
  },
  { value: "Frozen", label: "Frozen", icon: "snowflake" }
];

export function parseStorageMethod(
  value: StorageMethodString
): StorageMethod {
  return StorageMethod[value];
}

export interface InventoryItemForm {
  id: string;
  name: string;
  amount: string;
  unit: string;
  storage: StorageMethodString;
  addedDate: string;
  expiryDays: string;
}

export interface CreateInventoryItemRequest {
  name: string;
  amount: number;
  unit: string;
  storageMethod: StorageMethod;
  expirationDays?: number | null;
}

export interface UpdateInventoryItemRequest {
  amount: number;
  unit: string;
  storageMethod: StorageMethod;
  expirationDays?: number | null;
}

export interface InventoryItemResponseDto {
  id: string;
  householdId: string;
  ingredientId?: string | null;
  name: string;
  normalizedName?: string | null;
  resolveStatus: IngredientResolveStatus;
  resolveConfidence?: number | null;
  resolveMethod?: string | null;
  resolvedAt?: string | null;
  resolveAttempts: number;
  lastResolveError?: string | null;
  amount: number;
  unit: string;
  storageMethod: StorageMethodString;
  daysRemaining?: number | null;
  createdAt: string;
}

export interface InventoryListRequestDto {
  keyword?: string;
  storageMethod?: StorageMethod;
  sortBy?: InventorySortBy;
  sortOrder?: SortOrder;
  page?: number;
  pageSize?: number;
}

export interface InventoryListResponseDto {
  data: InventoryItemResponseDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface InventoryStatsResponseDto {
  totalCount: number;
  expiringSoonCount: number;
  storageMethodCount: number;
}
