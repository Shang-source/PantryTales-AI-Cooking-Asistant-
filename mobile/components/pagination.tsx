import {
  View,
  Text,
  TouchableOpacity,
} from "react-native";
import { ChevronLeft, ChevronRight, MoreHorizontal } from "lucide-react-native";
import { cn } from "../utils/cn";

function Pagination({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View
      role="navigation"
      aria-label="pagination"
      className={cn("mx-auto flex-row w-full justify-center", className)}
      {...props}
    />
  );
}

function PaginationContent({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View className={cn("flex-row items-center gap-1", className)} {...props} />
  );
}

function PaginationItem({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return <View className={cn("", className)} {...props} />;
}

// Define the properties of Link
type PaginationLinkProps = {
  isActive?: boolean;
  size?: "default" | "icon" | "sm" | "lg";
} & React.ComponentProps<typeof TouchableOpacity>;

function PaginationLink({
  className,
  isActive,
  size = "icon",
  children,
  ...props
}: PaginationLinkProps) {
  return (
    <TouchableOpacity
      activeOpacity={0.7}
      accessibilityRole="button"
      accessibilityState={{ selected: isActive }}
      className={cn(
        // Basic button styles
        "items-center justify-center rounded-md flex-row",
        // State styles (simulate Ghost and Outline)
        isActive
          ? "bg-white border border-gray-200 dark:bg-gray-800 dark:border-gray-800" // Outline / Active
          : "bg-transparent", // Ghost
        // Size styles
        size === "icon" && "h-10 w-10",
        size === "default" && "h-10 px-4 py-2",
        size === "sm" && "h-9 rounded-md px-3",
        size === "lg" && "h-11 rounded-md px-8",
        className
      )}
      {...props}
    >
      {/* Automatically handle text styles */}
      {typeof children === "string" || typeof children === "number" ? (
        <Text
          className={cn(
            "text-sm font-medium",
            isActive
              ? "text-gray-900 dark:text-gray-100"
              : "text-gray-500 dark:text-gray-400"
          )}
        >
          {children}
        </Text>
      ) : (
        children
      )}
    </TouchableOpacity>
  );
}

function PaginationPrevious({
  className,
  ...props
}: React.ComponentProps<typeof PaginationLink>) {
  return (
    <PaginationLink
      aria-label="Go to previous page"
      size="default"
      className={cn("gap-1 px-2.5", className)}
      {...props}
    >
      <ChevronLeft size={16} className="text-gray-900 dark:text-gray-100" />
      <Text className="text-sm font-medium text-gray-900 dark:text-gray-100 hidden sm:block">
        Previous
      </Text>
    </PaginationLink>
  );
}

function PaginationNext({
  className,
  ...props
}: React.ComponentProps<typeof PaginationLink>) {
  return (
    <PaginationLink
      aria-label="Go to next page"
      size="default"
      className={cn("gap-1 px-2.5", className)}
      {...props}
    >
      <Text className="text-sm font-medium text-gray-900 dark:text-gray-100 hidden sm:block">
        Next
      </Text>
      <ChevronRight size={16} className="text-gray-900 dark:text-gray-100" />
    </PaginationLink>
  );
}

function PaginationEllipsis({
  className,
  ...props
}: React.ComponentProps<typeof View>) {
  return (
    <View
      className={cn("flex h-9 w-9 items-center justify-center", className)}
      {...props}
    >
      <MoreHorizontal size={16} className="text-gray-900 dark:text-gray-100" />
      <Text className="sr-only">More pages</Text>
    </View>
  );
}

export {
  Pagination,
  PaginationContent,
  PaginationLink,
  PaginationItem,
  PaginationPrevious,
  PaginationNext,
  PaginationEllipsis,
};
