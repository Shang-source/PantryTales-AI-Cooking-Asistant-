import { useState, useRef, useContext, createContext } from "react";
import { View, PanResponder, ViewProps, LayoutChangeEvent } from "react-native";
import { GripVertical, GripHorizontal } from "lucide-react-native";
import { cn } from "@/utils/cn";

type ResizableContextType = {
  direction: "horizontal" | "vertical";
  panelSize: number;
  setPanelSize: (size: number) => void;
  containerSize: number;
  setContainerSize: (size: number) => void;
  isDragging: boolean;
  setIsDragging: (dragging: boolean) => void;
};

const ResizableContext = createContext<ResizableContextType | null>(null);

interface ResizablePanelGroupProps extends ViewProps {
  direction?: "horizontal" | "vertical";
  initialRatio?: number;
  className?: string;
}

function ResizablePanelGroup({
  children,
  direction = "horizontal",
  initialRatio = 0.5,
  className,
  ...props
}: ResizablePanelGroupProps) {
  const [panelSize, setPanelSize] = useState(initialRatio);
  const [containerSize, setContainerSize] = useState(0);
  const [isDragging, setIsDragging] = useState(false);

  const onLayout = (e: LayoutChangeEvent) => {
    const { width, height } = e.nativeEvent.layout;
    const size = direction === "horizontal" ? width : height;
    if (Math.abs(containerSize - size) > 1) {
      setContainerSize(size);
    }
  };

  return (
    <ResizableContext.Provider
      value={{
        direction,
        panelSize,
        setPanelSize,
        containerSize,
        setContainerSize,
        isDragging,
        setIsDragging,
      }}
    >
      <View
        onLayout={onLayout}
        className={cn(
          "flex-1 w-full h-full overflow-hidden",
          direction === "vertical" ? "flex-col" : "flex-row",
          className
        )}
        {...props}
      >
        {children}
      </View>
    </ResizableContext.Provider>
  );
}

interface ResizablePanelProps extends ViewProps {
  defaultSize?: number;
  order?: "first" | "second";
  className?: string;
}

function ResizablePanel({
  className,
  order = "first",
  children,
  ...props
}: ResizablePanelProps) {
  const context = useContext(ResizableContext);
  if (!context)
    throw new Error("ResizablePanel must be used within ResizablePanelGroup");

  const { panelSize } = context;
  const flexValue = order === "first" ? panelSize : 1 - panelSize;

  return (
    <View
      className={cn("overflow-hidden", className)}
      // Dynamic flex value must be an inline style as it changes every frame during drag
      style={{ flex: flexValue }}
      {...props}
    >
      {children}
    </View>
  );
}

interface ResizableHandleProps extends ViewProps {
  withHandle?: boolean;
  className?: string;
}

function ResizableHandle({
  withHandle,
  className,
  ...props
}: ResizableHandleProps) {
  const context = useContext(ResizableContext);
  if (!context)
    throw new Error("ResizableHandle must be used within ResizablePanelGroup");

  const {
    direction,
    setPanelSize,
    containerSize,
    panelSize,
    setIsDragging,
    isDragging,
  } = context;

  const valuesRef = useRef({
    containerSize,
    panelSize,
    direction,
  });

  valuesRef.current = { containerSize, panelSize, direction };

  const startSizeRef = useRef(0);

  const panResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponder: () => true,

      onPanResponderGrant: () => {
        setIsDragging(true);
        startSizeRef.current = valuesRef.current.panelSize;
      },

      onPanResponderMove: (_, gestureState) => {
        const {
          containerSize: currentContainerSize,
          direction: currentDirection,
        } = valuesRef.current;

        if (currentContainerSize === 0) return;

        const delta =
          currentDirection === "horizontal" ? gestureState.dx : gestureState.dy;
        const deltaRatio = delta / currentContainerSize;
        let newSize = startSizeRef.current + deltaRatio;

        newSize = Math.max(0.1, Math.min(0.9, newSize));

        setPanelSize(newSize);
      },

      onPanResponderRelease: () => {
        setIsDragging(false);
      },

      onPanResponderTerminate: () => {
        setIsDragging(false);
      },
    })
  ).current;

  const Icon = direction === "vertical" ? GripHorizontal : GripVertical;

  return (
    <View
      className={cn(
        "bg-[#E5E7EB] items-center justify-center z-50",
        direction === "horizontal"
          ? "w-5 h-full -mx-2.5 border-l border-l-black/5"
          : "h-5 w-full -my-2.5",
        isDragging && "bg-blue-500/20",
        className
      )}
      {...panResponder.panHandlers}
      {...props}
    >
      {withHandle && (
        <View className="bg-white border border-[#E5E7EB] rounded w-4 h-5 items-center justify-center shadow-sm shadow-black/10">
          <Icon size={16} color="#9CA3AF" />
        </View>
      )}
    </View>
  );
}

export { ResizablePanelGroup, ResizablePanel, ResizableHandle };
