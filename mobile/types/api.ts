export type ApiResponse<T> = {
  code: number;
  message: string;
  data?: T;
};

export interface HasPagination {
  page: number;
  totalPages: number;
}

export interface PagedResponse<T> extends HasPagination {
  items: T[];
  pageSize: number;
  totalCount: number;
}
