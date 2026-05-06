import React from "react";
import { View, StyleProp, ViewStyle } from "react-native";
import { cn } from "../utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

export interface ProgressProps {
  value?: number; // 0–100
  className?: string; // Tailwind / Nativewind classes
  trackClassName?: string;
  indicatorClassName?: string;
  indicatorStyle?: StyleProp<ViewStyle>;
}

export function Progress({
  value = 0,
  className,
  trackClassName,
  indicatorClassName,
  indicatorStyle,
}: ProgressProps) {
  const { colors } = useTheme();
  // Clamp value
  const progress = Math.min(100, Math.max(0, value));

  return (
    <View
      className={cn(
        "h-2 w-full rounded-full overflow-hidden",
        className,
        trackClassName
      )}
      style={{ backgroundColor: colors.border }}
    >
      <View
        className={cn("h-full rounded-full", indicatorClassName)}
        style={[{ width: `${progress}%`, backgroundColor: colors.accent }, indicatorStyle]}
      />
    </View>
  );
}
