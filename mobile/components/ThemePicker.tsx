import React from "react";
import { View, Text, TouchableOpacity, ScrollView } from "react-native";
import { Check } from "lucide-react-native";
import { useTheme } from "@/contexts/ThemeContext";
import { themeList, type Theme } from "@/constants/themes";

interface ThemeSwatchProps {
  theme: Theme;
  isSelected: boolean;
  onSelect: () => void;
}

function ThemeSwatch({ theme, isSelected, onSelect }: ThemeSwatchProps) {
  const { colors } = useTheme();
  return (
    <TouchableOpacity
      onPress={onSelect}
      activeOpacity={0.7}
      className="mr-3"
    >
      <View
        className="w-20 h-20 rounded-2xl overflow-hidden border-2"
        style={{ borderColor: isSelected ? colors.textPrimary : colors.border }}
      >
        {/* Background color fills the swatch */}
        <View
          style={{ backgroundColor: theme.colors.bg }}
          className="flex-1 items-center justify-center"
        >
          {/* Accent color circle */}
          <View
            style={{ backgroundColor: theme.colors.accent }}
            className="w-8 h-8 rounded-full items-center justify-center"
          >
            {isSelected && <Check size={16} color={theme.colors.bg} />}
          </View>
        </View>
      </View>
      <Text
        className="text-center text-xs mt-1.5"
        style={{ color: isSelected ? colors.textPrimary : colors.textSecondary, fontWeight: isSelected ? "600" : "400" }}
        numberOfLines={1}
      >
        {theme.name}
      </Text>
    </TouchableOpacity>
  );
}

export function ThemePicker() {
  const { themeId, setTheme } = useTheme();

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerClassName="px-1 py-2"
    >
      {themeList.map((theme) => (
        <ThemeSwatch
          key={theme.id}
          theme={theme}
          isSelected={themeId === theme.id}
          onSelect={() => setTheme(theme.id)}
        />
      ))}
    </ScrollView>
  );
}
