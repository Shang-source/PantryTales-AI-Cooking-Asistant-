import { View, Text, TouchableOpacity } from "react-native";
import { Feather } from "@expo/vector-icons";
import { cn } from "@/utils/cn";

interface WithClassName {
  className?: string;
}

type FeatherIconProps = Omit<React.ComponentProps<typeof Feather>, "name">;

const RN_ChevronRight = (props: FeatherIconProps) => (
  <Feather name="chevron-right" size={14} color="#D4A5A5" {...props} />
);

const RN_MoreHorizontal = (props: FeatherIconProps) => (
  <Feather name="more-horizontal" size={16} color="#D4A5A5" {...props} />
);

function Breadcrumb({
  className,
  ...props
}: React.ComponentProps<typeof View> & WithClassName) {
  return (
    <View aria-label="breadcrumb" className={cn("", className)} {...props} />
  );
}

function BreadcrumbList({
  className,
  ...props
}: React.ComponentProps<typeof View> & WithClassName) {
  return (
    <View
      className={cn(
        "flex-row flex-wrap items-center gap-1.5 text-[#6c757d]",
        className
      )}
      {...props}
    />
  );
}

function BreadcrumbItem({
  className,
  ...props
}: React.ComponentProps<typeof View> & WithClassName) {
  return (
    <View
      className={cn("flex-row items-center gap-1.5", className)}
      {...props}
    />
  );
}

function BreadcrumbLink({
  asChild = false,
  className,
  children,
  ...props
}: React.ComponentProps<typeof TouchableOpacity> &
  WithClassName & { asChild?: boolean }) {
  return (
    <TouchableOpacity
      activeOpacity={0.7}
      className={cn("transition-opacity", className)}
      {...props}
    >
      <Text className="text-[#adb5bd] font-medium">{children}</Text>
    </TouchableOpacity>
  );
}

function BreadcrumbPage({
  className,
  ...props
}: React.ComponentProps<typeof Text> & WithClassName) {
  return (
    <Text
      role="link"
      className={cn("text-[#212529] font-normal", className)}
      {...props}
    />
  );
}

function BreadcrumbSeparator({
  children = null,
  className,
  ...props
}: React.ComponentProps<typeof View> & WithClassName) {
  return (
    <View role="presentation" className={cn("", className)} {...props}>
      {children ?? <RN_ChevronRight />}
    </View>
  );
}

function BreadcrumbEllipsis({
  className,
  ...props
}: React.ComponentProps<typeof View> & WithClassName) {
  return (
    <View
      role="presentation"
      className={cn("flex-row items-center justify-center w-9 h-9", className)}
      {...props}
    >
      <RN_MoreHorizontal />
    </View>
  );
}

export {
  Breadcrumb,
  BreadcrumbList,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbPage,
  BreadcrumbSeparator,
  BreadcrumbEllipsis,
};
