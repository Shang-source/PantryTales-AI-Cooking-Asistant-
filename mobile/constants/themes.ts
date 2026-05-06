// Theme color tokens for the app
export interface ThemeColors {
  bg: string;           // Main background
  card: string;         // Card/surface background
  border: string;       // Border color
  accent: string;       // Primary accent
  accentMuted: string;  // Secondary accent
  textPrimary: string;  // Primary text
  textSecondary: string;// Secondary text
  textMuted: string;    // Muted text
  error: string;        // Error/delete actions
  success: string;      // Success states
  overlayText: string;  // Text on dark overlays (modals, etc.)
}

export interface Theme {
  id: string;
  name: string;
  colors: ThemeColors;
}

// Classic (Default) - Current app theme
const classicTheme: Theme = {
  id: "classic",
  name: "Classic",
  colors: {
    bg: "#5a7872",
    card: "rgba(255,255,255,0.08)",
    border: "rgba(255,255,255,0.16)",
    accent: "#dba7a7",
    accentMuted: "#c49595",
    textPrimary: "#ffffff",
    textSecondary: "rgba(255,255,255,0.8)",
    textMuted: "rgba(255,255,255,0.6)",
    error: "#ff6b6b",
    success: "#4ade80",
    overlayText: "#ffffff",
  },
};

// Ocean Blue - Deep navy with sky blue accent
const oceanBlueTheme: Theme = {
  id: "ocean",
  name: "Ocean Blue",
  colors: {
    bg: "#1e3a5f",
    card: "rgba(255,255,255,0.08)",
    border: "rgba(255,255,255,0.16)",
    accent: "#7dd3fc",
    accentMuted: "#5cb8e6",
    textPrimary: "#ffffff",
    textSecondary: "rgba(255,255,255,0.8)",
    textMuted: "rgba(255,255,255,0.6)",
    error: "#ff6b6b",
    success: "#4ade80",
    overlayText: "#ffffff",
  },
};

// Forest Green - Deep forest with mint accent
const forestGreenTheme: Theme = {
  id: "forest",
  name: "Forest Green",
  colors: {
    bg: "#1a3c34",
    card: "rgba(255,255,255,0.08)",
    border: "rgba(255,255,255,0.16)",
    accent: "#86efac",
    accentMuted: "#6bd895",
    textPrimary: "#ffffff",
    textSecondary: "rgba(255,255,255,0.8)",
    textMuted: "rgba(255,255,255,0.6)",
    error: "#ff6b6b",
    success: "#4ade80",
    overlayText: "#ffffff",
  },
};

// Sunset Orange - Burnt sienna with warm peach accent
const sunsetOrangeTheme: Theme = {
  id: "sunset",
  name: "Sunset Orange",
  colors: {
    bg: "#7c2d12",
    card: "rgba(255,255,255,0.08)",
    border: "rgba(255,255,255,0.16)",
    accent: "#fdba74",
    accentMuted: "#e5a565",
    textPrimary: "#ffffff",
    textSecondary: "rgba(255,255,255,0.8)",
    textMuted: "rgba(255,255,255,0.6)",
    error: "#ff6b6b",
    success: "#4ade80",
    overlayText: "#ffffff",
  },
};

// Warm Cream - Light cream background with golden accent (light theme)
const warmCreamTheme: Theme = {
  id: "cream",
  name: "Warm Cream",
  colors: {
    bg: "#F5F0E6",
    card: "#FFFFFF",
    border: "rgba(0,0,0,0.18)",
    accent: "#D4A056",
    accentMuted: "#C9985A",
    textPrimary: "#2D2A26",
    textSecondary: "rgba(45,42,38,0.7)",
    textMuted: "rgba(45,42,38,0.5)",
    error: "#DC4C4C",
    success: "#5B8A5F",
    overlayText: "#ffffff",
  },
};

// Midnight Purple - Dark purple/navy with violet accent
const midnightPurpleTheme: Theme = {
  id: "midnight",
  name: "Midnight Purple",
  colors: {
    bg: "#1a1a2e",
    card: "#25253d",
    border: "rgba(139,92,246,0.25)",
    accent: "#8b5cf6",
    accentMuted: "#7c4fe0",
    textPrimary: "#ffffff",
    textSecondary: "rgba(255,255,255,0.7)",
    textMuted: "rgba(255,255,255,0.5)",
    error: "#f87171",
    success: "#4ade80",
    overlayText: "#ffffff",
  },
};

// Pure White - Clean white theme with blue accent (light theme)
const pureWhiteTheme: Theme = {
  id: "white",
  name: "Pure White",
  colors: {
    bg: "#FFFFFF",
    card: "#F5F5F5",
    border: "rgba(0,0,0,0.2)",
    accent: "#3B82F6",
    accentMuted: "#2563EB",
    textPrimary: "#1A1A1A",
    textSecondary: "rgba(26,26,26,0.7)",
    textMuted: "rgba(26,26,26,0.5)",
    error: "#DC2626",
    success: "#16A34A",
    overlayText: "#ffffff",
  },
};

// Pure Black - OLED black theme with white accent (dark theme)
const pureBlackTheme: Theme = {
  id: "black",
  name: "Pure Black",
  colors: {
    bg: "#000000",
    card: "#121212",
    border: "rgba(255,255,255,0.15)",
    accent: "#FFFFFF",
    accentMuted: "#E5E5E5",
    textPrimary: "#FFFFFF",
    textSecondary: "rgba(255,255,255,0.7)",
    textMuted: "rgba(255,255,255,0.5)",
    error: "#FF4444",
    success: "#4ADE80",
    overlayText: "#ffffff",
  },
};

// Theme registry
export const themes: Record<string, Theme> = {
  classic: classicTheme,
  ocean: oceanBlueTheme,
  forest: forestGreenTheme,
  sunset: sunsetOrangeTheme,
  cream: warmCreamTheme,
  midnight: midnightPurpleTheme,
  white: pureWhiteTheme,
  black: pureBlackTheme,
};

export const themeList: Theme[] = [
  pureWhiteTheme,
  warmCreamTheme,
  classicTheme,
  sunsetOrangeTheme,
  oceanBlueTheme,
  forestGreenTheme,
  midnightPurpleTheme,
  pureBlackTheme,
];

export const defaultThemeId = "white";
