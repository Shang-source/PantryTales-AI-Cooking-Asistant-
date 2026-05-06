"use client";
import { useState } from "react";
import {
  View,
  Image,
  Text,
  type ViewProps,
  type ImageProps,
} from "react-native";
import type { ReactNode } from "react";
import { cn } from "../utils/cn";

export interface AvatarProps extends ViewProps {
  className?: string;
  children?: ReactNode;
}

function Avatar({ className, children, ...props }: AvatarProps) {
  return (
    <View
      data-slot="avatar"
      className={cn(
        "relative h-10 w-10 shrink-0 overflow-hidden rounded-full",
        className
      )}
      {...props}
    >
      {children}
    </View>
  );
}

export interface AvatarImageProps extends ImageProps {
  className?: string;
}

function AvatarImage({ className, ...props }: AvatarImageProps) {
  const [hasError, setHasError] = useState(false);
  if (hasError) {
    return null;
  }
  return (
    <Image
      data-slot="avatar-image"
      className={cn("h-full w-full", className)}
      onError={() => setHasError(true)}
      {...props}
    />
  );
}

export interface AvatarFallbackProps extends ViewProps {
  className?: string;
  children?: ReactNode;
  textClassName?: string;
}

function AvatarFallback({
  className,
  children,
  textClassName,
  ...props
}: AvatarFallbackProps) {
  return (
    <View
      data-slot="avatar-fallback"
      className={cn(
        "h-full w-full items-center justify-center rounded-full bg-zinc-200",
        className
      )}
      {...props}
    >
      {typeof children === "string" ? (
        <Text
          className={cn("text-sm font-semibold text-zinc-700", textClassName)}
        >
          {children}
        </Text>
      ) : (
        children
      )}
    </View>
  );
}

export { Avatar, AvatarImage, AvatarFallback };
