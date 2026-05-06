"use client";

import { useState, type ReactNode } from "react";
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  type TouchableOpacityProps,
  type GestureResponderEvent,
} from "react-native";
import { cn } from "../utils/cn";

type ToggleVariant = "default" | "outline";
type ToggleSize = "default" | "sm" | "lg";

interface ToggleProps extends TouchableOpacityProps {
  variant?: ToggleVariant;
  size?: ToggleSize;

  // Controlled pressed state
  pressed?: boolean;

  // Uncontrolled initial pressed state
  defaultPressed?: boolean;

  // Callback when pressed state changes
  onPressedChange?: (pressed: boolean) => void;

  className?: string;
  textClassName?: string;
  children?: ReactNode;
}

export function Toggle({
  variant = "default",
  size = "default",
  pressed,
  defaultPressed = false,
  onPressedChange,
  disabled,
  className,
  textClassName,
  children,
  onPress,
  ...touchableProps
}: ToggleProps) {
  const [internalPressed, setInternalPressed] = useState<boolean>(
    pressed ?? defaultPressed
  );

  const isPressed = pressed !== undefined ? pressed : internalPressed;

  const handlePress = (event: GestureResponderEvent) => {
    if (disabled) return;

    const next = !isPressed;

    if (pressed === undefined) {
      setInternalPressed(next);
    }

    onPressedChange?.(next);
    onPress?.(event);
  };

  const baseClasses =
    "flex flex-row items-center justify-center rounded-md text-sm font-medium";
  const sizeClasses =
    size === "sm"
      ? "h-8 px-2 min-w-8"
      : size === "lg"
        ? "h-10 px-3 min-w-10"
        : "h-9 px-2.5 min-w-9";

  const variantBaseClasses =
    variant === "outline"
      ? "border border-input bg-transparent"
      : "bg-transparent";

  const pressedClasses = "bg-accent text-accent-foreground";
  const unpressedClasses =
    variant === "outline"
      ? "hover:bg-accent hover:text-accent-foreground"
      : "hover:bg-muted hover:text-muted-foreground";

  const disabledClasses = "opacity-50";

  const buttonClassName = cn(
    baseClasses,
    sizeClasses,
    variantBaseClasses,
    isPressed ? pressedClasses : unpressedClasses,
    disabled && disabledClasses,
    className
  );

  const labelClassName = cn("text-sm font-medium", textClassName);

  return (
    <View data-slot="toggle-wrapper">
      <TouchableOpacity
        data-slot="toggle"
        activeOpacity={0.8}
        disabled={disabled}
        onPress={handlePress}
        className={buttonClassName}
        {...touchableProps}
      >
        {typeof children === "string" ? (
          <Text className={labelClassName}>{children}</Text>
        ) : (
          children
        )}
      </TouchableOpacity>

      {/* Hidden TextInput for state/debug and to satisfy TextInput usage */}
      <TextInput
        editable={false}
        value={isPressed ? "on" : "off"}
        className="hidden"
      />
    </View>
  );
}
