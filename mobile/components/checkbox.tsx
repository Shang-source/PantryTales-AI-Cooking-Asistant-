import { Pressable } from 'react-native';
import { Check } from 'lucide-react-native';
import { cn } from '@/utils/cn';
import { useTheme } from '@/contexts/ThemeContext';

interface CheckboxProps {
  checked: boolean;      
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  className?: string;
  testID?: string;
}

export function Checkbox({ checked, onChange, disabled, className, testID }: CheckboxProps) {
  const { colors } = useTheme();
  
  return (
    <Pressable
      testID={testID || "checkbox-button"}
      onPress={() => !disabled && onChange(!checked)}
      className={cn(
        "w-5 h-5 rounded border justify-center items-center",
        disabled && "opacity-50",
        className
      )}
      style={{
        backgroundColor: checked ? colors.accent : colors.card,
        borderColor: checked ? colors.accent : colors.border,
      }}
    >
      {checked && <Check size={14} color={colors.bg} strokeWidth={3} />}
    </Pressable>
  );
}
