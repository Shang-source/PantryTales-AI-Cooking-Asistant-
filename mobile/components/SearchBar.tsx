import type { ComponentProps } from "react";
import { View, Platform, TextInput } from "react-native";
import { Feather } from "@expo/vector-icons";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

type SearchBarProps = Omit<ComponentProps<typeof TextInput>, "className"> & {
  className?: string;
  inputClassName?: string;
};

export function SearchBar({
  className,
  inputClassName,
  placeholder = "Search",
  style,
  ...props
}: SearchBarProps) {
  const { colors } = useTheme();
  const iconSize = 18;
  const paddingLeft = Platform.OS === "android" ? 40 : 36;

  return (
    <View
      className={cn("w-full flex-row items-center rounded-full border px-3", className)}
      style={[{ backgroundColor: colors.card, borderColor: colors.border }, style]}
    >
      {/* Icon */}
      <Feather name="search" size={iconSize} color={colors.textSecondary} />

      {/* Input */}
      <TextInput
        data-slot="search"
        placeholder={placeholder}
        placeholderTextColor={colors.textMuted}
        cursorColor={colors.accent}
        selectionColor={colors.accent}
        autoCorrect={false}
        autoCapitalize="none"
        className={cn(
          "flex-1 text-sm font-medium",
          "py-2 min-h-[44px]",
          inputClassName
        )}
        style={{
          paddingLeft: 10,
          color: colors.textPrimary,
        }}
        {...props}
      />
    </View>
  );
}
