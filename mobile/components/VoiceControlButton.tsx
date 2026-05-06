import { useEffect } from "react";
import { TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import Animated, {
  useSharedValue,
  useAnimatedStyle,
  withRepeat,
  withTiming,
  cancelAnimation,
  Easing,
} from "react-native-reanimated";
import { useTheme } from "@/contexts/ThemeContext";

interface VoiceControlButtonProps {
  isEnabled: boolean;
  isListening: boolean;
  hasError?: boolean;
  onToggle: () => void;
}

export function VoiceControlButton({
  isEnabled,
  isListening,
  hasError = false,
  onToggle,
}: VoiceControlButtonProps) {
  const { colors } = useTheme();
  const pulseScale = useSharedValue(1);
  const pulseOpacity = useSharedValue(0.6);

  useEffect(() => {
    if (isListening && isEnabled) {
      // Start pulsing animation
      pulseScale.value = withRepeat(
        withTiming(1.4, { duration: 1000, easing: Easing.inOut(Easing.ease) }),
        -1,
        true,
      );
      pulseOpacity.value = withRepeat(
        withTiming(0, { duration: 1000, easing: Easing.inOut(Easing.ease) }),
        -1,
        true,
      );
    } else {
      // Stop animation and reset
      cancelAnimation(pulseScale);
      cancelAnimation(pulseOpacity);
      pulseScale.value = withTiming(1, { duration: 200 });
      pulseOpacity.value = withTiming(0.6, { duration: 200 });
    }

    return () => {
      cancelAnimation(pulseScale);
      cancelAnimation(pulseOpacity);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isListening, isEnabled]);

  const pulseStyle = useAnimatedStyle(() => ({
    transform: [{ scale: pulseScale.value }],
    opacity: pulseOpacity.value,
  }));

  // Determine icon color based on state
  const getIconColor = () => {
    if (hasError) return "#ef4444"; // Red for error
    if (isEnabled) return colors.accent; // Accent color when enabled
    return colors.textMuted; // Muted text color when disabled
  };

  // Determine background color based on state
  const getBackgroundColor = () => {
    if (hasError) return "rgba(239, 68, 68, 0.15)"; // Red tint for error
    if (isEnabled) return colors.card; // Card background when enabled
    return colors.card; // Card background when disabled
  };

  return (
    <TouchableOpacity
      onPress={onToggle}
      activeOpacity={0.7}
      className="relative"
    >
      {/* Pulse ring - only visible when listening */}
      {isEnabled && isListening && (
        <Animated.View
          style={[
            {
              position: "absolute",
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              borderRadius: 20,
              backgroundColor: colors.accent,
            },
            pulseStyle,
          ]}
        />
      )}

      {/* Button */}
      <View
        className="h-10 w-10 items-center justify-center rounded-full"
        style={{ backgroundColor: getBackgroundColor() }}
      >
        <Ionicons
          name={isEnabled ? "mic" : "mic-off"}
          size={20}
          color={getIconColor()}
        />
      </View>
    </TouchableOpacity>
  );
}
