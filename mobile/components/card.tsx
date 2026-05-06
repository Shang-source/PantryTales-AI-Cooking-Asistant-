import { TouchableOpacity, View, ViewProps, Text, TextProps } from "react-native";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

type CardProps = ViewProps & {
  onPress?: () => void;
  className?: string;
};

const Card = ({ onPress, className, children, style, ...props }: CardProps) => {
  const { colors } = useTheme();
  const Component = onPress ? TouchableOpacity : View;

  return (
    <Component
      {...(onPress && { onPress, activeOpacity: 0.8 })}
      className={cn(
        "flex rounded-xl p-4 mb-3 mx-4",
        className
      )}
      style={[{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }, style]}
      {...props as any}
    >
      {children}
    </Component>
  );
};
export default Card;

const CardHeader = ({ className, style, ...props }: ViewProps) => {
  const { colors } = useTheme();
  return (
    <View className={cn("flex flex-row", className)} style={[{ backgroundColor: colors.card }, style]} {...props} />
  );
};
CardHeader.displayName = "CardHeader";

const CardTitle = ({ className, style, ...props }: TextProps) => {
  const { colors } = useTheme();
  return (
    <Text
      className={cn("font-semibold text-lg mb-3", className)}
      style={[{ color: colors.textPrimary }, style]}
      {...props}
    />
  );
};
CardTitle.displayName = "CardTitle";

const CardDescription = ({ className, style, ...props }: TextProps) => {
  const { colors } = useTheme();
  return (
    <Text
      className={cn("text-sm", className)}
      style={[{ color: colors.textSecondary }, style]}
      {...props}
    />
  );
};
CardDescription.displayName = "CardDescription";

const CardContent = ({ className, ...props }: ViewProps) => (
  <View className={cn("", className)} {...props} />
);
CardContent.displayName = "CardContent";

const CardFooter = ({ className, ...props }: ViewProps) => (
  <View className={cn("flex flex-row", className)} {...props} />
);
CardFooter.displayName = "CardFooter";

const CardAction = ({ className, ...props }: ViewProps) => (
  <View className={cn("absolute right-2 top-2", className)} {...props} />
);
CardAction.displayName = "CardAction";

export {
  Card,
  CardHeader,
  CardFooter,
  CardTitle,
  CardDescription,
  CardContent,
  CardAction,
};
