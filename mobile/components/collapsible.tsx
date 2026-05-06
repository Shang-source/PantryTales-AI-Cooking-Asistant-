import { cn } from "@/utils/cn";
import { useState, createContext, useContext } from "react";
import {
  View,
  Pressable,
  Platform,
  UIManager,
  LayoutAnimation,
  PressableProps,
  ViewProps,
} from "react-native";

if (
  Platform.OS === "android" &&
  UIManager.setLayoutAnimationEnabledExperimental
) {
  UIManager.setLayoutAnimationEnabledExperimental(true);
}

interface CollapsibleContextValue {
  open: boolean;
  toggle: () => void;
  disabled?: boolean;
}

const CollapsibleContext = createContext<CollapsibleContextValue | undefined>(
  undefined
);

interface CollapsibleProps extends ViewProps {
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
  disabled?: boolean;
  className?: string;
}

function Collapsible({
  children,
  open: controlledOpen,
  defaultOpen = false,
  onOpenChange,
  disabled = false,
  className,
  ...props
}: CollapsibleProps) {
  const [internalOpen, setInternalOpen] = useState(defaultOpen);

  const isControlled = controlledOpen !== undefined;
  const currentOpen = isControlled ? controlledOpen : internalOpen;

  const toggle = () => {
    if (disabled) return;

    const nextOpen = !currentOpen;

    LayoutAnimation.configureNext(LayoutAnimation.Presets.easeInEaseOut);

    if (isControlled) {
      onOpenChange?.(nextOpen);
    } else {
      setInternalOpen(nextOpen);
      onOpenChange?.(nextOpen);
    }
  };

  return (
    <CollapsibleContext.Provider
      value={{ open: currentOpen, toggle, disabled }}
    >
      <View className={cn(className)} {...props}>
        {children}
      </View>
    </CollapsibleContext.Provider>
  );
}

interface CollapsibleTriggerProps extends PressableProps {
  children: React.ReactNode;
  className?: string;
}

function CollapsibleTrigger({
  children,
  className,
  ...props
}: CollapsibleTriggerProps) {
  const context = useContext(CollapsibleContext);
  if (!context)
    throw new Error("CollapsibleTrigger must be used within Collapsible");

  return (
    <Pressable
      onPress={context.toggle}
      disabled={context.disabled}
      className={cn(className)}
      accessibilityRole="button"
      accessibilityState={{ expanded: context.open }}
      {...props}
    >
      {children}
    </Pressable>
  );
}

interface CollapsibleContentProps extends ViewProps {
  className?: string;
}

function CollapsibleContent({
  children,
  className,
  ...props
}: CollapsibleContentProps) {
  const context = useContext(CollapsibleContext);
  if (!context)
    throw new Error("CollapsibleContent must be used within Collapsible");

  if (!context.open) {
    return null;
  }

  return (
    <View className={cn("overflow-hidden", className)} {...props}>
      {children}
    </View>
  );
}

export { Collapsible, CollapsibleTrigger, CollapsibleContent };
