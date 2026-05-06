import {
  Image,
  ScrollView,
  StatusBar,
  Text,
  TouchableOpacity,
  View,
  Linking,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { router } from "expo-router";
import { Info, Mail, Shield, X } from "lucide-react-native";
import { useTheme } from "@/contexts/ThemeContext";

const logoImage = require("../../assets/images/logo.jpg");

const versionDetails = [
  { label: "Version", value: "1.0.0" },
  { label: "Build", value: "2025.12.17" },
  { label: "Platform", value: "Mobile" },
];

const features = [
  "AI-powered ingredient and recipe recognition",
  "Smart inventory management with expiry tracking",
  "Personalized recipe recommendations",
  "Interactive cooking assistant",
  "Recipe sharing community",
  "Family inventory sharing",
];

const contactOptions = [
  {
    icon: Mail,
    title: "Email Support",
    email: "fmen997@aucklanduni.ac.nz",
  },
  {
    icon: Info,
    title: "Feedback & Suggestions",
    email: "feim0570@gmail.com",
  },
  {
    icon: Shield,
    title: "Report Security Issues",
    email: "2845497436@qq.com",
  },
];

export default function AboutUsScreen() {
  const { colors } = useTheme();

  const handleEmailPress = async (email: string) => {
    const mailtoUrl = `mailto:${email}`;
    try {
      await Linking.openURL(mailtoUrl);
    } catch {
      // no-op; UI click should fail silently if mail app is unavailable
    }
  };

  return (
    <SafeAreaView className="flex-1" style={{ backgroundColor: colors.bg }}>
      <StatusBar barStyle="light-content" />

      <View className="flex-row items-center px-5 pb-4 pt-2">
        <TouchableOpacity
          onPress={() => router.back()}
          className="-ml-2 mr-3 w-10 h-10 items-center justify-center"
          accessibilityLabel="Go back"
          accessibilityRole="button"
        >
          <X size={22} color={colors.textPrimary} />
        </TouchableOpacity>
        <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>About Us</Text>
      </View>

      <ScrollView className="flex-1" contentContainerClassName="pb-14">
        <View className="px-6 pb-6 pt-4 items-center">
          <View className="w-36 h-36 rounded-2xl shadow-md items-center justify-center" style={{ backgroundColor: colors.card }}>
            <Image
              source={logoImage}
              className="w-28 h-28 rounded-xl"
              resizeMode="contain"
            />
          </View>
          <Text className="text-3xl font-semibold mt-6" style={{ color: colors.textPrimary }}>
            PantryTales
          </Text>
          <Text className="text-lg mt-1" style={{ color: colors.textSecondary }}>
            Smart Food Management
          </Text>
        </View>

        <View className="px-6 pt-2">
          <Text className="text-xl font-semibold mb-3" style={{ color: colors.textPrimary }}>
            Version Information
          </Text>
          <View className="rounded-2xl border px-5 py-4" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
            {versionDetails.map((item, index) => {
              const isLast = index === versionDetails.length - 1;
              return (
                <View
                  key={item.label}
                  className={`flex-row items-center justify-between py-3 ${
                    !isLast ? "border-b" : ""
                  }`}
                  style={!isLast ? { borderBottomColor: colors.border } : undefined}
                >
                  <Text className="text-base font-medium" style={{ color: colors.textSecondary }}>
                    {item.label}
                  </Text>
                  <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                    {item.value}
                  </Text>
                </View>
              );
            })}
          </View>

          <Text className="text-xl font-semibold mt-8" style={{ color: colors.textPrimary }}>
            About PantryTales
          </Text>
          <Text className="leading-6 mt-3" style={{ color: colors.textSecondary }}>
            PantryTales is an intelligent food management application that helps
            you reduce food waste, discover new recipes, and manage your kitchen
            inventory efficiently. Using AI technology, we make cooking easier
            and more enjoyable for everyone.
          </Text>

          <Text className="text-xl font-semibold mt-8" style={{ color: colors.textPrimary }}>
            Features
          </Text>
          <View className="mt-3">
            {features.map((feature) => (
              <View key={feature} className="flex-row items-start mb-3">
                <View className="w-2 h-2 rounded-full mt-2 mr-3" style={{ backgroundColor: colors.accent }} />
                <Text className="flex-1 text-base leading-6" style={{ color: colors.textSecondary }}>
                  {feature}
                </Text>
              </View>
            ))}
          </View>

          <Text className="text-xl font-semibold mt-8" style={{ color: colors.textPrimary }}>Contact Us</Text>
          <View className="mt-3">
            {contactOptions.map((item) => {
              const Icon = item.icon;
              return (
                <TouchableOpacity
                  key={item.email}
                  onPress={() => handleEmailPress(item.email)}
                  className="flex-row items-center rounded-2xl px-5 py-4 mb-3 border"
                  style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  activeOpacity={0.85}
                >
                  <View className="w-10 h-10 rounded-full items-center justify-center mr-3" style={{ backgroundColor: `${colors.accent}30` }}>
                    <Icon size={20} color={colors.accent} />
                  </View>
                  <View className="flex-1">
                    <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                      {item.title}
                    </Text>
                    <Text className="text-sm mt-0.5" style={{ color: colors.accent }}>
                      {item.email}
                    </Text>
                  </View>
                </TouchableOpacity>
              );
            })}
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
