import { View, Text, ScrollView, ViewProps, TextProps } from "react-native";
import { forwardRef } from "react";
import { cn } from "@/utils/cn";

const Table = forwardRef<
  ScrollView,
  React.ComponentPropsWithoutRef<typeof ScrollView>
>(({ className, children, ...props }, ref) => (
  <View className="w-full overflow-hidden">
    <ScrollView
      ref={ref}
      horizontal
      showsHorizontalScrollIndicator={true}
      contentContainerClassName="min-w-full"
      className={cn("w-full", className)}
      {...props}
    >
      <View className="w-full min-w-full">{children}</View>
    </ScrollView>
  </View>
));
Table.displayName = "Table";

const TableHeader = forwardRef<View, ViewProps>(
  ({ className, ...props }, ref) => (
    <View
      ref={ref}
      className={cn("border-b border-gray-200 dark:border-gray-800", className)}
      {...props}
    />
  )
);
TableHeader.displayName = "TableHeader";

const TableBody = forwardRef<View, ViewProps>(
  ({ className, ...props }, ref) => (
    <View ref={ref} className={cn("flex-col", className)} {...props} />
  )
);
TableBody.displayName = "TableBody";

const TableFooter = forwardRef<View, ViewProps>(
  ({ className, ...props }, ref) => (
    <View
      ref={ref}
      className={cn(
        "flex-row bg-gray-100/50 border-t border-gray-200 font-medium dark:bg-gray-800/50 dark:border-gray-800",
        className
      )}
      {...props}
    />
  )
);
TableFooter.displayName = "TableFooter";

const TableRow = forwardRef<View, ViewProps>(({ className, ...props }, ref) => (
  <View
    ref={ref}
    className={cn(
      "flex-row border-b border-gray-200 dark:border-gray-800 active:bg-gray-100/50 dark:active:bg-gray-800/50",
      className
    )}
    {...props}
  />
));
TableRow.displayName = "TableRow";

const TableHead = forwardRef<View, ViewProps & { textClassName?: string }>(
  ({ className, textClassName, children, ...props }, ref) => (
    <View
      ref={ref}
      className={cn("h-12 px-4 justify-center items-start", className)}
      {...props}
    >
      {typeof children === "string" ? (
        <Text
          className={cn(
            "text-sm font-medium text-gray-500 dark:text-gray-400 text-left",
            textClassName
          )}
        >
          {children}
        </Text>
      ) : (
        children
      )}
    </View>
  )
);
TableHead.displayName = "TableHead";

const TableCell = forwardRef<View, ViewProps & { textClassName?: string }>(
  ({ className, textClassName, children, ...props }, ref) => (
    <View
      ref={ref}
      className={cn("p-4 justify-center align-middle", className)}
      {...props}
    >
      {typeof children === "string" ? (
        <Text
          className={cn(
            "text-sm text-gray-900 dark:text-gray-100",
            textClassName
          )}
        >
          {children}
        </Text>
      ) : (
        children
      )}
    </View>
  )
);
TableCell.displayName = "TableCell";

const TableCaption = forwardRef<Text, TextProps>(
  ({ className, ...props }, ref) => (
    <Text
      ref={ref}
      className={cn(
        "mt-4 text-sm text-gray-500 text-center dark:text-gray-400",
        className
      )}
      {...props}
    />
  )
);
TableCaption.displayName = "TableCaption";

export {
  Table,
  TableHeader,
  TableBody,
  TableFooter,
  TableHead,
  TableRow,
  TableCell,
  TableCaption,
};
