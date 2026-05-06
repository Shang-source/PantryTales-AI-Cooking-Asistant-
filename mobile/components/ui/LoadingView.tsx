import { View, ActivityIndicator } from "react-native";
import { useTheme } from "@/contexts/ThemeContext";

export default function LoadingView() {
  const { colors } = useTheme();
  return (
    <View className="flex-1 items-center justify-center py-20 mt-20">
      <ActivityIndicator size="large" color={colors.accent} />
    </View>
  );
}
