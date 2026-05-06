import { cn } from "@/utils/cn";
import { createContext, useContext } from "react";
import { View, Text, Platform, ViewProps } from "react-native";

export type ChartConfig = {
  [k in string]: {
    label?: string;
    icon?: React.ComponentType;
    color?: string;
  };
};

type ChartContextProps = {
  config: ChartConfig;
};

const ChartContext = createContext<ChartContextProps | null>(null);

function useChart() {
  const context = useContext(ChartContext);
  if (!context) {
    throw new Error("useChart must be used within a <ChartContainer />");
  }
  return context;
}

export function ChartContainer({
  children,
  config,
  className,
  ...props
}: ViewProps & {
  config: ChartConfig;
  className?: string;
}) {
  return (
    <ChartContext.Provider value={{ config }}>
      <View className={cn("w-full p-2.5 bg-transparent", className)} {...props}>
        {children}
      </View>
    </ChartContext.Provider>
  );
}

export function ChartTooltipContent({
  items,
  hideLabel = false,
  hideIndicator = false,
  indicator = "dot",
  className,
}: {
  items?: { value: number; label?: string; color?: string; text?: string }[];
  hideLabel?: boolean;
  hideIndicator?: boolean;
  indicator?: "line" | "dot" | "dashed";
  className?: string;
}) {
  const { config } = useChart();

  if (!items || items.length === 0) return null;

  return (
    <View
      className={cn(
        "bg-white rounded-lg p-2.5 border border-[#e5e7eb] min-w-[120px] shadow-sm shadow-black/10",
        className
      )}
    >
      {items.map((item, index) => {
        const configKey = item.label || "";
        const configItem = config[configKey];
        const label = configItem?.label || item.label || "Value";
        const color = item.color || configItem?.color || "#000";

        return (
          <View key={index} className="flex-row items-center mb-1">
            {!hideIndicator && (
              <View
                className={cn(
                  "rounded-sm",
                  indicator === "dot" && "w-2.5 h-2.5 rounded-full",
                  indicator === "line" && "w-1 h-2.5"
                )}
                style={{ backgroundColor: color }}
              />
            )}
            <View className="flex-row justify-between flex-1 ml-2">
              {!hideLabel && (
                <Text className="text-xs text-[#6b7280] mr-2">{label}</Text>
              )}
              <Text
                className={cn(
                  "text-xs font-bold text-black",
                  Platform.OS === "ios" ? "font-[Courier]" : "font-mono"
                )}
              >
                {item.value}
              </Text>
            </View>
          </View>
        );
      })}
    </View>
  );
}

export function ChartLegendContent({
  keys,
  className,
}: {
  keys: string[];
  className?: string;
}) {
  const { config } = useChart();

  return (
    <View
      className={cn("flex-row flex-wrap justify-center mt-4 gap-3", className)}
    >
      {keys.map((key) => {
        const item = config[key];
        if (!item) return null;

        return (
          <View key={key} className="flex-row items-center">
            <View className="w-2.5 h-2.5 rounded-full" />
            <Text className="text-xs text-[#374151] ml-1.5">{item.label}</Text>
          </View>
        );
      })}
    </View>
  );
}
