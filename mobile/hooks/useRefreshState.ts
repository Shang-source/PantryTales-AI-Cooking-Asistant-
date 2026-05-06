import { useCallback, useEffect, useState } from "react";

export type UseRefreshStateOptions = {
  isFetching: boolean;
  onRefresh?: () => void;
};

export function useRefreshState({
  isFetching,
  onRefresh,
}: UseRefreshStateOptions) {
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [refreshStarted, setRefreshStarted] = useState(false);

  const refresh = useCallback(() => {
    if (isRefreshing) return;
    setIsRefreshing(true);
    onRefresh?.();
  }, [isRefreshing, onRefresh]);

  useEffect(() => {
    if (!isRefreshing) {
      if (refreshStarted) {
        setRefreshStarted(false);
      }
      return;
    }

    if (isFetching && !refreshStarted) {
      setRefreshStarted(true);
      return;
    }

    if (refreshStarted && !isFetching) {
      setIsRefreshing(false);
      setRefreshStarted(false);
    }
  }, [isRefreshing, refreshStarted, isFetching]);

  return { isRefreshing, refresh };
}
