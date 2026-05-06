import {
  View,
  Text,
  Modal,
  Pressable,
  Animated,
  Dimensions,
  GestureResponderEvent,
  InteractionManager,
} from "react-native";
import { X } from "lucide-react-native";
import {
  cloneElement,
  createContext,
  isValidElement,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { cn } from "@/utils/cn";
import { useFocusEffect } from "expo-router";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { useTheme } from "@/contexts/ThemeContext";

interface SheetContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}
const SheetContext = createContext<SheetContextValue | undefined>(undefined);

const useSheet = () => {
  const context = useContext(SheetContext);
  if (!context)
    throw new Error("useSheet must be used within a Sheet provider");
  return context;
};

// --- Sheet Root ---
interface SheetProps {
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  children: React.ReactNode;
}

function Sheet({ open: controlledOpen, onOpenChange, children }: SheetProps) {
  const [uncontrolledOpen, setUncontrolledOpen] = useState(false);
  const open = controlledOpen ?? uncontrolledOpen;

  const setOpen = useCallback(
    (newOpen: boolean) => {
      if (onOpenChange) {
        onOpenChange(newOpen);
      } else {
        setUncontrolledOpen(newOpen);
      }
    },
    [onOpenChange]
  );

  useFocusEffect(
    useCallback(() => {
      return () => {
        if (open) {
          setOpen(false);
        }
      };
    }, [open, setOpen])
  );

  return (
    <SheetContext.Provider value={{ open, setOpen }}>
      <View>{children}</View>
    </SheetContext.Provider>
  );
}

// --- Trigger ---
interface SheetTriggerProps extends React.ComponentProps<typeof Pressable> {
  asChild?: boolean;
}

function SheetTrigger({
  className,
  children,
  asChild = false,
  ...props
}: SheetTriggerProps) {
  const { setOpen } = useSheet();
  const handlePress = (e: GestureResponderEvent) => {
    props.onPress?.(e);
    setOpen(true);
  };

  if (asChild && isValidElement(children)) {
    return cloneElement(
      children as React.ReactElement,
      {
        onPress: handlePress,
        ...props,
        className: cn(className, (children.props as any).className),
      } as any
    );
  }

  return (
    <Pressable className={cn(className)} onPress={handlePress} {...props}>
      {children}
    </Pressable>
  );
}

// --- Close ---
interface SheetCloseProps extends React.ComponentProps<typeof Pressable> {
  asChild?: boolean;
}

function SheetClose({
  className,
  children,
  asChild = false,
  ...props
}: SheetCloseProps) {
  const { setOpen } = useSheet();
  const handlePress = (e: GestureResponderEvent) => {
    const originalOnPress = props.onPress;
    setOpen(false);

    if (originalOnPress) {
      InteractionManager.runAfterInteractions(() => {
        originalOnPress(e);
      });
    }
  };

  if (asChild && isValidElement(children)) {
    return cloneElement(
      children as React.ReactElement,
      {
        ...props,
        onPress: handlePress,
        className: cn(className, (children.props as any).className),
      } as any
    );
  }

  return (
    <Pressable className={cn(className)} onPress={handlePress} {...props}>
      {children}
    </Pressable>
  );
}

// --- Content ---
const SHEET_SIDES = {
  top: "inset-x-0 top-0 border-b",
  bottom: "inset-x-0 bottom-0 border-t",
  left: "inset-y-0 left-0 h-full border-r",
  right: "inset-y-0 right-0 h-full border-l",
} as const;

interface SheetContentProps extends React.ComponentProps<typeof View> {
  side?: keyof typeof SHEET_SIDES;
  showCloseButton?: boolean;
}

function SheetContent({
  className,
  children,
  side = "left",
  showCloseButton = true,
  style,
  ...props
}: SheetContentProps) {
  const { open, setOpen } = useSheet();
  const [showModal, setShowModal] = useState(false);
  const insets = useSafeAreaInsets();
  const { colors } = useTheme();

  // Animation States
  const animatedValue = useRef(new Animated.Value(0)).current;
  const [contentLayout, setContentLayout] = useState({ width: 0, height: 0 });
  const screenDimensions = Dimensions.get("window");

  useEffect(() => {
    if (open) {
      setShowModal(true);
      Animated.timing(animatedValue, {
        toValue: 1,
        duration: 300,
        useNativeDriver: true,
      }).start();
    } else {
      Animated.timing(animatedValue, {
        toValue: 0,
        duration: 250,
        useNativeDriver: true,
      }).start(() => setShowModal(false));
    }
  }, [open, animatedValue]);

  // 1. Calculate Opacity Animation
  const opacityStyle = useMemo(
    () => ({
      opacity: animatedValue.interpolate({
        inputRange: [0, 1],
        outputRange: [0, 1],
      }),
    }),
    [animatedValue]
  );

  // 2. Calculate Transform Animation (Logic, not styling)
  const transformStyle = useMemo(() => {
    const distanceX = contentLayout.width || screenDimensions.width;
    const distanceY = contentLayout.height || screenDimensions.height;

    const transformMap = {
      left: [
        {
          translateX: animatedValue.interpolate({
            inputRange: [0, 1],
            outputRange: [-distanceX, 0],
          }),
        },
      ],
      right: [
        {
          translateX: animatedValue.interpolate({
            inputRange: [0, 1],
            outputRange: [distanceX, 0],
          }),
        },
      ],
      top: [
        {
          translateY: animatedValue.interpolate({
            inputRange: [0, 1],
            outputRange: [-distanceY, 0],
          }),
        },
      ],
      bottom: [
        {
          translateY: animatedValue.interpolate({
            inputRange: [0, 1],
            outputRange: [distanceY, 0],
          }),
        },
      ],
    };

    return { transform: transformMap[side] };
  }, [side, contentLayout, screenDimensions, animatedValue]);

  const safeAreaStyle = useMemo(() => {
    if (side !== "left" && side !== "right") return undefined;
    return {
      paddingTop: insets.top,
      paddingBottom: insets.bottom,
    } as const;
  }, [side, insets.top, insets.bottom]);

  if (!showModal) return null;

  return (
    <Modal
      transparent
      visible={showModal}
      onRequestClose={() => setOpen(false)}
      animationType="none"
      statusBarTranslucent
    >
      <Animated.View
        className="absolute inset-0 bg-black/50"
        style={opacityStyle} // for Native animation
      >
        <Pressable
          className="absolute inset-0"
          onPress={() => setOpen(false)}
        />
      </Animated.View>

      <View
        pointerEvents="box-none"
        className={cn(
          "flex-1",
          side === "top" && "justify-start",
          side === "bottom" && "justify-end",
          side === "left" && "flex-row justify-start",
          side === "right" && "flex-row justify-end"
        )}
      >
        <Animated.View
          onLayout={(e) => setContentLayout(e.nativeEvent.layout)}
          style={[transformStyle, safeAreaStyle, { backgroundColor: colors.bg }, style]} // for Native animation
          className={cn(
            "shadow-lg relative p-6",
            SHEET_SIDES[side],
            (side === "left" || side === "right") &&
              "w-3/4 max-w-sm sm:max-w-sm",
            className
          )}
          {...props}
        >
          {children}
          {showCloseButton ? (
            <Pressable
              className="absolute right-4 top-4 opacity-70 active:opacity-100 p-2"
              onPress={() => setOpen(false)}
              hitSlop={10}
            >
              <X size={20} color={colors.textSecondary} />
            </Pressable>
          ) : null}
        </Animated.View>
      </View>
    </Modal>
  );
}

function SheetHeader({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View
      className={cn(
        "flex flex-col space-y-2 text-center sm:text-left mb-4",
        className
      )}
      {...props}
    />
  );
}

function SheetFooter({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View
      className={cn(
        "flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2 mt-auto pt-4",
        className
      )}
      {...props}
    />
  );
}

function SheetTitle({
  className,
  style,
  ...props
}: React.ComponentProps<typeof Text>) {
  const { colors } = useTheme();
  return (
    <Text
      className={cn("text-lg font-semibold", className)}
      style={[{ color: colors.textPrimary }, style]}
      {...props}
    />
  );
}

function SheetDescription({
  className,
  style,
  ...props
}: React.ComponentProps<typeof Text>) {
  const { colors } = useTheme();
  return (
    <Text
      className={cn("text-sm", className)}
      style={[{ color: colors.textSecondary }, style]}
      {...props}
    />
  );
}

export {
  Sheet,
  SheetTrigger,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetFooter,
  SheetTitle,
  SheetDescription,
};
