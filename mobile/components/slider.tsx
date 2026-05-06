import {
  View,
  PanResponder,
  GestureResponderEvent,
  PanResponderGestureState,
  LayoutChangeEvent,
  Dimensions,
} from "react-native";
import { cn } from "@/utils/cn";
import { useEffect, useMemo, useRef, useState } from "react";

const screenWidth = Dimensions.get("window").width;
const HORIZONTAL_PADDING = 24 * 2;
const initialGuessWidth = screenWidth - HORIZONTAL_PADDING;
const THUMB_CONTAINER_SIZE = 40;
const THUMB_OFFSET = THUMB_CONTAINER_SIZE / 2;

interface SliderProps extends React.ComponentPropsWithoutRef<typeof View> {
  min?: number;
  max?: number;
  step?: number;
  value?: number | number[];
  defaultValue?: number | number[];
  onValueChange?: (value: number[]) => void;
  disabled?: boolean;
}

function Slider({
  className,
  min = 0,
  max = 100,
  step = 1,
  value: valueProp,
  defaultValue,
  onValueChange,
  disabled = false,
  ...props
}: SliderProps) {
  const [trackWidth, setTrackWidth] = useState(
    initialGuessWidth > 0 ? initialGuessWidth : 0
  );

  const initialValue = useMemo(() => {
    if (typeof valueProp !== "undefined") {
      return Array.isArray(valueProp) ? valueProp[0] : valueProp;
    }
    if (typeof defaultValue !== "undefined") {
      return Array.isArray(defaultValue) ? defaultValue[0] : defaultValue;
    }
    return min;
  }, [valueProp, defaultValue, min]);

  const [internalValue, setInternalValue] = useState(initialValue);

  const value =
    typeof valueProp !== "undefined"
      ? Array.isArray(valueProp)
        ? valueProp[0]
        : valueProp
      : internalValue;

  const percentage = useMemo(() => {
    const range = max - min;
    if (range === 0) return 0;
    return (value - min) / range;
  }, [value, min, max]);

  const currentPropsRef = useRef({
    value,
    onValueChange,
    trackWidth,
    min,
    max,
    step,
  });

  const startValueRef = useRef(value);

  useEffect(() => {
    currentPropsRef.current = {
      value,
      onValueChange,
      trackWidth,
      min,
      max,
      step,
    };
  }, [value, onValueChange, trackWidth, min, max, step]);

  const panResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => !disabled,
      onMoveShouldSetPanResponder: () => !disabled,

      onStartShouldSetPanResponderCapture: () => !disabled,
      onMoveShouldSetPanResponderCapture: (
        _: GestureResponderEvent,
        gestureState: PanResponderGestureState
      ) => {
        return (
          !disabled && Math.abs(gestureState.dx) > Math.abs(gestureState.dy)
        );
      },

      onPanResponderGrant: () => {
        startValueRef.current = currentPropsRef.current.value;
      },

      onPanResponderMove: (
        _: GestureResponderEvent,
        gestureState: PanResponderGestureState
      ) => {
        const {
          trackWidth,
          min,
          max,
          step,
          onValueChange,
          value: latestValue,
        } = currentPropsRef.current;

        if (disabled || trackWidth <= 0) {
          return;
        }

        const diffRatio = gestureState.dx / trackWidth;
        const diffValue = diffRatio * (max - min);
        let newValue = startValueRef.current + diffValue;
        newValue = Math.max(min, Math.min(max, newValue));
        if (step > 0) {
          newValue = Math.round(newValue / step) * step;
        }
        if (newValue !== latestValue) {
          onValueChange?.([newValue]);

          if (typeof valueProp === "undefined") {
            setInternalValue(newValue);
          }
        }
      },
      onPanResponderRelease: () => {
      },
    })
  ).current;

  return (
    <View
      testID="slider-root"
      className={cn(
        "relative flex w-full h-10 justify-center touch-none select-none opacity-100",
        disabled && "opacity-50",
        className
      )}
      {...props}
    >
      <View
        onLayout={(e: LayoutChangeEvent) => {
          const width = e.nativeEvent.layout.width;
          if (width > 0 && width !== trackWidth) {
            setTrackWidth(width);
          }
        }}
        className="relative h-2 w-full grow overflow-hidden rounded-full bg-secondary bg-gray-200 dark:bg-gray-800"
      >
        <View
          className="absolute h-full bg-primary bg-black dark:bg-white"
          style={{
            width: `${percentage * 100}%`,
          }}
        />
      </View>
      <View
        testID="slider-thumb"
        className={cn(
          "absolute h-10 w-10",
          "items-center justify-center",
          "left-0",
          disabled ? "pointer-events-none" : "pointer-events-auto"
        )}
        style={{
          transform: [
            {
              translateX: trackWidth * percentage - THUMB_OFFSET,
            },
          ],
          opacity: trackWidth === 0 ? 0 : 1,
        }}
        {...panResponder.panHandlers}
      >
        <View
          className={cn(
            "h-5 w-5 rounded-full border-2 border-primary bg-white dark:bg-black dark:border-white transition-colors"
          )}
        />
      </View>
    </View>
  );
}

export { Slider };
