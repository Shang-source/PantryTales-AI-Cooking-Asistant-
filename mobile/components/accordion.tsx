"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  View,
  Text,
  TouchableOpacity,
  LayoutAnimation,
  Platform,
  UIManager,
} from "react-native";

// Enable LayoutAnimation on Android
if (
  Platform.OS === "android" &&
  UIManager.setLayoutAnimationEnabledExperimental
) {
  UIManager.setLayoutAnimationEnabledExperimental(true);
}

// Types

type AccordionType = "single" | "multiple";

interface AccordionProps {
  type?: AccordionType; // "single" | "multiple"
  collapsible?: boolean; // in "single" mode, allow all items to be collapsed
  defaultValue?: string | string[]; // initially opened item(s)
  value?: string | string[]; // controlled mode
  onValueChange?: (value: string | string[]) => void;
  className?: string;
  children: ReactNode;
}

interface AccordionItemProps {
  value: string;
  className?: string;
  children: ReactNode;
}

interface AccordionTriggerProps {
  className?: string;
  textClassName?: string;
  children: ReactNode;
}

interface AccordionContentProps {
  className?: string;
  children: ReactNode;
}

// Context

interface AccordionContextValue {
  type: AccordionType;
  collapsible: boolean;
  openValues: string[];
  toggleValue: (value: string) => void;
}

const AccordionContext = createContext<AccordionContextValue | null>(null);

function useAccordionContext() {
  const ctx = useContext(AccordionContext);
  if (!ctx) {
    throw new Error("Accordion components must be used within <Accordion />");
  }
  return ctx;
}

interface AccordionItemContextValue {
  value: string;
}

const AccordionItemContext = createContext<AccordionItemContextValue | null>(
  null
);

function useAccordionItemContext() {
  const ctx = useContext(AccordionItemContext);
  if (!ctx) {
    throw new Error(
      "AccordionTrigger and AccordionContent must be used within <AccordionItem />"
    );
  }
  return ctx;
}

// Accordion Root

export function Accordion({
  type = "single",
  collapsible = false,
  defaultValue,
  value,
  onValueChange,
  className,
  children,
}: AccordionProps) {
  const [internalOpen, setInternalOpen] = useState<string[]>(() => {
    if (value !== undefined) {
      return Array.isArray(value) ? value : [value];
    }
    if (defaultValue !== undefined) {
      return Array.isArray(defaultValue) ? defaultValue : [defaultValue];
    }
    return [];
  });

  const openValues = useMemo(() => {
    if (value !== undefined) {
      return Array.isArray(value) ? value : [value];
    }
    return internalOpen;
  }, [value, internalOpen]);

  const setOpenValues = useCallback((next: string[]) => {
    LayoutAnimation.configureNext(LayoutAnimation.Presets.easeInEaseOut);

    if (onValueChange) {
      if (type === "single") {
        onValueChange(next[0] ?? "");
      } else {
        onValueChange(next);
      }
    }

    if (value === undefined) {
      setInternalOpen(next);
    }
  }, [onValueChange, type, value]);

  const toggleValue = useCallback(
    (v: string) => {
      const isOpen = openValues.includes(v);

      if (type === "single") {
        if (isOpen) {
          if (collapsible) {
            // allow closing the currently open item
            setOpenValues([]);
          } else {
            // not collapsible: keep at least one item open
            return;
          }
        } else {
          // open the new item, close others
          setOpenValues([v]);
        }
      } else {
        // "multiple" mode: toggle items independently
        if (isOpen) {
          setOpenValues(openValues.filter((x) => x !== v));
        } else {
          setOpenValues([...openValues, v]);
        }
      }
    },
    [type, collapsible, openValues, setOpenValues]
  );

  const ctxValue = useMemo<AccordionContextValue>(
    () => ({
      type,
      collapsible,
      openValues,
      toggleValue,
    }),
    [type, collapsible, openValues, toggleValue]
  );

  return (
    <AccordionContext.Provider value={ctxValue}>
      <View className={className ?? ""}>{children}</View>
    </AccordionContext.Provider>
  );
}

// AccordionItem
export function AccordionItem({
  value,
  className,
  children,
}: AccordionItemProps) {
  return (
    <AccordionItemContext.Provider value={{ value }}>
      <View className={`border-b border-zinc-200 ${className ?? ""}`}>
        {children}
      </View>
    </AccordionItemContext.Provider>
  );
}

// AccordionTrigger

export function AccordionTrigger({
  className,
  textClassName,
  children,
}: AccordionTriggerProps) {
  const { openValues, toggleValue } = useAccordionContext();
  const { value } = useAccordionItemContext();
  const isOpen = openValues.includes(value);

  return (
    <View>
      <TouchableOpacity
        onPress={() => toggleValue(value)}
        activeOpacity={0.8}
        className={`flex flex-row items-center justify-between py-3 ${
          className ?? ""
        }`}
      >
        {/* Left side: label / custom content */}
        {typeof children === "string" ? (
          <Text
            className={`text-sm font-medium text-zinc-900 ${
              textClassName ?? ""
            }`}
          >
            {children}
          </Text>
        ) : (
          children
        )}

        {/* Right side: chevron indicator, rotated when open */}
        <Text
          className={`text-xs text-zinc-500 ${
            isOpen ? "rotate-180" : "rotate-0"
          }`}
        >
          ▼
        </Text>
      </TouchableOpacity>
    </View>
  );
}

// AccordionContent

export function AccordionContent({
  className,
  children,
}: AccordionContentProps) {
  const { openValues } = useAccordionContext();
  const { value } = useAccordionItemContext();
  const isOpen = openValues.includes(value);

  if (!isOpen) return null;

  return (
    <View className={className ?? ""}>
      <View className="pb-3">{children}</View>
    </View>
  );
}
