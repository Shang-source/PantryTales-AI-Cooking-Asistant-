import { cn } from "@/utils/cn";
import React from "react";
import { PressableProps, Pressable, ViewProps, View } from "react-native";

//----------------context--------------------------
type HoverCardContextValue = {
  open: boolean;
  setOpen: (open: boolean) => void;
};

const HoverCardContext = React.createContext<HoverCardContextValue | undefined>(
  undefined,
);

function useHoverCardContext() {
  const ctx = React.useContext(HoverCardContext);
  if (!ctx) {
    throw new Error("HoverCard.* must be used inside <HoverCard>");
  }
  return ctx;
}

//-------------------------root---------------------------
//Root components---provide open status
type HoverCardProps = {
  defaultOpen?: boolean;
  children: React.ReactNode;
};

function HoverCard({
  defaultOpen = false,
  children,
  ...props
}: HoverCardProps) {
  const [open, setOpen] = React.useState(defaultOpen);

  return (
    <HoverCardContext.Provider value={{ open, setOpen }}>
      {children}
    </HoverCardContext.Provider>
  );
}

//------------------------triger----------------------
type HoverCardTriggerProps = PressableProps & {
  children: React.ReactNode;
};

function HoverCardTrigger({ children, ...props }: HoverCardTriggerProps) {
  const { setOpen } = useHoverCardContext();

  return (
    <Pressable
      data-slot="hover-card-trigger"
      {...props}
      onPressIn={(e) => {
        props.onPressIn?.(e);
        setOpen(true);
      }}
      onPressOut={(e) => {
        props.onPressOut?.(e);
        setOpen(false);
      }}
    >
      {children}
    </Pressable>
  );
}

//------------------content----------------------
type HoverCardContentProps = ViewProps & {
  align?: "left" | "center" | "right";
  sideOffset?: number;
  className?: string;
  children: React.ReactNode;
};

function HoverCardContent({
  align = "center",
  sideOffset = 4,
  className,
  style,
  children,
  ...props
}: HoverCardContentProps) {
  const { open } = useHoverCardContext();

  if (!open) return null;

  const alignClass =
    align === "left"
      ? "self-start"
      : align === "right"
        ? "self-end"
        : "self-center";

  return (
    <View
      data-slot="hover-card-content"
      className={cn(
        "bg-popover text-popover-foreground " +
          "data-[state=open]:animate-in data-[state=closed]:animate-out " +
          "data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 " +
          "data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 " +
          "data-[side=bottom]:slide-in-from-top-2 " +
          "data-[side=left]:slide-in-from-right-2 " +
          "data-[side=right]:slide-in-from-left-2 " +
          "data-[side=top]:slide-in-from-bottom-2 " +
          "z-50 w-64 origin-(--radix-hover-card-content-transform-origin) " +
          "rounded-md border p-4 shadow-md outline-hidden",
        alignClass,
        className,
      )}
      style={[{ marginTop: sideOffset }, style]}
      {...props}
    >
      {children}
    </View>
  );
}

export { HoverCard, HoverCardTrigger, HoverCardContent };
