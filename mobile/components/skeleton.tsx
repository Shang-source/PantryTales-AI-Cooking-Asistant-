import { ViewProps, View, StyleProp, ViewStyle } from "react-native";
import { useTheme } from "@/contexts/ThemeContext";

interface SkeletonProps extends Omit<ViewProps, "style"> {
  className?: string;
  style?: StyleProp<ViewStyle>;
}

export function Skeleton({ className, style, ...props }: SkeletonProps) {
  const { colors } = useTheme();
  return (
    <View
      className={`${className} mb-2 opacity-70`}
      style={[{ backgroundColor: colors.card }, style]}
      {...props}
    />
  );
}