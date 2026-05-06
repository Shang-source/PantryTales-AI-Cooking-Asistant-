// Mock AsyncStorage
jest.mock("@react-native-async-storage/async-storage", () =>
  require("@react-native-async-storage/async-storage/jest/async-storage-mock")
);

// Mock expo-haptics
jest.mock("expo-haptics", () => ({
  impactAsync: jest.fn(),
  notificationAsync: jest.fn(),
  selectionAsync: jest.fn(),
  ImpactFeedbackStyle: {
    Light: "light",
    Medium: "medium",
    Heavy: "heavy",
  },
  NotificationFeedbackType: {
    Success: "success",
    Warning: "warning",
    Error: "error",
  },
}));

// Mock react-native-reanimated
jest.mock("react-native-reanimated", () => {
  const Reanimated = require("react-native-reanimated/mock");
  Reanimated.default.call = () => {};
  return Reanimated;
});

// Mock Clerk
jest.mock("@clerk/clerk-expo", () => ({
  useAuth: jest.fn(() => ({
    isSignedIn: true,
    userId: "test-user-id",
    getToken: jest.fn(() => Promise.resolve("test-token")),
  })),
  useUser: jest.fn(() => ({
    user: { id: "test-user-id", firstName: "Test", lastName: "User" },
    isLoaded: true,
  })),
  ClerkProvider: ({ children }) => children,
}));

// Mock ThemeContext
jest.mock("@/contexts/ThemeContext", () => ({
  useTheme: jest.fn(() => ({
    theme: {
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
    },
    themeId: "classic",
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
    setTheme: jest.fn(),
    isLoading: false,
  })),
  useThemeColors: jest.fn(() => ({
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
  })),
  ThemeProvider: ({ children }) => children,
}));

// Silence console warnings in tests
const originalWarn = console.warn;
console.warn = (...args) => {
  if (
    args[0]?.includes?.("Animated") ||
    args[0]?.includes?.("useNativeDriver")
  ) {
    return;
  }
  originalWarn(...args);
};
