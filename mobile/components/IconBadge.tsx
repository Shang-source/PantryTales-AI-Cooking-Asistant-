import { cn } from "@/utils/cn";
import { Text, TextProps, View } from "react-native";
import { Ionicons, MaterialCommunityIcons } from "@expo/vector-icons";
import { useTheme } from "@/contexts/ThemeContext";

type IconSetName = "Ionicons" | "MaterialCommunityIcons";

type LabelProps = TextProps & {
  className?: string;
  textClassName?: string;
  iconSet: IconSetName;
  iconName: string;
  iconColor?: string;
  iconSize?: number;
};

const renderIcon = (
  iconSet: IconSetName,
  name: string,
  color: string,
  size: number
) => {
  switch (iconSet) {
    case "Ionicons":
      return (
        <Ionicons
          name={name as keyof typeof Ionicons.glyphMap}
          size={size}
          color={color}
        />
      );
    case "MaterialCommunityIcons":
      return (
        <MaterialCommunityIcons
          name={name as keyof typeof MaterialCommunityIcons.glyphMap}
          size={size}
          color={color}
        />
      );
    default:
      return null;
  }
};

function IconBadge({
  className,
  textClassName,
  iconSet,
  iconName,
  iconColor,
  iconSize = 16,
  children,
  style,
  ...props
}: LabelProps) {
  const { colors } = useTheme();
  const resolvedIconColor = iconColor ?? colors.textPrimary;

  return (
    <View className={cn("flex-row items-center", className)}>
      {renderIcon(iconSet, iconName, resolvedIconColor, iconSize)}
      <Text
        data-slot="label"
        className={cn("text-xs ml-1.5", textClassName)}
        style={[{ color: colors.textPrimary }, style]}
        {...props}
      >
        {children}
      </Text>
    </View>
  );
}

export { IconBadge };
