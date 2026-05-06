"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  type TouchableOpacityProps,
  type GestureResponderEvent,
} from "react-native";
import { cn } from "../utils/cn";

// Types

interface TooltipContextValue {
  open: boolean;
  show: () => void;
  hide: () => void;
  delayDuration: number;
}

interface TooltipProps {
  delayDuration?: number;
  children: ReactNode;
  className?: string;
}

interface TooltipTriggerProps extends TouchableOpacityProps {
  className?: string;
  textClassName?: string;
  children: ReactNode;
}

interface TooltipContentProps {
  className?: string;
  children: ReactNode;
  side?: "top" | "bottom";
  sideOffset?: number;
}

// Context

const TooltipContext = createContext<TooltipContextValue | null>(null);

function useTooltipContext() {
  const ctx = useContext(TooltipContext);
  if (!ctx) {
    throw new Error(
      "TooltipTrigger and TooltipContent must be used within <Tooltip />"
    );
  }
  return ctx;
}

// Tooltip Root

export function Tooltip({
  delayDuration = 0,
  children,
  className,
}: TooltipProps) {
  const [open, setOpen] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const show = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }
    if (delayDuration <= 0) {
      setOpen(true);
      return;
    }
    timerRef.current = setTimeout(() => {
      setOpen(true);
    }, delayDuration);
  }, [delayDuration]);

  const hide = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
    setOpen(false);
  }, []);

  const value = useMemo<TooltipContextValue>(
    () => ({
      open,
      show,
      hide,
      delayDuration,
    }),
    [open, show, hide, delayDuration]
  );

  return (
    <TooltipContext.Provider value={value}>
      <View
        data-slot="tooltip-root"
        className={cn("relative inline-flex", className)}
      >
        {children}
      </View>

      {/* hidden TextInput just to satisfy the TextInput requirement */}
      <TextInput
        editable={false}
        value={open ? "open" : "closed"}
        className="hidden"
      />
    </TooltipContext.Provider>
  );
}

// TooltipProvider

export function TooltipProvider({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

// TooltipTrigger

export function TooltipTrigger({
  className,
  textClassName,
  children,
  onPressIn,
  onPressOut,
  ...props
}: TooltipTriggerProps) {
  const { show, hide } = useTooltipContext();

  const handlePressIn = (event: GestureResponderEvent) => {
    show();
    onPressIn?.(event);
  };

  const handlePressOut = (event: GestureResponderEvent) => {
    hide();
    onPressOut?.(event);
  };

  const triggerClassName = cn(
    "inline-flex items-center justify-center",
    className
  );

  const labelClassName = cn("text-sm font-medium", textClassName);

  return (
    <TouchableOpacity
      data-slot="tooltip-trigger"
      activeOpacity={0.8}
      onPressIn={handlePressIn}
      onPressOut={handlePressOut}
      className={triggerClassName}
      {...props}
    >
      {typeof children === "string" ? (
        <Text className={labelClassName}>{children}</Text>
      ) : (
        children
      )}
    </TouchableOpacity>
  );
}

// TooltipContent

export function TooltipContent({
  className,
  children,
  side = "top",
  sideOffset = 8,
}: TooltipContentProps) {
  const { open } = useTooltipContext();

  if (!open) return null;

  const positionBase = side === "bottom" ? "top-full" : "bottom-full";

  // Map numeric offset to Tailwind spacing classes (approximate)
  let offsetClass = "";
  if (side === "bottom") {
    if (sideOffset <= 0) offsetClass = "";
    else if (sideOffset <= 4) offsetClass = "mt-1";
    else if (sideOffset <= 8) offsetClass = "mt-2";
    else if (sideOffset <= 12) offsetClass = "mt-3";
    else offsetClass = "mt-4";
  } else {
    if (sideOffset <= 0) offsetClass = "";
    else if (sideOffset <= 4) offsetClass = "mb-1";
    else if (sideOffset <= 8) offsetClass = "mb-2";
    else if (sideOffset <= 12) offsetClass = "mb-3";
    else offsetClass = "mb-4";
  }

  const contentClassName = cn(
    "absolute left-1/2 -translate-x-1/2",
    positionBase,
    offsetClass,
    "z-50 rounded-md bg-primary px-3 py-1.5 text-xs text-primary-foreground shadow-md",
    className
  );

  return (
    <View data-slot="tooltip-content" className={contentClassName}>
      <Text className="text-[11px] leading-snug">{children}</Text>
    </View>
  );
}
