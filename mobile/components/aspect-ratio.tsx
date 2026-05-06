"use client";

import { View, type ViewProps } from "react-native";
import type { ReactNode } from "react";
import { cn } from "../utils/cn";

export interface AspectRatioProps extends ViewProps {
  ratio?: number;
  className?: string;
  children?: ReactNode;
}

export function AspectRatio({
  ratio = 16 / 9,
  className,
  children,
  style,
  ...rest
}: AspectRatioProps) {
  return (
    <View
      data-slot="aspect-ratio"
      className={cn("w-full", className)}
      style={[{ aspectRatio: ratio }, style]}
      {...rest}
    >
      {children}
    </View>
  );
}

export default AspectRatio;
