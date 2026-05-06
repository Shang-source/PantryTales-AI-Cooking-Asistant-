import { useEffect, useRef, useCallback, useState } from "react";
import { View, Text, TouchableOpacity, StyleSheet, LayoutChangeEvent } from "react-native";
import { useFocusEffect } from "expo-router";
import { Lightbulb, ChevronRight } from "lucide-react-native";
import Animated, {
  useSharedValue,
  useAnimatedStyle,
  withTiming,
  withDelay,
  withRepeat,
  withSequence,
  runOnJS,
  Easing,
  cancelAnimation,
} from "react-native-reanimated";
import {
  GestureDetector,
  Gesture,
  GestureHandlerRootView,
} from "react-native-gesture-handler";
import type { CookingTip } from "@/hooks/useCookingTips";
import { Skeleton } from "./skeleton";
import { useTheme } from "@/contexts/ThemeContext";
const DEFAULT_INTERVAL = 5000; // Base interval for short text

/**
 * Calculate the time needed for marquee to fully display text once.
 * Returns 0 if no marquee is needed.
 */
function calculateMarqueeDisplayTime(textWidth: number, containerWidth: number): number {
  if (textWidth <= containerWidth || containerWidth <= 0) {
    return 0; // No marquee needed
  }
  const overflow = textWidth - containerWidth;
  const scrollDuration = Math.max(3000, overflow * 40);
  // Time for: initial wait (2000) + scroll to end (scrollDuration) + reading buffer (1000)
  return 2000 + scrollDuration + 1000;
}

interface CookingTipsTickerProps {
  /** Array of cooking tips to display */
  tips: CookingTip[];
  /** Callback when a tip is pressed */
  onTipPress: (tip: CookingTip) => void;
  /** Whether the tips are loading */
  isLoading?: boolean;
  /** Auto-rotation interval in milliseconds (default: 4000) */
  autoRotateInterval?: number;
}

/**
 * Animated ticker component for displaying cooking tips.
 * Features slide-up animation and swipe gesture support.
 */
export function CookingTipsTicker({
  tips,
  onTipPress,
  isLoading = false,
  autoRotateInterval = DEFAULT_INTERVAL,
}: CookingTipsTickerProps) {
  const { colors } = useTheme();
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isScreenFocused, setIsScreenFocused] = useState(true);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Reset currentIndex to 0 when tips array changes
  useEffect(() => {
    setCurrentIndex(0);
  }, [tips]);

  // Animation values for tip transitions
  const translateY = useSharedValue(0);
  const opacity = useSharedValue(1);

  // Marquee animation values
  const [containerWidth, setContainerWidth] = useState(0);
  const [textWidth, setTextWidth] = useState(0);
  const marqueeX = useSharedValue(0);

  // Handle screen focus/unfocus to pause/resume rotation
  useFocusEffect(
    useCallback(() => {
      setIsScreenFocused(true);
      return () => setIsScreenFocused(false);
    }, [])
  );

  // Simple index incrementer - bounds handled at render time
  const incrementIndex = useCallback(() => {
    setCurrentIndex((prev) => prev + 1);
  }, []);

  // Simple index decrementer - bounds handled at render time
  const decrementIndex = useCallback(() => {
    setCurrentIndex((prev) => prev - 1);
  }, []);

  // Animate to next tip
  const animateToNext = useCallback(() => {
    if (tips.length <= 1) return;

    // Exit animation (slide up and fade out)
    translateY.value = withTiming(-20, {
      duration: 300,
      easing: Easing.in(Easing.ease),
    });
    opacity.value = withTiming(0, { duration: 300 }, () => {
      // After exit animation, increment index on JS thread
      runOnJS(incrementIndex)();

      // Reset position below and animate up
      translateY.value = 20;
      opacity.value = 0;

      translateY.value = withTiming(0, {
        duration: 400,
        easing: Easing.out(Easing.ease),
      });
      opacity.value = withTiming(1, { duration: 400 });
    });
  }, [translateY, opacity, tips.length, incrementIndex]);

  // Animate to previous tip
  const animateToPrev = useCallback(() => {
    if (tips.length <= 1) return;

    // Exit animation (slide down and fade out)
    translateY.value = withTiming(20, {
      duration: 300,
      easing: Easing.in(Easing.ease),
    });
    opacity.value = withTiming(0, { duration: 300 }, () => {
      // After exit animation, decrement index on JS thread
      runOnJS(decrementIndex)();

      // Reset position above and animate down
      translateY.value = -20;
      opacity.value = 0;

      translateY.value = withTiming(0, {
        duration: 400,
        easing: Easing.out(Easing.ease),
      });
      opacity.value = withTiming(1, { duration: 400 });
    });
  }, [translateY, opacity, tips.length, decrementIndex]);

  // Calculate effective interval: max of default and time needed for marquee
  const effectiveInterval = Math.max(
    autoRotateInterval,
    calculateMarqueeDisplayTime(textWidth, containerWidth)
  );

  // Reset and restart timer
  const resetTimer = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }
    if (isScreenFocused && tips.length > 1) {
      timerRef.current = setTimeout(animateToNext, effectiveInterval);
    }
  }, [isScreenFocused, tips.length, effectiveInterval, animateToNext]);

  // Auto-rotation effect
  useEffect(() => {
    if (isScreenFocused && tips.length > 1) {
      timerRef.current = setTimeout(animateToNext, effectiveInterval);
    }

    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, [currentIndex, isScreenFocused, tips.length, effectiveInterval, animateToNext]);

  // Swipe gesture handler
  const swipeGesture = Gesture.Pan()
    .activeOffsetX([-20, 20])
    .onEnd((event) => {
      if (event.translationX < -50) {
        // Swipe left -> next tip
        runOnJS(resetTimer)();
        runOnJS(animateToNext)();
      } else if (event.translationX > 50) {
        // Swipe right -> previous tip
        runOnJS(resetTimer)();
        runOnJS(animateToPrev)();
      }
    });

  // Animated styles for slide-up effect
  const animatedStyle = useAnimatedStyle(() => ({
    transform: [{ translateY: translateY.value }],
    opacity: opacity.value,
  }));

  // Check if marquee is needed
  const needsMarquee = textWidth > containerWidth && containerWidth > 0;

  // Marquee animation effect
  useEffect(() => {
    if (needsMarquee) {
      const overflow = textWidth - containerWidth;
      const duration = Math.max(3000, overflow * 40); // Slower speed for readability

      // Animate: wait -> scroll left -> wait -> scroll back
      marqueeX.value = withRepeat(
        withSequence(
          withDelay(2000, withTiming(-overflow, { duration, easing: Easing.linear })),
          withDelay(2000, withTiming(0, { duration, easing: Easing.linear }))
        ),
        -1, // Infinite repeat
        false
      );
    } else {
      cancelAnimation(marqueeX);
      marqueeX.value = 0;
    }

    return () => {
      cancelAnimation(marqueeX);
    };
  }, [needsMarquee, textWidth, containerWidth, marqueeX]);

  // Reset marquee when tip changes
  useEffect(() => {
    cancelAnimation(marqueeX);
    marqueeX.value = 0;
    setTextWidth(0);
  }, [currentIndex, marqueeX]);

  // Marquee animated style
  const marqueeStyle = useAnimatedStyle(() => ({
    transform: [{ translateX: marqueeX.value }],
  }));

  const handleContainerLayout = useCallback((e: LayoutChangeEvent) => {
    setContainerWidth(e.nativeEvent.layout.width);
  }, []);

  const handleTextLayout = useCallback((e: LayoutChangeEvent) => {
    // Get the actual text width from the hidden measurement text
    setTextWidth(e.nativeEvent.layout.width);
  }, []);

  // Show skeleton while loading
  if (isLoading) {
    return (
      <View style={styles.container}>
        <Skeleton className="h-12 w-full rounded-xl" style={{ backgroundColor: colors.card }} />
      </View>
    );
  }

  // Hide if no tips
  if (tips.length === 0) {
    return null;
  }

  // Safe index calculation - handles overflow and negative indices
  const safeIndex = tips.length > 0
    ? ((currentIndex % tips.length) + tips.length) % tips.length
    : 0;
  const currentTip = tips[safeIndex];

  // Final safety guard
  if (!currentTip) {
    return null;
  }

  return (
    <GestureHandlerRootView style={styles.container}>
      {/* Hidden text for accurate width measurement */}
      <View style={styles.hiddenMeasureContainer}>
        <Text
          style={styles.hiddenMeasureText}
          onLayout={handleTextLayout}
        >
          {currentTip.title.replace(/\n/g, ' ')}
        </Text>
      </View>

      <GestureDetector gesture={swipeGesture}>
        <TouchableOpacity
          activeOpacity={0.8}
          onPress={() => onTipPress(currentTip)}
          style={styles.touchable}
        >
          <View style={[styles.card, { backgroundColor: colors.card, borderColor: colors.border }]}>
            {/* Left side: Icon */}
            <View style={styles.iconContainer}>
              <Lightbulb size={18} color={colors.accent} />
            </View>

            {/* Center: Tip content */}
            <View style={styles.contentContainer}>
              <Text style={[styles.label, { color: colors.accent }]}>Tip:</Text>
              <Animated.View style={[styles.titleWrapper, animatedStyle]}>
                <View style={styles.marqueeContainer} onLayout={handleContainerLayout}>
                  <Animated.View style={[styles.marqueeContent, marqueeStyle, textWidth > 0 && { width: textWidth }]}>
                    <Text style={[styles.title, { color: colors.textPrimary }]}>
                      {currentTip.title.replace(/\n/g, ' ')}
                    </Text>
                  </Animated.View>
                </View>
              </Animated.View>
            </View>

            {/* Right side: Chevron */}
            <View style={styles.chevronContainer}>
              <ChevronRight size={18} color={colors.textMuted} />
            </View>
          </View>
        </TouchableOpacity>
      </GestureDetector>
    </GestureHandlerRootView>
  );
}

const styles = StyleSheet.create({
  container: {
    marginHorizontal: 0,
    marginBottom: 12,
  },
  hiddenMeasureContainer: {
    position: "absolute",
    top: -1000, // Off-screen
    left: 0,
    flexDirection: "row", // Prevents text wrapping
    width: 9999, // Very large width to prevent any constraint
    opacity: 0,
  },
  hiddenMeasureText: {
    fontSize: 13,
    fontWeight: "500",
    flexShrink: 0, // Prevent text from shrinking
  },
  touchable: {
    width: "100%",
  },
  card: {
    flexDirection: "row",
    alignItems: "center",
    borderRadius: 12,
    borderWidth: 1,
    paddingHorizontal: 14,
    paddingVertical: 12,
  },
  iconContainer: {
    marginRight: 10,
  },
  contentContainer: {
    flex: 1,
    flexDirection: "row",
    alignItems: "center",
  },
  label: {
    fontSize: 13,
    fontWeight: "600",
    marginRight: 6,
  },
  titleWrapper: {
    flex: 1,
    height: 18, // Fixed height for single line
    overflow: "hidden",
  },
  marqueeContainer: {
    flex: 1,
    height: 18,
    overflow: "hidden",
  },
  marqueeContent: {
    position: "absolute",
    left: 0,
    top: 0,
    flexDirection: "row",
    height: 18,
  },
  title: {
    fontSize: 13,
    fontWeight: "500",
    lineHeight: 18,
  },
  chevronContainer: {
    marginLeft: 8,
  },
});

export default CookingTipsTicker;
