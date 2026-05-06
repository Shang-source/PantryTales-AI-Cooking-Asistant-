import { useState, createContext, useContext } from "react";
import {
  View,
  Text,
  Pressable,
  Modal,
  Dimensions,
  PressableProps,
  ViewProps,
} from "react-native";
import { Check, Circle } from "lucide-react-native";
import { cn } from "@/utils/cn";

interface ContextMenuContextValue {
  visible: boolean;
  position: { x: number; y: number };
  openMenu: (x: number, y: number) => void;
  closeMenu: () => void;
}

const ContextMenuContext = createContext<ContextMenuContextValue | undefined>(
  undefined
);

function ContextMenu({ children }: { children: React.ReactNode }) {
  const [visible, setVisible] = useState(false);
  const [position, setPosition] = useState({ x: 0, y: 0 });

  const openMenu = (x: number, y: number) => {
    const screenHeight = Dimensions.get("window").height;
    const adjustedY = y > screenHeight - 200 ? y - 200 : y;

    setPosition({ x, y: adjustedY });
    setVisible(true);
  };

  const closeMenu = () => setVisible(false);

  return (
    <ContextMenuContext.Provider
      value={{ visible, position, openMenu, closeMenu }}
    >
      {children}
    </ContextMenuContext.Provider>
  );
}

interface ContextMenuTriggerProps extends PressableProps {
  className?: string;
}

function ContextMenuTrigger({
  children,
  className,
  ...props
}: ContextMenuTriggerProps) {
  const context = useContext(ContextMenuContext);
  if (!context)
    throw new Error("ContextMenuTrigger must be used within ContextMenu");

  return (
    <Pressable
      onLongPress={(e) => {
        const { pageX, pageY } = e.nativeEvent;
        context.openMenu(pageX, pageY);
      }}
      delayLongPress={200}
      className={cn(className)}
      {...props}
    >
      {children}
    </Pressable>
  );
}

interface ContextMenuContentProps extends ViewProps {
  className?: string;
}

function ContextMenuContent({
  children,
  className,
  ...props
}: ContextMenuContentProps) {
  const context = useContext(ContextMenuContext);
  if (!context)
    throw new Error("ContextMenuContent must be used within ContextMenu");

  if (!context.visible) return null;

  return (
    <Modal
      transparent={true}
      visible={context.visible}
      animationType="fade"
      onRequestClose={context.closeMenu}
    >
      <Pressable className="flex-1" onPress={context.closeMenu}>
        <View
          className={cn(
            "absolute w-[200px] bg-white rounded-lg py-1 border border-[#eee] shadow-lg shadow-black/25",
            className
          )}
          // Only this part must keep inline style because the coordinates (x, y) are dynamically generated from user clicks. NativeWind cannot predict these values at build time and generate corresponding classes.

          style={{
            top: context.position.y,
            left: Math.min(
              context.position.x,
              Dimensions.get("window").width - 200
            ),
          }}
          onStartShouldSetResponder={() => true}
          {...props}
        >
          {children}
        </View>
      </Pressable>
    </Modal>
  );
}

interface ContextMenuItemProps extends PressableProps {
  inset?: boolean;
  destructive?: boolean;
  children: React.ReactNode;
  className?: string;
}

function ContextMenuItem({
  children,
  className,
  inset,
  destructive,
  onPress,
  ...props
}: ContextMenuItemProps) {
  const context = useContext(ContextMenuContext);

  return (
    <Pressable
      className={cn(
        "flex-row items-center py-2.5 px-3 active:bg-[#F3F4F6]",
        inset && "pl-8",
        className
      )}
      {...props}
      onPress={(e) => {
        onPress?.(e);
        context?.closeMenu();
      }}
    >
      {typeof children === "string" ? (
        <Text
          className={cn("text-sm text-[#333]", destructive && "text-[#EF4444]")}
        >
          {children}
        </Text>
      ) : (
        children
      )}
    </Pressable>
  );
}

function ContextMenuLabel({
  children,
  className,
  inset,
  ...props
}: {
  children: React.ReactNode;
  className?: string;
  inset?: boolean;
} & Text["props"]) {
  return (
    <Text
      className={cn(
        "text-xs text-[#888] font-semibold px-3 py-1.5",
        inset && "pl-8",
        className
      )}
      {...props}
    >
      {children}
    </Text>
  );
}

function ContextMenuSeparator({
  className,
  ...props
}: {
  className?: string;
} & ViewProps) {
  return (
    <View className={cn("h-[1px] bg-[#eee] my-1", className)} {...props} />
  );
}

interface ContextMenuCheckboxItemProps extends ContextMenuItemProps {
  checked?: boolean;
}

function ContextMenuCheckboxItem({
  children,
  checked,
  className,
  ...props
}: ContextMenuCheckboxItemProps) {
  return (
    <ContextMenuItem {...props} className={cn("pl-8", className)}>
      <View className="absolute left-2.5 w-4 items-center justify-center">
        {checked && <Check size={14} color="#333" />}
      </View>
      <Text className="text-sm text-[#333]">{children}</Text>
    </ContextMenuItem>
  );
}

function ContextMenuRadioItem({
  children,
  checked,
  className,
  ...props
}: ContextMenuCheckboxItemProps) {
  return (
    <ContextMenuItem {...props} className={cn("pl-8", className)}>
      <View className="absolute left-2.5 w-4 items-center justify-center">
        {checked && <Circle size={8} fill="#333" color="#333" />}
      </View>
      <Text className="text-sm text-[#333]">{children}</Text>
    </ContextMenuItem>
  );
}

function ContextMenuShortcut({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <Text
      className={cn("ml-auto text-xs text-[#999] tracking-widest", className)}
    >
      {children}
    </Text>
  );
}

const ContextMenuGroup = View;
const ContextMenuPortal = View;
const ContextMenuSub = View;
const ContextMenuSubContent = View;
const ContextMenuSubTrigger = ContextMenuItem;
const ContextMenuRadioGroup = View;

export {
  ContextMenu,
  ContextMenuTrigger,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuCheckboxItem,
  ContextMenuRadioItem,
  ContextMenuLabel,
  ContextMenuSeparator,
  ContextMenuShortcut,
  ContextMenuGroup,
  ContextMenuPortal,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuRadioGroup,
};
