import { View, Text, ViewProps, TextStyle } from "react-native";
import Icon from "react-native-vector-icons/Feather";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

interface StatCardProps extends ViewProps {
  value: number;
  label: string;
  className?: string;
  valueClassName?: string;
  labelClassName?: string;
  /** Override value text style */
  valueStyle?: TextStyle;
  /** Compact horizontal layout for side-by-side display */
  compact?: boolean;
  /** Icon name (Feather icons) for compact mode */
  icon?: string;
  /** Icon color for compact mode */
  iconColor?: string;
}

export default function StatCard({
  value,
  label,
  className = "",
  valueClassName = "",
  labelClassName = "",
  valueStyle,
  compact = false,
  icon,
  iconColor,
  ...props
}: StatCardProps) {
  const { colors } = useTheme();
  const resolvedIconColor = iconColor ?? colors.textMuted;

  if (compact) {
    return (
      <View
        className={cn("flex-row items-center justify-between", className)}
        {...props}
      >
        <View className="flex-row items-center gap-2">
          {icon && <Icon name={icon} size={14} color={resolvedIconColor} />}
          <Text
            className={cn("text-sm", labelClassName)}
            style={{ color: colors.textSecondary }}
            numberOfLines={1}
          >
            {label}
          </Text>
        </View>
        <Text
          className={cn("text-base font-semibold", valueClassName)}
          style={[{ color: resolvedIconColor }, valueStyle]}
          numberOfLines={1}
        >
          {value}
        </Text>
      </View>
    );
  }

  return (
    <View
      className={cn("rounded-xl p-3 items-center justify-center", className)}
      {...props}
    >
      <Text
        className={cn("text-xl font-semibold", valueClassName)}
        style={[{ color: colors.accent }, valueStyle]}
        numberOfLines={1}
        adjustsFontSizeToFit
      >
        {value}
      </Text>
      <Text
        className={cn("text-sm", labelClassName)}
        style={{ color: colors.textMuted }}
        numberOfLines={1}
        adjustsFontSizeToFit
      >
        {label}
      </Text>
    </View>
  );
}
