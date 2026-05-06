import { Text, TouchableOpacity, StyleProp, TextStyle } from "react-native";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

const buttonVariants = cva("flex-row items-center justify-center gap-2", {
  variants: {
    variant: {
      default: "rounded-xl",
      shortcut: "rounded-xl",
      destructive: "bg-[#ef4444]", // destructive
      outline: "", // background + ring(alpha)
      secondary: "rounded-2xl px-2", // secondary
      ghost: "bg-transparent",
      link: "bg-transparent",
    },
    size: {
      default: "h-10",
      sm: "h-10",
      lg: "h-10 px-6",
      icon: "h-9 w-9 p-0",
    },
  },
  defaultVariants: {
    variant: "default",
    size: "default",
  },
});

const buttonTextVariants = cva("font-medium text-center", {
  variants: {
    variant: {
      default: "", // Will use theme color
      shortcut: "", // Will use theme color
      destructive: "", // Will use white
      outline: "", // Will use theme color
      secondary: "", // Will use theme color
      ghost: "", // Will use theme color
      link: "underline", // Will use theme color
    },
    size: {
      default: "text-base",
      sm: "text-sm",
      lg: "text-lg",
      icon: "",
    },
  },
  defaultVariants: {
    variant: "default",
    size: "default",
  },
});

interface ButtonProps
  extends
    React.ComponentPropsWithoutRef<typeof TouchableOpacity>,
    VariantProps<typeof buttonVariants> {
  children: React.ReactNode;
  textClassName?: string;
  textStyle?: StyleProp<TextStyle>;
}

const Button = ({
  className,
  textClassName,
  textStyle,
  variant,
  size,
  children,
  disabled,
  style,
  ...props
}: ButtonProps) => {
  const { colors } = useTheme();
  let activeOpacity = 0.8;
  if (variant === "ghost" || variant === "link") activeOpacity = 0.7;

  // Determine background and border colors based on variant
  const getVariantStyles = () => {
    switch (variant) {
      case "default":
        return {
          backgroundColor: colors.card,
          borderWidth: 1,
          borderColor: `${colors.accent}50`,
        };
      case "shortcut":
        return {
          backgroundColor: colors.card,
          borderWidth: 1,
          borderColor: `${colors.accentMuted}66`,
        };
      case "outline":
        return {
          backgroundColor: colors.card,
          borderWidth: 1,
          borderColor: colors.border,
        };
      case "secondary":
        return { backgroundColor: colors.card };
      case "destructive":
      case "ghost":
      case "link":
      default:
        return {};
    }
  };

  // Determine text color based on variant
  const getTextColor = () => {
    switch (variant) {
      case "destructive":
        return "#ffffff";
      case "outline":
        return colors.textPrimary;
      default:
        return colors.textPrimary;
    }
  };

  return (
    <TouchableOpacity
      activeOpacity={activeOpacity}
      disabled={disabled}
      className={cn(
        buttonVariants({ variant, size, className }),
        disabled && "opacity-50"
      )}
      style={[getVariantStyles(), style]}
      {...props}
    >
      {typeof children === "string" ? (
        <Text
          className={cn(buttonTextVariants({ variant, size }), textClassName)}
          style={[{ color: getTextColor() }, textStyle]}
        >
          {children}
        </Text>
      ) : (
        children
      )}
    </TouchableOpacity>
  );
};
Button.displayName = "Button";

export { Button, buttonVariants, buttonTextVariants };
