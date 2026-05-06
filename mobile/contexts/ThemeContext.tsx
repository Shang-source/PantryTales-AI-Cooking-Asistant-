import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  useMemo,
} from "react";
import AsyncStorage from "@react-native-async-storage/async-storage";
import { themes, defaultThemeId, type Theme, type ThemeColors } from "@/constants/themes";

const THEME_STORAGE_KEY = "@app_theme";

interface ThemeContextValue {
  theme: Theme;
  themeId: string;
  colors: ThemeColors;
  setTheme: (themeId: string) => void;
  isLoading: boolean;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

interface ThemeProviderProps {
  children: React.ReactNode;
}

export function ThemeProvider({ children }: ThemeProviderProps) {
  const [themeId, setThemeId] = useState<string>(defaultThemeId);
  const [isLoading, setIsLoading] = useState(true);

  // Load saved theme on mount
  useEffect(() => {
    const loadTheme = async () => {
      try {
        const savedThemeId = await AsyncStorage.getItem(THEME_STORAGE_KEY);
        if (savedThemeId && themes[savedThemeId]) {
          setThemeId(savedThemeId);
        }
      } catch (error) {
        console.warn("Failed to load theme preference:", error);
      } finally {
        setIsLoading(false);
      }
    };
    loadTheme();
  }, []);

  const setTheme = useCallback(async (newThemeId: string) => {
    if (!themes[newThemeId]) {
      console.warn(`Theme "${newThemeId}" not found`);
      return;
    }
    setThemeId(newThemeId);
    try {
      await AsyncStorage.setItem(THEME_STORAGE_KEY, newThemeId);
    } catch (error) {
      console.warn("Failed to save theme preference:", error);
    }
  }, []);

  const theme = themes[themeId] || themes[defaultThemeId];

  const value = useMemo(
    () => ({
      theme,
      themeId,
      colors: theme.colors,
      setTheme,
      isLoading,
    }),
    [theme, themeId, setTheme, isLoading]
  );

  return (
    <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
  );
}

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);
  if (context === undefined) {
    throw new Error("useTheme must be used within a ThemeProvider");
  }
  return context;
}

// Hook for getting just the colors (convenience)
export function useThemeColors(): ThemeColors {
  const { colors } = useTheme();
  return colors;
}
