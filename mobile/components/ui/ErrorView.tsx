import { View, Text, TouchableOpacity } from "react-native";
import { useTheme } from "@/contexts/ThemeContext";

type ErrorViewProps = {
  errorPage: string;
  refetch: () => void;
};

export default function ErrorView({
  errorPage,
  refetch,
}: ErrorViewProps) {
  const { colors } = useTheme();
  return (
    <View className="flex-1 items-center justify-center px-4">
      <Text className="mb-4 text-center" style={{ color: colors.textSecondary }}>
        Failed to load {errorPage}
      </Text>

      <TouchableOpacity
        onPress={refetch}
        className="rounded-lg px-6 py-2"
        style={{ backgroundColor: colors.accent }}
        activeOpacity={0.85}
      >
        <Text className="font-medium" style={{ color: colors.bg }}>Retry</Text>
      </TouchableOpacity>
    </View>
  );
}
