import { cn } from "@/utils/cn";
import { useEffect, useRef } from "react";
import { Pressable, Animated, Platform, ViewProps } from "react-native";

interface SwitchProps extends Omit<ViewProps, "style"> {
  checked?: boolean;
  onCheckedChange?: (checked: boolean) => void;
  disabled?: boolean;
  className?: string;
}

function Switch({
  checked = false,
  onCheckedChange,
  disabled = false,
  className,
  ...props
}: SwitchProps) {
  const animatedValue = useRef(new Animated.Value(checked ? 1 : 0)).current;

  useEffect(() => {
    Animated.timing(animatedValue, {
      toValue: checked ? 1 : 0,
      duration: 200,
      useNativeDriver: Platform.OS !== "web",
    }).start();
  }, [checked, animatedValue]);

  const thumbTranslate = animatedValue.interpolate({
    inputRange: [0, 1],
    outputRange: [2, 20],
  });

  const handlePress = () => {
    if (!disabled && onCheckedChange) {
      onCheckedChange(!checked);
    }
  };

  return (
    <Pressable
      onPress={handlePress}
      disabled={disabled}
      className={cn(
        "peer inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors",
        disabled && "opacity-50",
        checked ? "bg-[#18181B]" : "bg-[#E4E4E7]",
        className
      )}
      {...props}
    >
      <Animated.View
        className={cn(
          "pointer-events-none block h-5 w-5 rounded-full bg-white shadow-sm ring-0",
          "shadow-black/20"
        )}
        // for animation
        style={{
          transform: [{ translateX: thumbTranslate }],
        }}
      />
    </Pressable>
  );
}

export { Switch };
