import React from "react";
import { View, TouchableOpacity } from "react-native";
import { Circle } from "lucide-react-native";
import { cn } from "../utils/cn";

// --- Context ---
interface RadioGroupContextValue {
  value: string | undefined;
  onValueChange: (value: string) => void;
}
const RadioGroupContext = React.createContext<RadioGroupContextValue | null>(
  null
);

// --- Components ---

function RadioGroup({
  className,
  value,
  onValueChange,
  children,
  ...props
}: React.ComponentProps<typeof View> & {
  value?: string;
  onValueChange?: (value: string) => void;
}) {
  return (
    <RadioGroupContext.Provider
      value={{
        value,
        onValueChange: onValueChange || (() => {}),
      }}
    >
      <View className={cn("gap-2", className)} {...props}>
        {children}
      </View>
    </RadioGroupContext.Provider>
  );
}

function RadioGroupItem({
  className,
  value,
  disabled,
  ...props
}: React.ComponentProps<typeof TouchableOpacity> & { value: string }) {
  const context = React.useContext(RadioGroupContext);

  if (!context) {
    throw new Error("RadioGroupItem must be used within RadioGroup");
  }

  const isChecked = context.value === value;

  return (
    <TouchableOpacity
      activeOpacity={0.7}
      disabled={disabled}
      onPress={() => context.onValueChange(value)}
      accessibilityRole="radio"
      accessibilityState={{ checked: isChecked, disabled }}
      className={cn(
        // Base styles: round, border
        "aspect-square h-5 w-5 rounded-full border items-center justify-center",
        // Checked state styles
        isChecked
          ? "border-gray-900 dark:border-gray-100 text-gray-900 dark:text-gray-100" // Primary color
          : "border-gray-300 dark:border-gray-600",
        // Disabled styles
        disabled && "opacity-50",
        className
      )}
      {...props}
    >
      {isChecked && (
        <View className="flex items-center justify-center">
          {/* Solid circle indicator */}
          <Circle
            size={10}
            className="fill-current text-current"
            fill="currentColor"
          />
        </View>
      )}
    </TouchableOpacity>
  );
}

export { RadioGroup, RadioGroupItem };
