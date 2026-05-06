"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  isValidElement,
  cloneElement,
  type ReactNode,
  type ReactElement,
} from "react";
import {
  View,
  Text,
  Modal,
  TouchableOpacity,
  type GestureResponderEvent,
} from "react-native";

import { cn } from "../utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

/*  Context & Types   */

interface AlertDialogContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}

const AlertDialogContext = createContext<AlertDialogContextValue | null>(null);

function useAlertDialogContext() {
  const ctx = useContext(AlertDialogContext);
  if (!ctx) {
    throw new Error(
      "AlertDialog components must be used within <AlertDialog />"
    );
  }
  return ctx;
}

interface AlertDialogProps {
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
  children: ReactNode;
}

type PressableChildProps = {
  onPress?: (event: GestureResponderEvent) => void;
  className?: string;
  children?: ReactNode;
  [key: string]: any;
};

interface AlertDialogTriggerProps {
  children: ReactElement<PressableChildProps>;
}

interface AlertDialogContentProps {
  children: ReactNode;
  className?: string;
}

interface AlertDialogOverlayProps {
  className?: string;
  children?: ReactNode;
}

interface SimpleDivProps {
  children: ReactNode;
  className?: string;
}

interface AlertDialogTitleProps {
  children: ReactNode;
  className?: string;
}

interface AlertDialogDescriptionProps {
  children: ReactNode;
  className?: string;
}

type AlertDialogButtonVariant = "default" | "outline" | "destructive";

interface AlertDialogButtonProps {
  children: ReactNode;
  variant?: AlertDialogButtonVariant;
  className?: string;
  textClassName?: string;
  onPress?: (event: GestureResponderEvent) => void;
}

/*  Root  */

function AlertDialog({
  open,
  defaultOpen,
  onOpenChange,
  children,
}: AlertDialogProps) {
  const [internalOpen, setInternalOpen] = useState(defaultOpen ?? false);

  const isControlled = open !== undefined;
  const actualOpen = isControlled ? open : internalOpen;

  const setOpen = useCallback(
    (next: boolean) => {
      onOpenChange?.(next);
      if (!isControlled) {
        setInternalOpen(next);
      }
    },
    [onOpenChange, isControlled]
  );

  const ctxValue = useMemo(
    () => ({ open: actualOpen, setOpen }),
    [actualOpen, setOpen]
  );

  return (
    <AlertDialogContext.Provider value={ctxValue}>
      {children}
    </AlertDialogContext.Provider>
  );
}

/*  Trigger  */

function AlertDialogTrigger({ children }: AlertDialogTriggerProps) {
  const { setOpen } = useAlertDialogContext();

  if (!isValidElement(children)) return null;

  return cloneElement(children, {
    onPress: (e: GestureResponderEvent) => {
      children.props.onPress?.(e);
      setOpen(true);
    },
  });
}

/*  Overlay + Portal  */

function AlertDialogPortal({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

function AlertDialogOverlay({ className, children }: AlertDialogOverlayProps) {
  return (
    <View
      className={cn(
        "flex-1 items-center justify-center bg-black/50",
        className
      )}
    >
      {children}
    </View>
  );
}

/*  Content   */

function AlertDialogContent({ children, className }: AlertDialogContentProps) {
  const { open, setOpen } = useAlertDialogContext();
  const { colors } = useTheme();

  return (
    <AlertDialogPortal>
      <Modal
        visible={open}
        transparent
        animationType="fade"
        onRequestClose={() => setOpen(false)}
      >
        <AlertDialogOverlay>
          <View
            className={cn(
              "w-[90%] max-w-md rounded-xl border p-6 shadow-lg",
              className
            )}
            style={{ backgroundColor: colors.card, borderColor: colors.border }}
          >
            {children}
          </View>
        </AlertDialogOverlay>
      </Modal>
    </AlertDialogPortal>
  );
}

/*  Header & Footer  */

function AlertDialogHeader({ children, className }: SimpleDivProps) {
  return (
    <View className={cn("mb-3 flex flex-col gap-2", className)}>
      {children}
    </View>
  );
}

function AlertDialogFooter({ children, className }: SimpleDivProps) {
  return (
    <View className={cn("mt-4 flex flex-row justify-end gap-3", className)}>
      {children}
    </View>
  );
}

/*  Title & Description */

function AlertDialogTitle({ children, className }: AlertDialogTitleProps) {
  const { colors } = useTheme();
  return (
    <Text
      className={cn("text-lg font-semibold", className)}
      style={{ color: colors.textPrimary }}
    >
      {children}
    </Text>
  );
}

function AlertDialogDescription({
  children,
  className,
}: AlertDialogDescriptionProps) {
  const { colors } = useTheme();
  return (
    <Text className={cn("text-sm", className)} style={{ color: colors.textSecondary }}>
      {children}
    </Text>
  );
}

/*  Buttons  */

function BaseButton({
  children,
  variant = "default",
  className,
  textClassName,
  onPress,
}: AlertDialogButtonProps) {
  const { colors } = useTheme();
  const base =
    "min-h-[36px] px-4 py-2 rounded-md items-center justify-center flex-row";
  const textBase = "text-sm font-medium";

  let bgStyle: { backgroundColor: string; borderWidth?: number; borderColor?: string } = {
    backgroundColor: colors.accent,
  };
  let textColor = colors.bg;

  if (variant === "destructive") {
    bgStyle = { backgroundColor: colors.error };
    textColor = "#ffffff";
  } else if (variant === "outline") {
    bgStyle = { backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border };
    textColor = colors.textPrimary;
  }

  return (
    <TouchableOpacity
      onPress={onPress}
      activeOpacity={0.85}
      className={cn(base, className)}
      style={bgStyle}
    >
      {typeof children === "string" ? (
        <Text className={cn(textBase, textClassName)} style={{ color: textColor }}>
          {children}
        </Text>
      ) : (
        children
      )}
    </TouchableOpacity>
  );
}

function AlertDialogAction(props: AlertDialogButtonProps) {
  const { setOpen } = useAlertDialogContext();
  const { onPress, ...rest } = props;

  return (
    <BaseButton
      {...rest}
      onPress={(e) => {
        onPress?.(e);
        setOpen(false);
      }}
    />
  );
}

function AlertDialogCancel(props: AlertDialogButtonProps) {
  const { setOpen } = useAlertDialogContext();
  const { onPress, variant = "outline", ...rest } = props;

  return (
    <BaseButton
      {...rest}
      variant={variant}
      onPress={(e) => {
        onPress?.(e);
        setOpen(false);
      }}
    />
  );
}

export {
  AlertDialog,
  AlertDialogPortal,
  AlertDialogOverlay,
  AlertDialogTrigger,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogCancel,
};
