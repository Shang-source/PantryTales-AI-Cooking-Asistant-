import { cn } from "@/utils/cn";
import { forwardRef } from "react";
import { ScrollView, ScrollViewProps, View, ViewProps } from "react-native";

interface ScrollAreaProps extends ScrollViewProps {
  className?: string;
  contentContainerClassName?: string;
  orientation?: "vertical" | "horizontal";
}

const ScrollArea = forwardRef<ScrollView, ScrollAreaProps>(
  (
    {
      className,
      contentContainerClassName,
      children,
      orientation = "vertical",
      ...props
    },
    ref
  ) => {
    const isHorizontal = orientation === "horizontal";

    return (
      <View className={cn("overflow-hidden", className)}>
        <ScrollView
          ref={ref}
          horizontal={isHorizontal}
          nestedScrollEnabled={true}
          showsVerticalScrollIndicator={
            !isHorizontal && props.showsVerticalScrollIndicator !== false
          }
          showsHorizontalScrollIndicator={
            isHorizontal && props.showsHorizontalScrollIndicator !== false
          }
          className="flex-1 w-full"
          contentContainerClassName={cn(
            isHorizontal ? "flex-row" : "flex-col",
            contentContainerClassName
          )}
          {...props}
        >
          {children}
        </ScrollView>
      </View>
    );
  }
);
ScrollArea.displayName = "ScrollArea";

const ScrollBar = forwardRef<
  View,
  ViewProps & { orientation?: "vertical" | "horizontal" }
>((props, ref) => null);
ScrollBar.displayName = "ScrollBar";

export { ScrollArea, ScrollBar };
