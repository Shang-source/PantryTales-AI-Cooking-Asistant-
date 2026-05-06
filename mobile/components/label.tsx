import { cn } from "../utils/cn";
import { Text, TextProps } from "react-native";

type LabelProps = TextProps & {
  className?: string;
};

function Label({ className, children, ...props }: LabelProps) {
  return (
    <Text
      data-slot="label"
      className={cn(
        "flex items-center gap-2 text-sm leading-none font-medium select-none group-data-[disabled=true]:pointer-events-none group-data-[disabled=true]:opacity-50 peer-disabled:cursor-not-allowed peer-disabled:opacity-50",
        className
      )}
      {...props}
    >
      {children}
    </Text>
  );
}

export { Label };
