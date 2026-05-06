import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  View,
  Text,
  TouchableOpacity,
  type ViewProps,
} from "react-native";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";


type ToggleGroupType = "single" | "multiple";
type ToggleVariant = "default" | "outline";
type ToggleSize = "default" | "sm" | "lg";

interface BaseToggleGroupProps extends ViewProps {
  disabled?: boolean;
  variant?: ToggleVariant;
  size?: ToggleSize;
  className?: string;
  children: ReactNode;
}

interface ToggleGroupSingleProps extends BaseToggleGroupProps {
  type?: "single";
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
}

interface ToggleGroupMultipleProps extends BaseToggleGroupProps {
  type: "multiple";
  value?: string[];
  defaultValue?: string[];
  onValueChange?: (value: string[]) => void;
}

export type ToggleGroupProps =
  | ToggleGroupSingleProps
  | ToggleGroupMultipleProps;

export interface ToggleGroupItemProps {
  value: string;
  disabled?: boolean;
  className?: string;
  textClassName?: string;
  children: ReactNode;
}

interface ToggleGroupContextValue {
  type: ToggleGroupType;
  values: string[];
  disabled?: boolean;
  variant: ToggleVariant;
  size: ToggleSize;
  toggleValue: (value: string) => void;
}

const ToggleGroupContext = createContext<ToggleGroupContextValue | null>(null);

function useToggleGroupContext() {
  const ctx = useContext(ToggleGroupContext);
  if (!ctx) {
    throw new Error("ToggleGroupItem must be used within <ToggleGroup />");
  }
  return ctx;
}

export function ToggleGroup(props: ToggleGroupProps) {
  const {
    type,
    value,
    defaultValue,
    onValueChange,
    disabled,
    variant = "default",
    size = "default",
    className,
    children,
    ...viewProps
  } = props;

  const groupType: ToggleGroupType = type === "multiple" ? "multiple" : "single";

  const [internalValues, setInternalValues] = useState<string[]>(() => {
    if (value !== undefined) {
      if (Array.isArray(value)) return value;
      return [value as string];
    }
    if (defaultValue !== undefined) {
      if (Array.isArray(defaultValue)) return defaultValue as string[];
      return [defaultValue as string];
    }
    return [];
  });

  const selectedValues = useMemo(() => {
    if (value !== undefined) {
      if (Array.isArray(value)) return value as string[];
      return value ? [value as string] : [];
    }
    return internalValues;
  }, [value, internalValues]);

  const applyValues = useCallback(
    (next: string[]) => {
      if (groupType === "multiple") {
        (onValueChange as ToggleGroupMultipleProps["onValueChange"])?.(next);
      } else {
        const nextValue = next[0] ?? "";
        (onValueChange as ToggleGroupSingleProps["onValueChange"])?.(nextValue);
      }
      if (value === undefined) {
        setInternalValues(next);
      }
    },
    [groupType, onValueChange, value]
  );

  const toggleValue = useCallback(
    (v: string) => {
      if (disabled) return;

      const isSelected = selectedValues.includes(v);

      if (groupType === "multiple") {
        let next: string[];
        if (isSelected) {
          next = selectedValues.filter((x) => x !== v);
        } else {
          next = [...selectedValues, v];
        }
        applyValues(next);
      } else {
        let next: string[];
        if (isSelected) {
          next = [];
        } else {
          next = [v];
        }
        applyValues(next);
      }
    },
    [disabled, selectedValues, groupType, applyValues]
  );

  const ctxValue = useMemo<ToggleGroupContextValue>(
    () => ({
      type: groupType,
      values: selectedValues,
      disabled,
      variant,
      size,
      toggleValue,
    }),
    [groupType, selectedValues, disabled, variant, size, toggleValue]
  );

  const { colors } = useTheme();

  return (
    <ToggleGroupContext.Provider value={ctxValue}>
      <View
        data-slot="toggle-group"
        className={cn(
          "flex flex-row items-center rounded-md",
          className
        )}
        style={variant === "outline" ? { borderWidth: 1, borderColor: colors.border } : undefined}
        {...viewProps}
      >
        {children}
      </View>
    </ToggleGroupContext.Provider>
  );
}

export function ToggleGroupItem({
  value,
  disabled: itemDisabled,
  className,
  textClassName,
  children,
}: ToggleGroupItemProps) {
  const {
    values,
    toggleValue,
    disabled: groupDisabled,
    variant,
    size,
  } = useToggleGroupContext();
  const { colors } = useTheme();

  const isSelected = values.includes(value);
  const isDisabled = groupDisabled || itemDisabled;
  const isStringChild = typeof children === "string";
  // Check if className requests transparency (let className handle styling)
  const hasTransparentBg = className?.includes("bg-transparent");

  const baseClasses =
    "min-w-0 flex-1 flex-row items-center justify-center";
  const paddingClasses =
    size === "sm" ? "px-2 py-1" : size === "lg" ? "px-4 py-3" : "px-3 py-2";

  const disabledClasses = "opacity-50";

  const containerClassName = cn(
    baseClasses,
    paddingClasses,
    isDisabled && disabledClasses,
    className
  );

  // Calculate styles based on variant and selection state
  // Skip background styling if className has bg-transparent (custom styling)
  const containerStyle = (() => {
    if (hasTransparentBg) {
      // Respect className's transparent background
      return undefined;
    }
    if (variant === "outline") {
      return {
        backgroundColor: isSelected ? colors.accent : "transparent",
        borderColor: colors.border,
      };
    }
    // Default variant with string children - apply standard styling
    if (isStringChild) {
      return {
        backgroundColor: isSelected ? colors.card : colors.accent,
      };
    }
    // Custom children without bg-transparent - apply subtle selection indicator
    return {
      backgroundColor: isSelected ? `${colors.accent}20` : "transparent",
    };
  })();

  const textColor = (() => {
    if (variant === "outline") {
      return isSelected ? colors.bg : colors.textPrimary;
    }
    return isSelected ? colors.textPrimary : colors.bg;
  })();

  const labelClassName = cn("text-sm font-medium", textClassName);

  return (
    <TouchableOpacity
      data-slot="toggle-group-item"
      activeOpacity={0.8}
      disabled={isDisabled}
      onPress={() => toggleValue(value)}
      className={containerClassName}
      style={containerStyle}
    >
      {isStringChild ? (
        <Text className={labelClassName} style={{ color: textColor }}>
          {children}
        </Text>
      ) : (
        children
      )}
    </TouchableOpacity>
  );
}
