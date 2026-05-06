import { cn } from "@/utils/cn";
import { useState, createContext, useContext } from "react";
import {
  View,
  Text,
  Modal,
  Pressable,
  KeyboardAvoidingView,
  Platform,
  TouchableWithoutFeedback,
  ViewProps,
  TextProps,
  PressableProps,
} from "react-native";

interface DrawerContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}

const DrawerContext = createContext<DrawerContextValue | undefined>(undefined);

interface DrawerProps {
  children: React.ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  defaultOpen?: boolean;
}

function Drawer({
  children,
  open: controlledOpen,
  onOpenChange,
  defaultOpen = false,
}: DrawerProps) {
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
    <DrawerContext.Provider value={{ open, setOpen }}>
      {children}
    </DrawerContext.Provider>
  );
}

interface DrawerTriggerProps extends PressableProps {
  asChild?: boolean;
  className?: string;
}

function DrawerTrigger({
  children,
  asChild,
  className,
  ...props
}: DrawerTriggerProps) {
  const context = useContext(DrawerContext);
  if (!context) throw new Error("DrawerTrigger must be used within Drawer");

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

function DrawerPortal({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}

function DrawerOverlay({
  className,
  ...props
}: ViewProps & { className?: string }) {
  return <View className={cn("bg-black/50", className)} {...props} />;
}

interface DrawerContentProps extends ViewProps {
  children: React.ReactNode;
  className?: string;
}

function DrawerContent({
  children,
  className,
  ...props
}: DrawerContentProps) {
  const context = useContext(DrawerContext);
  if (!context) throw new Error("DrawerContent must be used within Drawer");

  if (!context.open) return null;

  return (
    <Modal
      transparent={true}
      visible={context.open}
      animationType="slide"
      onRequestClose={() => context.setOpen(false)}
      statusBarTranslucent
    >
      <KeyboardAvoidingView
        behavior={Platform.OS === "ios" ? "padding" : undefined}
        className="flex-1"
      >
        <Pressable
          className="flex-1 bg-black/50 justify-end"
          onPress={() => context.setOpen(false)}
        >
          <TouchableWithoutFeedback>
            <View
              className={cn(
                "bg-white rounded-t-[10px] pb-10 w-full max-h-[90%] shadow-lg shadow-black/10",
                className
              )}
              {...props}
            >
              <View className="items-center pt-3 pb-2">
                <View className="w-[50px] h-1 rounded-full bg-[#E2E8F0]" />
              </View>

              {children}
            </View>
          </TouchableWithoutFeedback>
        </Pressable>
      </KeyboardAvoidingView>
    </Modal>
  );
}

function DrawerHeader({
  children,
  className,
  ...props
}: ViewProps & { className?: string }) {
  return (
    <View className={cn("px-4 mb-2 gap-1", className)} {...props}>
      {children}
    </View>
  );
}

function DrawerFooter({
  children,
  className,
  ...props
}: ViewProps & { className?: string }) {
  return (
    <View className={cn("mt-auto px-4 pt-4 gap-2", className)} {...props}>
      {children}
    </View>
  );
}

function DrawerTitle({
  children,
  className,
  ...props
}: TextProps & { className?: string }) {
  return (
    <Text
      className={cn("text-lg font-semibold text-black text-center", className)}
      {...props}
    >
      {children}
    </Text>
  );
}

function DrawerDescription({
  children,
  className,
  ...props
}: TextProps & { className?: string }) {
  return (
    <Text
      className={cn("text-sm text-[#666] text-center", className)}
      {...props}
    >
      {children}
    </Text>
  );
}

function DrawerClose({
  children,
  asChild,
  className,
  ...props
}: DrawerTriggerProps) {
  const context = useContext(DrawerContext);
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
  Drawer,
  DrawerPortal,
  DrawerOverlay,
  DrawerTrigger,
  DrawerClose,
  DrawerContent,
  DrawerHeader,
  DrawerFooter,
  DrawerTitle,
  DrawerDescription,
};
