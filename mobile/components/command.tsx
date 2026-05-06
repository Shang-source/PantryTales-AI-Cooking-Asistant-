import { useState, createContext, useContext } from "react";
import {
  View,
  TextInput,
  Text,
  Pressable,
  Modal,
  ScrollView,
  ViewProps,
  TextInputProps,
  Platform,
  PressableProps,
} from "react-native";
import { Search } from "lucide-react-native";
import { cn } from "@/utils/cn";

interface CommandContextValue {
  search: string;
  setSearch: (text: string) => void;
  hide: () => void;
}
const CommandContext = createContext<CommandContextValue | undefined>(
  undefined
);

interface CommandProps extends ViewProps {
  children: React.ReactNode;
  shouldFilter?: boolean;
  className?: string;
}

function Command({
  children,
  className,
  shouldFilter = true,
  ...props
}: CommandProps) {
  const [search, setSearch] = useState("");
  const hide = () => {};

  return (
    <CommandContext.Provider value={{ search, setSearch, hide }}>
      <View
        className={cn("flex-1 bg-white rounded-lg overflow-hidden", className)}
        {...props}
      >
        {children}
      </View>
    </CommandContext.Provider>
  );
}

interface CommandDialogProps extends ViewProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  children: React.ReactNode;
  className?: string;
}

function CommandDialog({
  open,
  onOpenChange,
  children,
  className,
  ...props
}: CommandDialogProps) {
  return (
    <Modal
      visible={open}
      transparent
      animationType="fade"
      onRequestClose={() => onOpenChange(false)}
    >
      <Pressable
        className="flex-1 bg-black/50 justify-center p-5 pt-[100px]"
        onPress={() => onOpenChange(false)}
      >
        <Pressable
          className={cn(
            "bg-white rounded-xl max-h-[80%] shadow-lg shadow-black/25",
            className
          )}
          onPress={(e) => e.stopPropagation()}
        >
          <Command className="flex-1">{children}</Command>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

interface CommandInputProps extends TextInputProps {
  className?: string;
}

function CommandInput({ className, ...props }: CommandInputProps) {
  const context = useContext(CommandContext);
  if (!context) throw new Error("CommandInput must be used within Command");

  return (
    <View className="flex-row items-center border-b border-[#eee] px-3 h-[50px]">
      <Search size={18} color="#888" className="mr-2" />
      <TextInput
        className={cn("flex-1 text-base h-full text-[#333]", className)}
        placeholderTextColor="#888"
        value={context.search}
        onChangeText={context.setSearch}
        autoFocus={Platform.OS !== "web"}
        {...props}
      />
    </View>
  );
}

interface CommandListProps extends ViewProps {
  className?: string;
}

function CommandList({ children, className, ...props }: CommandListProps) {
  return (
    <ScrollView
      className={cn("flex-1", className)}
      keyboardShouldPersistTaps="handled"
      {...props}
    >
      {children}
    </ScrollView>
  );
}

interface CommandEmptyProps extends ViewProps {
  className?: string;
}

function CommandEmpty({ children, className, ...props }: CommandEmptyProps) {
  return (
    <View className={cn("p-6 items-center", className)} {...props}>
      {typeof children === "string" ? (
        <Text className="text-[#888] text-sm">{children}</Text>
      ) : (
        children
      )}
    </View>
  );
}

interface CommandGroupProps extends ViewProps {
  heading?: string;
  className?: string;
}

function CommandGroup({
  heading,
  children,
  className,
  ...props
}: CommandGroupProps) {
  return (
    <View className={cn("py-1", className)} {...props}>
      {heading && (
        <Text className="text-xs text-[#888] px-3 py-1 font-semibold">
          {heading}
        </Text>
      )}
      {children}
    </View>
  );
}

interface CommandItemProps extends PressableProps {
  onSelect?: () => void;
  value?: string;
  children?: React.ReactNode;
  className?: string;
}

function CommandItem({
  children,
  className,
  onSelect,
  value,
  ...props
}: CommandItemProps) {
  const context = useContext(CommandContext);

  if (context && value && context.search) {
    const searchText = context.search.toLowerCase();
    const itemValue = value.toLowerCase();
    if (!itemValue.includes(searchText)) {
      return null;
    }
  }

  return (
    <Pressable
      className={cn(
        "flex-row items-center px-3 py-2.5 gap-2 active:bg-[#F3F4F6]",
        className
      )}
      onPress={onSelect}
      {...props}
    >
      {typeof children === "string" ? (
        <Text className="text-sm text-[#333]">{children}</Text>
      ) : (
        children
      )}
    </Pressable>
  );
}

function CommandSeparator({
  className,
  ...props
}: ViewProps & { className?: string }) {
  return (
    <View className={cn("h-[1px] bg-[#eee] my-1", className)} {...props} />
  );
}

export {
  Command,
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
  CommandSeparator,
};
