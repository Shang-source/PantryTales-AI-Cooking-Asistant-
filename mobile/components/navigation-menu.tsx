// mobile/components/navigation-menu.tsx

import { useState } from "react";
import { View, Text, TouchableOpacity } from "react-native";
import { ChevronDown } from "lucide-react-native";
import { cn } from "../utils/cn";

/* ------------------------------
   Types
------------------------------ */
export type NavItem = {
  id: string;
  label: string;
  href?: string;
  disabled?: boolean;

  /* Submenu items */
  children?: NavItem[];

  /* onPress handler for native button press */
  onPress?: () => void;
};

export type NavigationMenuProps = {
  items: NavItem[];
  className?: string;
};

/* ------------------------------
   Root Navigation Menu
------------------------------ */
export function NavigationMenu({ items, className }: NavigationMenuProps) {
  return (
    <View
      className={cn(
        "flex flex-row items-center justify-center gap-2",
        className,
      )}
    >
      {items.map((item) => (
        <NavigationMenuItem key={item.id} item={item} />
      ))}
    </View>
  );
}

/* ------------------------------
   Top-level Item (like Radix item)
------------------------------ */
function NavigationMenuItem({ item }: { item: NavItem }) {
  const [open, setOpen] = useState(false);

  const hasChildren = item.children && item.children.length > 0;

  return (
    <View className="relative">
      <TouchableOpacity
        onPress={() => (hasChildren ? setOpen(!open) : item.onPress?.())}
        disabled={item.disabled}
        className={cn(
          "px-3 py-2 rounded-md flex-row items-center bg-background",
          "active:bg-accent active:text-accent-foreground",
          item.disabled && "opacity-50",
        )}
      >
        <Text className="text-foreground text-sm">{item.label}</Text>

        {hasChildren && (
          <ChevronDown
            size={14}
            className={cn(
              "ml-1 text-muted-foreground transition-transform",
              open && "rotate-180",
            )}
          />
        )}
      </TouchableOpacity>

      {/* Submenu */}
      {hasChildren && open && (
        <View
          className={cn(
            "absolute top-full left-0 mt-1",
            "min-w-[180px] rounded-md border bg-popover shadow p-1 z-50",
          )}
        >
          {item.children!.map((child) => (
            <NavigationSubItem key={child.id} item={child} />
          ))}
        </View>
      )}
    </View>
  );
}

/* ------------------------------
   Submenu item
------------------------------ */
function NavigationSubItem({ item }: { item: NavItem }) {
  const hasChildren = item.children && item.children.length > 0;

  return (
    <TouchableOpacity
      onPress={item.onPress}
      disabled={item.disabled}
      className={cn(
        "px-3 py-2 rounded-sm flex-row items-center",
        "hover:bg-accent hover:text-accent-foreground",
        item.disabled && "opacity-50",
      )}
    >
      <Text className="text-sm">{item.label}</Text>

      {hasChildren && <ChevronDown size={14} className="ml-auto rotate-270" />}
    </TouchableOpacity>
  );
}

export default NavigationMenu;
