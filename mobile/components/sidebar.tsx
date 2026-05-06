import {
  View,
  Text,
  Dimensions,
  Animated,
  Modal,
  TouchableOpacity,
  ScrollView,
  TouchableWithoutFeedback
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { PanelLeft } from "lucide-react-native";
import {cn} from "@/utils/cn";
import { Button } from "./Button";
import { Input } from "./input";
import { Separator } from "./separator";
import { Children, createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";

const SIDEBAR_WIDTH = 280;
const SIDEBAR_WIDTH_ICON = 64;
const MOBILE_BREAKPOINT = 768;

// --- Context ---
type SidebarContextProps = {
  state: "expanded" | "collapsed";
  open: boolean;
  setOpen: (open: boolean) => void;
  isMobile: boolean;
  toggleSidebar: () => void;
};

const SidebarContext = createContext<SidebarContextProps | null>(null);

function useSidebar() {
  const context = useContext(SidebarContext);
  if (!context) {
    throw new Error("useSidebar must be used within a SidebarProvider.");
  }
  return context;
}

function useIsMobile() {
  const [isMobile, setIsMobile] = useState(
    Dimensions.get("window").width < MOBILE_BREAKPOINT
  );

  useEffect(() => {
    const onChange = ({ window }: { window: { width: number } }) => {
      setIsMobile(window.width < MOBILE_BREAKPOINT);
    };
    const subscription = Dimensions.addEventListener("change", onChange);
    return () => subscription.remove();
  }, []);

  return isMobile;
}

// --- Sidebar Provider ---
function SidebarProvider({
  defaultOpen = true,
  open: openProp,
  onOpenChange: setOpenProp,
  className,
  children,
  ...props
}: React.ComponentProps<typeof View> & {
  defaultOpen?: boolean;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}) {
  const isMobile = useIsMobile();
  const [openState, setOpenState] = useState(defaultOpen);

  const open = openProp ?? openState;

  const setOpen = useCallback(
    (value: boolean | ((value: boolean) => boolean)) => {
      const nextOpen = typeof value === "function" ? value(open) : value;
      if (setOpenProp) {
        setOpenProp(nextOpen);
      } else {
        setOpenState(nextOpen);
      }
    },
    [setOpenProp, open]
  );

  const toggleSidebar = useCallback(() => {
    setOpen((prev) => !prev);
  }, [setOpen]);

  const state = open ? "expanded" : "collapsed";

  const contextValue = useMemo(
    () => ({
      state: state as "expanded" | "collapsed",
      open,
      setOpen,
      isMobile,
      toggleSidebar,
    }),
    [state, open, setOpen, isMobile, toggleSidebar]
  );

  return (
    <SidebarContext.Provider value={contextValue}>
      <View
        className={cn(
          "flex-1 flex-row h-full bg-gray-100 dark:bg-black",
          className
        )}
        {...props}
      >
        {children}
      </View>
    </SidebarContext.Provider>
  );
}

// --- Sidebar ---
function Sidebar({
  side = "left",
  variant = "sidebar",
  collapsible = "offcanvas",
  className,
  children,
  ...props
}: React.ComponentProps<typeof View> & {
  side?: "left" | "right";
  variant?: "sidebar" | "floating" | "inset";
  collapsible?: "offcanvas" | "icon" | "none";
}) {
  const { isMobile, open, setOpen } = useSidebar();
  const insets = useSafeAreaInsets();

  const animatedWidth = useRef(
    new Animated.Value(open ? SIDEBAR_WIDTH : SIDEBAR_WIDTH_ICON)
  ).current;

  useEffect(() => {
    if (!isMobile) {
      Animated.timing(animatedWidth, {
        toValue: open ? SIDEBAR_WIDTH : SIDEBAR_WIDTH_ICON,
        duration: 300,
        useNativeDriver: false,
      }).start();
    }
  }, [open, isMobile, animatedWidth]);

  if (isMobile) {
    return (
      <Modal
        visible={open}
        transparent
        animationType="fade"
        onRequestClose={() => setOpen(false)}
        statusBarTranslucent
      >
        <View className="flex-1 flex-row">
          <TouchableWithoutFeedback onPress={() => setOpen(false)}>
            <View className="absolute inset-0 bg-black/50" />
          </TouchableWithoutFeedback>

          <View
            className={cn(
              "h-full bg-white dark:bg-zinc-900 border-r border-zinc-200 dark:border-zinc-800 w-[85%] max-w-[300px] shadow-xl",
              side === "right" && "ml-auto border-l border-r-0",
              className
            )}
            {...props}
          >
            {/* Safe area spacer at top */}
            <View style={{ height: insets.top }} />
            <View className="flex-1">{children}</View>
            {/* Safe area spacer at bottom */}
            <View style={{ height: insets.bottom }} />
          </View>
        </View>
      </Modal>
    );
  }

  if (collapsible === "none") {
    return (
      <View
        className={cn(
          "bg-white dark:bg-zinc-900 border-r border-zinc-200 dark:border-zinc-800 flex h-full",
          `w-[${SIDEBAR_WIDTH}px]`,
          className
        )}
        {...props}
      >
        {children}
      </View>
    );
  }

  return (
    <Animated.View
      className={cn(
        "bg-white dark:bg-zinc-900 border-r border-zinc-200 dark:border-zinc-800 hidden md:flex h-full flex-col overflow-hidden",
        className
      )}
      style={{ width: animatedWidth }}
      {...props}
    >
      <View className="flex-1 w-full">{children}</View>
    </Animated.View>
  );
}

function SidebarTrigger({
  className,
  onPress,
  ...props
}: Omit<React.ComponentProps<typeof Button>, "children">) {
  const { toggleSidebar } = useSidebar();

  return (
    <Button
      variant="ghost"
      size="icon"
      className={cn("h-9 w-9 p-0", className)}
      onPress={(e: any) => {
        onPress?.(e);
        toggleSidebar();
      }}
      {...props}
    >
      <PanelLeft size={18} className="text-foreground" color="gray" />
      <Text className="sr-only">Toggle Sidebar</Text>
    </Button>
  );
}

function SidebarInput({
  className,
  ...props
}: React.ComponentProps<typeof Input>) {
  return (
    <Input
      className={cn("h-10 bg-white dark:bg-zinc-950", className)}
      {...props}
    />
  );
}

function SidebarSeparator({
  className,
  ...props
}: React.ComponentProps<typeof Separator>) {
  return (
    <Separator
      className={cn("mx-2 w-auto bg-zinc-200 dark:bg-zinc-700", className)}
      {...props}
    />
  );
}

// --- Layout Helpers ---

function SidebarInset({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View className={cn("flex-1 flex flex-col h-full", className)} {...props} />
  );
}

function SidebarHeader({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View className={cn("flex flex-col gap-2 p-4", className)} {...props} />
  );
}

function SidebarFooter({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View
      className={cn(
        "flex flex-col gap-2 p-4 mt-auto border-t border-zinc-100 dark:border-zinc-800",
        className
      )}
      {...props}
    />
  );
}

function SidebarContent({
  className,
  ...props
}: React.ComponentProps<typeof ScrollView>) {
  return (
    <ScrollView
      data-slot="sidebar-content"
      className={cn("flex-1", className)}
      contentContainerClassName="p-2 gap-2"
      showsVerticalScrollIndicator={false}
      {...props}
    />
  );
}

function SidebarGroup({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View className={cn("flex flex-col w-full py-2", className)} {...props} />
  );
}

function SidebarGroupLabel({
  className,
  children,
  ...props
}: React.ComponentProps<typeof Text>) {
  const { state, isMobile } = useSidebar();
  if (state === "collapsed" && !isMobile) return <View className="h-4" />;

  return (
    <Text
      className={cn(
        "text-zinc-500 dark:text-zinc-400 text-xs font-medium px-2 py-1.5 mb-1 uppercase tracking-wider",
        className
      )}
      {...props}
    >
      {children}
    </Text>
  );
}

function SidebarMenu({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View className={cn("flex flex-col gap-1 w-full", className)} {...props} />
  );
}

function SidebarMenuItem({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return <View className={cn("relative", className)} {...props} />;
}

// --- Menu Button ---
function SidebarMenuButton({
  className,
  isActive = false,
  children,
  size = "default",
  ...props
}: React.ComponentProps<typeof TouchableOpacity> & {
  isActive?: boolean;
  size?: "default" | "sm" | "lg";
}) {
  const { state, isMobile } = useSidebar();
  const isCollapsed = state === "collapsed" && !isMobile;

  return (
    <TouchableOpacity
      activeOpacity={0.7}
      className={cn(
        "flex flex-row items-center gap-2 rounded-md px-2 py-2 transition-colors",
        isActive
          ? "bg-zinc-100 dark:bg-zinc-800"
          : "hover:bg-zinc-100 dark:hover:bg-zinc-800",
        isCollapsed && "justify-center px-0",
        size === "sm" && "py-1",
        size === "lg" && "py-3",
        className
      )}
      {...props}
    >
      {isCollapsed
        ? Children.map(children, (child, index) =>
            index === 0 ? child : null
          )
        : children}
    </TouchableOpacity>
  );
}

function SidebarMenuBadge({
  className,
  ...props
}: React.ComponentProps<typeof Text>) {
  const { state, isMobile } = useSidebar();
  if (state === "collapsed" && !isMobile) return null;

  return (
    <Text
      className={cn("text-xs font-medium text-zinc-500 ml-auto", className)}
      {...props}
    />
  );
}

export {
  SidebarProvider,
  Sidebar,
  SidebarTrigger,
  SidebarInset,
  SidebarHeader,
  SidebarFooter,
  SidebarContent,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarMenuBadge,
  SidebarInput,
  SidebarSeparator,
  useSidebar,
};
