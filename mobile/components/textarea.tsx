"use client";

import { forwardRef, useState } from "react";
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  type TextInputProps,
} from "react-native";
import { cn } from "../utils/cn";

export interface TextareaProps extends TextInputProps {
  className?: string;
  containerClassName?: string;
  label?: string;
  helperText?: string;
  errorText?: string;
  showClearButton?: boolean;
}

export const Textarea = forwardRef<TextInput, TextareaProps>(
  (
    {
      className,
      containerClassName,
      label,
      helperText,
      errorText,
      showClearButton = false,
      editable = true,
      onChangeText,
      value,
      defaultValue,
      ...props
    },
    ref
  ) => {
    // Controlled + uncontrolled mode support
    const [innerValue, setInnerValue] = useState(
      (value as string | undefined) ??
        (defaultValue as string | undefined) ??
        ""
    );

    const isControlled = value !== undefined && value !== null;
    const currentValue = isControlled ? (value as string) : innerValue;

    const hasError = !!errorText;

    const handleChangeText = (text: string) => {
      if (!isControlled) {
        setInnerValue(text);
      }
      onChangeText?.(text);
    };

    const handleClear = () => {
      handleChangeText("");
    };

    return (
      <View className={cn("w-full", containerClassName)}>
        {/* Label */}
        {label && (
          <Text className="mb-1 text-sm font-medium text-foreground">
            {label}
          </Text>
        )}

        {/* Outer container: borders, background, focus styles */}
        <View
          className={cn(
            "flex w-full min-h-16 rounded-md border px-3 py-2",
            "bg-input-background border-input",
            "focus-within:border-ring focus-within:ring-2 focus-within:ring-ring/50",
            hasError && "border-destructive focus-within:ring-destructive/40",
            !editable && "opacity-50"
          )}
        >
          {/* Textarea input */}
          <TextInput
            ref={ref}
            multiline
            textAlignVertical="top"
            className={cn(
              "flex-1 min-h-16 text-base leading-relaxed md:text-sm",
              "text-foreground placeholder:text-muted-foreground",
              "web:outline-none",
              className
            )}
            editable={editable}
            value={currentValue}
            onChangeText={handleChangeText}
            {...props}
          />

          {showClearButton && !!currentValue && editable && (
            <TouchableOpacity
              onPress={handleClear}
              className="ml-2 self-start rounded-full px-2 py-1"
              accessibilityRole="button"
              accessibilityLabel="Clear text"
            >
              <Text className="text-xs text-muted-foreground">Clear</Text>
            </TouchableOpacity>
          )}
        </View>

        {hasError ? (
          <Text className="mt-1 text-xs text-destructive">{errorText}</Text>
        ) : helperText ? (
          <Text className="mt-1 text-xs text-muted-foreground">
            {helperText}
          </Text>
        ) : null}
      </View>
    );
  }
);

Textarea.displayName = "Textarea";
