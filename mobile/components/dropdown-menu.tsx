// mobile/components/dropdown-menu.tsx
import React from "react";
import {
  View,
  Pressable,
  Text,
  ViewProps,
  PressableProps,
  TextProps,
  Modal,
  LayoutRectangle,
  Platform,
  ScrollView,
} from "react-native";
import { CheckIcon, ChevronRightIcon, CircleIcon } from "lucide-react-native";

import { cn } from "../utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

// ---------------------- shared types -------------------------

const ANDROID_DROPDOWN_OFFSET = -38;

interface ViewWithClassNameProps extends ViewProps {
  className?: string;
}

interface PressableWithClassNameProps extends PressableProps {
  className?: string;
}

interface TextWithClassNameProps extends TextProps {
  className?: string;
}

// ---------------------- context & root -----------------------

type DropdownMenuContextValue = {
  open: boolean;
  setOpen: React.Dispatch<React.SetStateAction<boolean>>;
  triggerLayout: LayoutRectangle | null;
  setTriggerLayout: React.Dispatch<React.SetStateAction<LayoutRectangle | null>>;
};

const DropdownMenuContext = React.createContext<
  DropdownMenuContextValue | undefined
>(undefined);

function useDropdownMenuContext() {
  const ctx = React.useContext(DropdownMenuContext);
  if (!ctx) {
    throw new Error("DropdownMenu.* must be used inside <DropdownMenu>");
  }
  return ctx;
}

type DropdownMenuProps = {
  defaultOpen?: boolean;
  children: React.ReactNode;
};

function DropdownMenu({ defaultOpen = false, children }: DropdownMenuProps) {
  const [open, setOpen] = React.useState(defaultOpen);
  const [triggerLayout, setTriggerLayout] =
    React.useState<LayoutRectangle | null>(null);

  return (
    <DropdownMenuContext.Provider
      value={{ open, setOpen, triggerLayout, setTriggerLayout }}
    >
      {children}
    </DropdownMenuContext.Provider>
  );
}

// RN does not need a real portal; keep API compatible
type DropdownMenuPortalProps = {
  children: React.ReactNode;
};

function DropdownMenuPortal({ children }: DropdownMenuPortalProps) {
  return <>{children}</>;
}

// ---------------------- trigger ------------------------------

type DropdownMenuTriggerProps = PressableWithClassNameProps & {
  children: React.ReactNode;
};

function DropdownMenuTrigger({
  children,
  onPress,
  ...props
}: DropdownMenuTriggerProps) {
  const { setOpen, setTriggerLayout } = useDropdownMenuContext();
  const triggerRef = React.useRef<View>(null);

  return (
    <Pressable
      ref={triggerRef}
      data-slot="dropdown-menu-trigger"
      {...props}
      onPress={(e) => {
        onPress?.(e);
        triggerRef.current?.measureInWindow((x, y, width, height) => {
          setTriggerLayout({ x, y, width, height });
          setOpen((prev) => !prev);
        });
      }}
    >
      {children}
    </Pressable>
  );
}

// ---------------------- content ------------------------------

type DropdownMenuContentProps = ViewWithClassNameProps & {
  align?: "left" | "center" | "right";
  sideOffset?: number;
  children: React.ReactNode;
};

function DropdownMenuContent({
  className,
  style,
  align = "center",
  sideOffset = 4,
  children,
  ...props
}: DropdownMenuContentProps) {
  const { open, triggerLayout, setOpen } = useDropdownMenuContext();
  const { colors } = useTheme();
  if (!open) return null;

  const androidOffset =
    Platform.OS === "android" ? ANDROID_DROPDOWN_OFFSET : 0;
  const baseTop =
    (triggerLayout?.y ?? 0) +
    (triggerLayout?.height ?? 0) +
    sideOffset +
    androidOffset;
  const baseLeft = triggerLayout?.x ?? 0;

  return (
    <DropdownMenuPortal>
      <Modal
        transparent
        visible
        animationType="fade"
        onRequestClose={() => setOpen(false)}
      >
        <Pressable
          style={{ flex: 1 }}
          onPress={() => setOpen(false)}
          accessibilityRole="button"
        >
          <View
            data-slot="dropdown-menu-content"
            className={cn(
              "rounded-md p-1 shadow-lg",
              className,
            )}
            style={[
              {
                position: "absolute",
                top: baseTop,
                left: baseLeft,
                minWidth: triggerLayout?.width ?? 160,
                elevation: 12,
                backgroundColor: colors.card,
                borderWidth: 1,
                borderColor: colors.border,
              },
              style,
            ]}
            {...props}
          >
            <ScrollView
              showsVerticalScrollIndicator={false}
              showsHorizontalScrollIndicator={false}
            >
              {children}
            </ScrollView>
          </View>
        </Pressable>
      </Modal>
    </DropdownMenuPortal>
  );
}

// ---------------------- group / label / separator ------------

type DropdownMenuGroupProps = ViewWithClassNameProps & {
  children: React.ReactNode;
};

function DropdownMenuGroup({
  className,
  children,
  ...props
}: DropdownMenuGroupProps) {
  return (
    <View data-slot="dropdown-menu-group" className={className} {...props}>
      {children}
    </View>
  );
}

type DropdownMenuLabelProps = TextWithClassNameProps & {
  inset?: boolean;
};

function DropdownMenuLabel({
  className,
  inset,
  children,
  ...props
}: DropdownMenuLabelProps) {
  return (
    <Text
      data-slot="dropdown-menu-label"
      data-inset={inset}
      className={cn(
        "px-2 py-1.5 text-sm font-medium data-[inset]:pl-8",
        className,
      )}
      {...props}
    >
      {children}
    </Text>
  );
}

type DropdownMenuSeparatorProps = ViewWithClassNameProps;

function DropdownMenuSeparator({
  className,
  ...props
}: DropdownMenuSeparatorProps) {
  return (
    <View
      data-slot="dropdown-menu-separator"
      className={cn("bg-border -mx-1 my-1 h-px", className)}
      {...props}
    />
  );
}

// ---------------------- item & shortcut ----------------------

type DropdownMenuItemProps = PressableWithClassNameProps & {
  inset?: boolean;
  variant?: "default" | "destructive";
  children: React.ReactNode;
};

function DropdownMenuItem({
  className,
  inset,
  variant = "default",
  children,
  onPress,
  ...props
}: DropdownMenuItemProps) {
  const { setOpen } = useDropdownMenuContext();

  return (
    <Pressable
      data-slot="dropdown-menu-item"
      data-inset={inset}
      data-variant={variant}
      className={cn(
        "focus:bg-accent focus:text-accent-foreground data-[variant=destructive]:text-destructive data-[variant=destructive]:focus:bg-destructive/10 dark:data-[variant=destructive]:focus:bg-destructive/20 data-[variant=destructive]:focus:text-destructive data-[variant=destructive]:*:[svg]:!text-destructive [&_svg:not([class*='text-'])]:text-muted-foreground relative flex cursor-default items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden select-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50 data-[inset]:pl-8 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
        className
      )}
      onPress={(e) => {
        onPress?.(e);
        setOpen(false);
      }}
      {...props}
    >
      {children}
    </Pressable>
  );
}

type DropdownMenuShortcutProps = TextWithClassNameProps;

function DropdownMenuShortcut({
  className,
  children,
  ...props
}: DropdownMenuShortcutProps) {
  return (
    <Text
      data-slot="dropdown-menu-shortcut"
      className={cn(
        "text-muted-foreground ml-auto text-xs tracking-widest",
        className,
      )}
      {...props}
    >
      {children}
    </Text>
  );
}

// ---------------------- checkbox / radio ---------------------

type DropdownMenuCheckboxItemProps = PressableWithClassNameProps & {
  checked?: boolean;
  children: React.ReactNode;
};

function DropdownMenuCheckboxItem({
  className,
  checked,
  children,
  ...props
}: DropdownMenuCheckboxItemProps) {
  return (
    <Pressable
      data-slot="dropdown-menu-checkbox-item"
      className={cn(
        "focus:bg-accent focus:text-accent-foreground relative flex cursor-default items-center gap-2 rounded-sm py-1.5 pr-2 pl-8 text-sm outline-hidden select-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
        className,
      )}
      {...props}
    >
      <View className="pointer-events-none absolute left-2 flex size-3.5 items-center justify-center">
        {checked ? <CheckIcon className="size-4" /> : null}
      </View>
      {children}
    </Pressable>
  );
}

type DropdownMenuRadioGroupProps = ViewWithClassNameProps & {
  children: React.ReactNode;
};

function DropdownMenuRadioGroup({
  className,
  children,
  ...props
}: DropdownMenuRadioGroupProps) {
  return (
    <View
      data-slot="dropdown-menu-radio-group"
      className={className}
      {...props}
    >
      {children}
    </View>
  );
}

type DropdownMenuRadioItemProps = PressableWithClassNameProps & {
  children: React.ReactNode;
};

function DropdownMenuRadioItem({
  className,
  children,
  ...props
}: DropdownMenuRadioItemProps) {
  return (
    <Pressable
      data-slot="dropdown-menu-radio-item"
      className={cn(
        "focus:bg-accent focus:text-accent-foreground relative flex cursor-default items-center gap-2 rounded-sm py-1.5 pr-2 pl-8 text-sm outline-hidden select-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
        className,
      )}
      {...props}
    >
      <View className="pointer-events-none absolute left-2 flex size-3.5 items-center justify-center">
        <CircleIcon className="size-2 fill-current" />
      </View>
      {children}
    </Pressable>
  );
}

// ---------------------- sub menu (simple stubs) --------------

type DropdownMenuSubProps = ViewWithClassNameProps & {
  children: React.ReactNode;
};

function DropdownMenuSub({
  className,
  children,
  ...props
}: DropdownMenuSubProps) {
  return (
    <View data-slot="dropdown-menu-sub" className={className} {...props}>
      {children}
    </View>
  );
}

type DropdownMenuSubTriggerProps = PressableWithClassNameProps & {
  inset?: boolean;
  children: React.ReactNode;
};

function DropdownMenuSubTrigger({
  className,
  inset,
  children,
  ...props
}: DropdownMenuSubTriggerProps) {
  return (
    <Pressable
      data-slot="dropdown-menu-sub-trigger"
      data-inset={inset}
      className={cn(
        "focus:bg-accent focus:text-accent-foreground data-[state=open]:bg-accent data-[state=open]:text-accent-foreground flex cursor-default items-center rounded-sm px-2 py-1.5 text-sm outline-hidden select-none data-[inset]:pl-8",
        className,
      )}
      {...props}
    >
      {children}
      <ChevronRightIcon className="ml-auto size-4" />
    </Pressable>
  );
}

type DropdownMenuSubContentProps = ViewWithClassNameProps & {
  children: React.ReactNode;
};

function DropdownMenuSubContent({
  className,
  children,
  ...props
}: DropdownMenuSubContentProps) {
  return (
    <View
      data-slot="dropdown-menu-sub-content"
      className={cn(
        "bg-popover text-popover-foreground data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 z-50 min-w-[8rem] origin-(--radix-dropdown-menu-content-transform-origin) overflow-hidden rounded-md border p-1 shadow-lg",
        className,
      )}
      {...props}
    >
      {children}
    </View>
  );
}

// ---------------------- exports ------------------------------

export {
  DropdownMenu,
  DropdownMenuPortal,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuItem,
  DropdownMenuCheckboxItem,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuSubContent,
};
