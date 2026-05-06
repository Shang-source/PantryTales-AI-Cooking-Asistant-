// mobile/components/menubar.tsx

import { useState } from "react";
import { View, Text, TouchableOpacity } from "react-native";
import { ChevronRight, Check, Circle } from "lucide-react-native";
import { cn } from "../utils/cn";

/** type definition */
export type MenuItem = {
  id: string;
  label: string;
  disabled?: boolean;
  destructive?: boolean;
  checked?: boolean;
  type?: "normal" | "checkbox" | "radio" | "submenu";
  children?: MenuItem[];
  onPress?: () => void;
};

export type MenubarProps = {
  items: MenuItem[];
  className?: string;
};

/** ============================
 *      top level Menubar
 * ============================ */
export default function Menubar({ items, className }: MenubarProps) {
  const [openMenu, setOpenMenu] = useState<string | null>(null);

  return (
    <View
      className={cn(
        "flex flex-row items-center h-10 rounded-md border bg-background p-1 gap-1",
        className
      )}
    >
      {items.map((item) => (
        <TopMenuItem
          key={item.id}
          item={item}
          isOpen={openMenu === item.id}
          onToggle={() => setOpenMenu(openMenu === item.id ? null : item.id)}
        />
      ))}
    </View>
  );
}

/** ============================
 *      Top menu entrance
 * ============================ */
function TopMenuItem({
  item,
  isOpen,
  onToggle,
}: {
  item: MenuItem;
  isOpen: boolean;
  onToggle: () => void;
}) {
  const hasSubmenu = item.children && item.children.length > 0;

  const handlePress = () => {
    if (item.disabled) return;
    if (hasSubmenu) onToggle();
    else item.onPress?.();
  };

  return (
    <View className="relative">
      <TouchableOpacity
        onPress={handlePress}
        disabled={item.disabled}
        className={cn(
          "px-3 py-1.5 rounded-sm flex-row items-center",
          item.disabled && "opacity-50",
          isOpen && "bg-accent text-accent-foreground"
        )}
      >
        <Text className="text-foreground text-sm">{item.label}</Text>
        {hasSubmenu && (
          <ChevronRight size={16} className="ml-1 text-muted-foreground" />
        )}
      </TouchableOpacity>

      {/* sub menu ↓ */}
      {hasSubmenu && isOpen && (
        <View
          className={cn(
            "absolute left-0 top-full mt-1",
            "min-w-[160px] rounded-md border bg-popover shadow-lg p-1 z-50"
          )}
        >
          {item.children!.map((child) => (
            <SubMenuItem key={child.id} item={child} />
          ))}
        </View>
      )}
    </View>
  );
}

/** ============================
 *      Submenu content
 * ============================ */
function SubMenuItem({ item }: { item: MenuItem }) {
  const isCheckbox = item.type === "checkbox";
  const isRadio = item.type === "radio";

  return (
    <TouchableOpacity
      onPress={item.onPress}
      disabled={item.disabled}
      className={cn(
        "flex-row items-center gap-2 px-3 py-2 rounded-sm",
        item.disabled && "opacity-50",
        item.destructive ? "text-destructive" : "text-foreground",
        "hover:bg-accent"
      )}
    >
      {/* Checkbox / Radio */}
      {isCheckbox && (
        <View className="w-4 items-center justify-center">
          {item.checked && <Check size={14} />}
        </View>
      )}

      {isRadio && (
        <View className="w-4 items-center justify-center">
          {item.checked && <Circle size={8} />}
        </View>
      )}

      {/* Label */}
      <Text className="text-sm">{item.label}</Text>

      {/* Submenu arrow */}
      {item.children && (
        <ChevronRight size={16} className="ml-auto text-muted-foreground" />
      )}
    </TouchableOpacity>
  );
}
