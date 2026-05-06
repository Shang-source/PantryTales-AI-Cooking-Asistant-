import React, {
  createContext,
  useContext,
  useMemo,
  useRef,
  useCallback,
} from "react";
import {
  View,
  Text,
  TextInput,
  Pressable,
  StyleSheet,
  type ViewProps,
  type TextProps,
} from "react-native";
import { cn } from "../utils/cn";

type Slot = {
  char: string;
  isActive: boolean;
};

type OTPContextValue = {
  slots: Slot[];
};

const OTPInputContext = createContext<OTPContextValue | null>(null);

type InputOTPProps = ViewProps & {
  value: string;
  onChange: (value: string) => void;
  maxLength?: number;
  containerClassName?: string;
};

function InputOTP({
  value,
  onChange,
  maxLength = 4,
  style,
  className,
  containerClassName,
  children,
  ...props
}: InputOTPProps) {
  const inputRef = useRef<TextInput>(null);

  const slots: Slot[] = useMemo(
    () =>
      Array.from({ length: maxLength }).map((_, index) => {
        const char = value[index] ?? "";
        const isActive = value.length === index;
        return { char, isActive };
      }),
    [value, maxLength]
  );

  const handleChangeText = useCallback(
    (text: string) => {
      const next = text.slice(0, maxLength);
      onChange(next);
    },
    [onChange, maxLength]
  );

  return (
    <OTPInputContext.Provider value={{ slots }}>
      <Pressable
        onPress={() => inputRef.current?.focus()}
        style={[styles.pressableContainer, style]}
        className={cn("flex-row items-center gap-2", containerClassName)}
        {...props}
      >
        <View
          className={cn("flex-row items-center gap-1", className)}
          style={styles.groupContainer}
        >
          {children}
        </View>

        <TextInput
          ref={inputRef}
          value={value}
          onChangeText={handleChangeText}
          keyboardType="number-pad"
          maxLength={maxLength}
          style={styles.hiddenInput}
        />
      </Pressable>
    </OTPInputContext.Provider>
  );
}

// ========= InputOTPGroup =========
type InputOTPGroupProps = ViewProps & {
  className?: string;
};

function InputOTPGroup({ className, style, ...props }: InputOTPGroupProps) {
  return (
    <View
      className={cn("flex-row items-center gap-1", className)}
      style={style}
      {...props}
    />
  );
}

// ========= InputOTPSlot =========
type InputOTPSlotProps = ViewProps & {
  index: number;
  className?: string;
};

function InputOTPSlot({
  index,
  className,
  style,
  ...props
}: InputOTPSlotProps) {
  const ctx = useContext(OTPInputContext);
  const slot = ctx?.slots[index];

  const char = slot?.char ?? "";
  const isActive = !!slot?.isActive;

  return (
    <View
      style={[styles.slot, isActive && styles.slotActive, style]}
      className={cn("items-center justify-center", className)}
      {...props}
    >
      <Text style={styles.slotText}>{char}</Text>
    </View>
  );
}

// ========= InputOTPSeparator =========
type InputOTPSeparatorProps = TextProps & {
  className?: string;
};

function InputOTPSeparator({
  className,
  style,
  ...props
}: InputOTPSeparatorProps) {
  return (
    <Text style={[styles.separator, style]} className={className} {...props}>
      -
    </Text>
  );
}

const styles = StyleSheet.create({
  pressableContainer: {
    alignItems: "center",
  },
  groupContainer: {
    flexDirection: "row",
    alignItems: "center",
  },
  hiddenInput: {
    position: "absolute",
    opacity: 0,
    height: 0,
    width: 0,
  },
  slot: {
    width: 36,
    height: 36,
    borderWidth: 1,
    borderRadius: 6,
    borderColor: "#d4d4d4",
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "white",
  },
  slotActive: {
    borderColor: "#22c55e",
  },
  slotText: {
    fontSize: 16,
    fontWeight: "500",
  },
  separator: {
    marginHorizontal: 4,
    fontSize: 18,
  },
});

export { InputOTP, InputOTPGroup, InputOTPSlot, InputOTPSeparator };
