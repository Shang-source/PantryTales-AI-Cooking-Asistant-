import { useState, createContext, useContext } from "react";
import {
  View,
  Text,
  Modal,
  Pressable,
  TouchableWithoutFeedback,
  KeyboardAvoidingView,
  Platform,
  ViewProps,
  PressableProps,
  TextProps,
} from "react-native";
import { X } from "lucide-react-native";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

interface DialogContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}

const DialogContext = createContext<DialogContextValue | undefined>(undefined);

interface DialogProps {
  children: React.ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  defaultOpen?: boolean;
}

function Dialog({
  children,
  open: controlledOpen,
  onOpenChange,
  defaultOpen = false,
}: DialogProps) {
  const [internalOpen, setInternalOpen] = useState(defaultOpen);

  const isControlled = controlledOpen !== undefined;
  const open = isControlled ? controlledOpen : internalOpen;

  const setOpen = (newOpen: boolean) => {
    if (!isControlled) {
      setInternalOpen(newOpen);
    }
    onOpenChange?.(newOpen);
  };

  return (
    <DialogContext.Provider value={{ open, setOpen }}>
      {children}
    </DialogContext.Provider>
  );
}

interface DialogTriggerProps extends PressableProps {
  asChild?: boolean;
  className?: string;
}

function DialogTrigger({
  children,
  asChild,
  className,
  ...props
}: DialogTriggerProps) {
  const context = useContext(DialogContext);
  if (!context) throw new Error("DialogTrigger must be used within Dialog");

  return (
    <Pressable
      onPress={() => context.setOpen(true)}
      className={cn(className)}
      {...props}
    >
      {children}
    </Pressable>
  );
}

function DialogPortal({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}

function DialogOverlay({
  className,
  ...props
}: ViewProps & { className?: string }) {
  return <View className={cn("bg-black/50", className)} {...props} />;
}

interface DialogContentProps extends ViewProps {
  children: React.ReactNode;
  className?: string;
  hideCloseButton?: boolean;
  transparent?: boolean;
}

function DialogContent({
  children,
  className,
  hideCloseButton = false,
  transparent = true,
  style,
  ...props
}: DialogContentProps & { style?: ViewProps["style"] }) {
  const context = useContext(DialogContext);
  const { colors } = useTheme();
  if (!context) throw new Error("DialogContent must be used within Dialog");

  if (!context.open) return null;

  return (
    <Modal
      transparent={transparent}
      visible={context.open}
      animationType="fade"
      onRequestClose={() => context.setOpen(false)}
      statusBarTranslucent
    >
      <KeyboardAvoidingView
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        className="flex-1"
      >
        <Pressable
          className="flex-1 bg-black/50 justify-center items-center p-5"
          onPress={() => context.setOpen(false)}
        >
          <TouchableWithoutFeedback>
            <View
              className={cn(
                "rounded-xl p-6 w-full max-w-[400px] relative shadow-lg shadow-black/20",
                className
              )}
              style={[{ backgroundColor: colors.bg }, style]}
              {...props}
            >
              {children}
              {!hideCloseButton && (
                <Pressable
                  className="absolute right-4 top-4 opacity-70 z-10 p-1"
                  onPress={() => context.setOpen(false)}
                  hitSlop={10}
                >
                  <X size={20} color={colors.textMuted} />
                </Pressable>
              )}
            </View>
          </TouchableWithoutFeedback>
        </Pressable>
      </KeyboardAvoidingView>
    </Modal>
  );
}

function DialogHeader({
  children,
  className,
  ...props
}: ViewProps & { className?: string }) {
  return (
    <View className={cn("mb-4 items-center", className)} {...props}>
      {children}
    </View>
  );
}

function DialogFooter({
  children,
  className,
  ...props
}: ViewProps & { className?: string }) {
  return (
    <View
      className={cn("mt-6 flex-row justify-end gap-2", className)}
      {...props}
    >
      {children}
    </View>
  );
}

function DialogTitle({
  children,
  className,
  style,
  ...props
}: TextProps & { className?: string }) {
  const { colors } = useTheme();
  return (
    <Text
      className={cn("text-lg font-semibold text-center mb-1.5", className)}
      style={[{ color: colors.textPrimary }, style]}
      {...props}
    >
      {children}
    </Text>
  );
}

function DialogDescription({
  children,
  className,
  style,
  ...props
}: TextProps & { className?: string }) {
  const { colors } = useTheme();
  return (
    <Text
      className={cn("text-sm text-center", className)}
      style={[{ color: colors.textSecondary }, style]}
      {...props}
    >
      {children}
    </Text>
  );
}

function DialogClose({
  children,
  asChild,
  className,
  ...props
}: DialogTriggerProps) {
  const context = useContext(DialogContext);
  return (
    <Pressable
      onPress={() => context?.setOpen(false)}
      className={cn(className)}
      {...props}
    >
      {children}
    </Pressable>
  );
}

export {
  Dialog,
  DialogTrigger,
  DialogContent,
  DialogHeader,
  DialogFooter,
  DialogTitle,
  DialogDescription,
  DialogClose,
  DialogOverlay,
  DialogPortal,
};
