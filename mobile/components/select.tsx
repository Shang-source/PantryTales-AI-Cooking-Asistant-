import {
  View,
  Text,
  Modal,
  TouchableOpacity,
  ScrollView,
  TouchableWithoutFeedback,
} from "react-native";
import { Check, ChevronDown } from "lucide-react-native";
import { createContext, useContext, useEffect, useState } from "react";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

interface SelectContextValue {
  value?: string;
  onValueChange?: (value: string) => void;
  open: boolean;
  setOpen: (open: boolean) => void;
  label?: string;
  setLabel: (label: string) => void;
}

const SelectContext = createContext<SelectContextValue | undefined>(undefined);

const useSelect = () => {
  const context = useContext(SelectContext);
  if (!context) {
    throw new Error("useSelect must be used within a Select provider");
  }
  return context;
};

interface SelectProps {
  value?: string;
  onValueChange?: (value: string) => void;
  defaultValue?: string;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  children: React.ReactNode;
}

function Select({
  value: controlledValue,
  onValueChange,
  defaultValue,
  open: controlledOpen,
  onOpenChange,
  children,
}: SelectProps) {
  const [uncontrolledValue, setUncontrolledValue] = useState(
    defaultValue || ""
  );
  const [uncontrolledOpen, setUncontrolledOpen] = useState(false);
  const [selectedLabel, setSelectedLabel] = useState("");

  const value = controlledValue ?? uncontrolledValue;
  const open = controlledOpen ?? uncontrolledOpen;

  const handleValueChange = (newValue: string) => {
    if (onValueChange) {
      onValueChange(newValue);
    }
    setUncontrolledValue(newValue);
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (onOpenChange) {
      onOpenChange(newOpen);
    }
    setUncontrolledOpen(newOpen);
  };

  return (
    <SelectContext.Provider
      value={{
        value,
        onValueChange: handleValueChange,
        open,
        setOpen: handleOpenChange,
        label: selectedLabel,
        setLabel: setSelectedLabel,
      }}
    >
      <View>{children}</View>
    </SelectContext.Provider>
  );
}

function SelectGroup({
  className,
  children,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View className={cn("p-1", className)} {...props}>
      {children}
    </View>
  );
}

function SelectValue({
  className,
  placeholder,
  ...props
}: {
  className?: string;
  placeholder?: string;
} & React.ComponentProps<typeof Text>) {
  const { value, label } = useSelect();

  return (
    <Text
      className={cn(
        "text-sm native:text-base text-foreground",
        !value && "text-muted-foreground",
        className
      )}
      numberOfLines={1}
      {...props}
    >
      {value ? label || value : placeholder}
    </Text>
  );
}

function SelectTrigger({
  className,
  children,
  ...props
}: React.ComponentProps<typeof TouchableOpacity>) {
  const { open, setOpen } = useSelect();

  return (
    <TouchableOpacity
      activeOpacity={0.7}
      onPress={() => setOpen(!open)}
      className={cn(
        "flex flex-row h-12 items-center justify-between rounded-md border border-input bg-background px-3 py-2",
        open ? "opacity-100" : "opacity-100",
        className
      )}
      {...props}
    >
      {children}
      <ChevronDown size={16} className="opacity-50" color="gray" />
    </TouchableOpacity>
  );
}

function SelectContent({
  className,
  children,
  position = "popper",
  style,
  ...props
}: { position?: "popper" | "item-aligned" } & React.ComponentProps<
  typeof View
>) {
  const { open, setOpen } = useSelect();
  const { colors } = useTheme();

  if (!open) return null;

  return (
    <Modal
      transparent
      animationType="fade"
      visible={open}
      onRequestClose={() => setOpen(false)}
    >
      <TouchableWithoutFeedback onPress={() => setOpen(false)}>
        <View className="flex-1 justify-center items-center bg-black/50 p-4">
          <TouchableWithoutFeedback>
            <View
              className={cn(
                "relative z-50 min-w-[8rem] w-full max-w-xs overflow-hidden rounded-md border shadow-md",
                className
              )}
              style={[{ backgroundColor: colors.bg, borderColor: colors.border }, style]}
              {...props}
            >
              <ScrollView
                className="max-h-[300px]"
                showsVerticalScrollIndicator={false}
              >
                <View className="p-1">{children}</View>
              </ScrollView>
            </View>
          </TouchableWithoutFeedback>
        </View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

function SelectLabel({
  className,
  ...props
}: React.ComponentProps<typeof Text>) {
  return (
    <Text
      className={cn(
        "py-1.5 pl-8 pr-2 text-sm font-semibold text-muted-foreground",
        className
      )}
      {...props}
    />
  );
}

interface SelectItemProps extends React.ComponentProps<
  typeof TouchableOpacity
> {
  value: string;
  label: string;
}

function SelectItem({
  className,
  children,
  value,
  label,
  ...props
}: SelectItemProps) {
  const {
    value: selectedValue,
    onValueChange,
    setOpen,
    setLabel,
  } = useSelect();
  const { colors } = useTheme();
  const isSelected = selectedValue === value;

  useEffect(() => {
    if (isSelected) {
      setLabel(label);
    }
  }, [isSelected, label, setLabel]);

  const handlePress = () => {
    if (onValueChange) {
      onValueChange(value);
      setLabel(label);
    }
    setOpen(false);
  };

  return (
    <TouchableOpacity
      className={cn(
        "relative flex flex-row w-full cursor-default select-none items-center rounded-sm py-2.5 pl-8 pr-2 outline-none",
        className
      )}
      style={isSelected ? { backgroundColor: `${colors.accent}20` } : undefined}
      onPress={handlePress}
      {...props}
    >
      <View className="absolute left-2 flex h-3.5 w-3.5 items-center justify-center">
        {isSelected && <Check size={16} color={colors.textPrimary} />}
      </View>
      <Text className="text-sm native:text-base" style={{ color: colors.textPrimary }}>
        {children}
      </Text>
    </TouchableOpacity>
  );
}

function SelectSeparator({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View className={cn("-mx-1 my-1 h-[1px] bg-muted", className)} {...props} />
  );
}

export {
  Select,
  SelectGroup,
  SelectValue,
  SelectTrigger,
  SelectContent,
  SelectLabel,
  SelectItem,
  SelectSeparator,
};
