export const getDefaultAvatarUrl = (userId?: string | null): string | null => {
  if (!userId) return null;
  return `https://picsum.photos/seed/${userId}/200`;
};
