// Legacy color palette - use useThemeColors() from @/contexts/ThemeContext instead
// This is kept for backwards compatibility with components not yet migrated to themes
export const cookPalette = {
  bg: "#5a7872",
  card: "rgba(255,255,255,0.08)",
  border: "rgba(255,255,255,0.16)",
  accent: "#dba7a7",
  pencilColor: "#ffffff",
  trashColor: "#ff6b6b",
};

// Re-export useThemeColors for easy migration
export { useThemeColors } from "@/contexts/ThemeContext";
