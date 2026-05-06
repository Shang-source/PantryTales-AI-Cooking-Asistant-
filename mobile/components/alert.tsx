"use client";

import { View, Text, type ViewProps, type TextProps } from "react-native";
import { cn } from "../utils/cn";

export type AlertVariant = "default" | "destructive";

export interface AlertProps extends ViewProps {
  variant?: AlertVariant;
  className?: string;
}

export interface AlertTitleProps extends TextProps {
  className?: string;
}

export interface AlertDescriptionProps extends TextProps {
  className?: string;
}

function Alert({ className, variant = "default", ...props }: AlertProps) {
  const baseClasses = "relative w-full rounded-lg border px-4 py-3";

  const variantClasses =
    variant === "destructive"
      ? "bg-red-50 border-red-200 text-red-700"
      : "bg-white border-zinc-200 text-zinc-900";

  return (
    <View
      data-slot="alert"
      className={cn(baseClasses, variantClasses, className)}
      {...props}
    />
  );
}

function AlertTitle({ className, ...props }: AlertTitleProps) {
  return (
    <Text
      data-slot="alert-title"
      className={cn("text-sm font-medium tracking-tight", className)}
      {...props}
    />
  );
}

function AlertDescription({ className, ...props }: AlertDescriptionProps) {
  return (
    <Text
      data-slot="alert-description"
      className={cn("mt-1 text-xs leading-relaxed text-zinc-600", className)}
      {...props}
    />
  );
}

export { Alert, AlertTitle, AlertDescription };
