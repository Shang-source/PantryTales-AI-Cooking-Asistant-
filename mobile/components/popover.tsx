"use client";

import {
  createContext,
  useContext,
  useState,
  type ReactNode,
  useCallback,
} from "react";
import { View, Text, TouchableOpacity } from "react-native";
import { cn } from "../utils/cn";

// ---------------------------------------------
// Types
// ---------------------------------------------

interface PopoverContextValue {
  open: boolean;
  setOpen: (value: boolean) => void;
}

interface PopoverProps {
  open?: boolean; // controlled mode (optional)
  defaultOpen?: boolean; // uncontrolled initial value
  onOpenChange?: (open: boolean) => void;
  children: ReactNode;
}

interface PopoverTriggerProps {
  className?: string;
  children: ReactNode;
}

interface PopoverContentProps {
  className?: string;
  overlayClassName?: string;
  children: ReactNode;
}

// ---------------------------------------------
// Context
// ---------------------------------------------

const PopoverContext = createContext<PopoverContextValue | null>(null);

function usePopoverContext() {
  const ctx = useContext(PopoverContext);
  if (!ctx) {
    throw new Error("Popover components must be used within <Popover />");
  }
  return ctx;
}

// ---------------------------------------------
// Popover Root
// ---------------------------------------------

export function Popover({
  open,
  defaultOpen = false,
  onOpenChange,
  children,
}: PopoverProps) {
  const [internalOpen, setInternalOpen] = useState(defaultOpen);

  const isControlled = open !== undefined;
  const actualOpen = isControlled ? open : internalOpen;

  const setOpen = useCallback(
    (next: boolean) => {
      if (!isControlled) {
        setInternalOpen(next);
      }
      onOpenChange?.(next);
    },
    [isControlled, onOpenChange]
  );

  return (
    <PopoverContext.Provider value={{ open: actualOpen, setOpen }}>
      {/* 
        This wrapper View is important: 
        it is the relative positioning container for the PopoverContent.
      */}
      <View className="relative">{children}</View>
    </PopoverContext.Provider>
  );
}

// ---------------------------------------------
// Popover Trigger
// ---------------------------------------------

export function PopoverTrigger({ className, children }: PopoverTriggerProps) {
  const { open, setOpen } = usePopoverContext();

  return (
    <TouchableOpacity
      activeOpacity={0.8}
      onPress={() => setOpen(!open)}
      className={cn(className)}
    >
      {children}
    </TouchableOpacity>
  );
}

// ---------------------------------------------
// Popover Content
// ---------------------------------------------

export function PopoverContent({
  className,
  overlayClassName,
  children,
}: PopoverContentProps) {
  const { open, setOpen } = usePopoverContext();

  if (!open) return null;

  return (
    <>
      {/* Optional dimmed overlay behind the popover */}
      <TouchableOpacity
        activeOpacity={1}
        onPress={() => setOpen(false)}
        className={cn(
          "absolute inset-0 bg-black/20",
          // overlay above other content
          "z-40",
          overlayClassName
        )}
      />

      {/* Actual popover panel */}
      <View
        className={cn(
          // basic positioning: below and centered relative to trigger wrapper
          "absolute z-50 mt-2 right-0",
          "min-w-[180px] rounded-md border border-zinc-200 bg-white px-3 py-2 shadow-lg",
          "dark:bg-zinc-900 dark:border-zinc-700",
          className
        )}
      >
        {typeof children === "string" ? (
          <Text className="text-sm text-zinc-900 dark:text-zinc-100">
            {children}
          </Text>
        ) : (
          children
        )}
      </View>
    </>
  );
}
