"use client";

import { View, Text, type ViewProps } from "react-native";
import { Children, type ReactNode } from "react";
import { cn } from "../utils/cn";

// Variants consistent with the original cva-based badge
export type BadgeVariant = "default" | "secondary" | "destructive" | "outline";

export interface BadgeProps extends ViewProps {
  variant?: BadgeVariant;
  className?: string;
  textClassName?: string;
  children?: ReactNode;
}

export function badgeVariants(variant: BadgeVariant = "default") {
  const base =
    "flex flex-row items-center justify-center rounded-md border px-2 py-0.5 text-xs font-medium w-fit whitespace-nowrap shrink-0 gap-1 overflow-hidden";

  const map: Record<BadgeVariant, string> = {
    default: "border-transparent bg-zinc-900 text-zinc-50",
    secondary: "border-transparent bg-zinc-100 text-zinc-900",
    destructive: "border-transparent bg-red-600 text-white",
    outline: "border-zinc-300 bg-transparent text-zinc-900",
  };

  return cn(base, map[variant]);
}

export function Badge({
  className,
  textClassName,
  variant = "default",
  children,
  ...props
}: BadgeProps) {
  const classes = badgeVariants(variant);

  const normalizedChildren = (() => {
    const nodes = Children.toArray(children);
    const output: ReactNode[] = [];
    let textBuffer: (string | number)[] = [];

    const flushText = () => {
      if (!textBuffer.length) return;
      output.push(
        <Text
          key={`text-${output.length}`}
          className={cn("text-xs font-medium", textClassName)}
        >
          {textBuffer.join("")}
        </Text>
      );
      textBuffer = [];
    };

    nodes.forEach((child) => {
      if (typeof child === "string" || typeof child === "number") {
        textBuffer.push(child);
        return;
      }
      flushText();
      output.push(child);
    });

    flushText();

    return output;
  })();

  return (
    <View data-slot="badge" className={cn(classes, className)} {...props}>
      {normalizedChildren}
    </View>
  );
}

export default Badge;
