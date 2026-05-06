import { Platform, TextInput, TextInputProps } from "react-native";
import { cn } from "../utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

type InputProps = TextInputProps & {
  className?: string;
};

function Input({ className, style, ...props }: InputProps) {
  const { colors } = useTheme();

  return (
    <TextInput
      data-slot="input"
      className={cn(
        "disabled:opacity-50",
        className
      )}
      style={[
        {
          borderWidth: 1,
          borderColor: colors.border,
          borderRadius: 6,
          paddingHorizontal: 12,
          paddingVertical: Platform.OS === "ios" ? 12 : 10,
          fontSize: 16,
          lineHeight: 20,
          backgroundColor: colors.card,
          color: colors.textPrimary,
          textAlignVertical: "center",
        },
        style,
      ]}
      placeholderTextColor={props.placeholderTextColor ?? colors.textSecondary}
      {...props}
    />
  );
}

export { Input };
