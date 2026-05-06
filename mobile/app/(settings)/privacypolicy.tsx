import { StatusBar, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { X } from "lucide-react-native";
import { router } from "expo-router";
import { useTheme } from "@/contexts/ThemeContext";

const policySections = [
  {
    title: "Data Collection",
    body:
      "PantryTales collects and stores information you provide, including your profile details, inventory data, recipes, and community posts. All data is stored locally on your device and is not transmitted to external servers unless you explicitly enable cloud sync features.",
  },
  {
    title: "Camera & Photo Access",
    body:
      "We request camera and photo library access to enable ingredient scanning and recipe detection features. Photos are processed locally on your device for AI recognition. We do not upload or store your photos on external servers.",
  },
  {
    title: "Personal Information",
    body:
      "Your personal information (name, email, dietary preferences, allergies) is used solely to personalize your experience and provide relevant recipe recommendations. This information is encrypted and stored securely on your device.",
  },
  {
    title: "Community Features",
    body:
      "When you share recipes or posts in the community section, this content becomes visible to other users. You can delete your posts at any time. Your profile information (username and avatar) will be visible to other users when you participate in community features.",
  },
  {
    title: "Data Security",
    body:
      "We implement industry-standard security measures to protect your data. However, no method of electronic storage is 100% secure. We recommend using strong passwords and enabling device security features.",
  },
  {
    title: "Third-Party Services",
    body:
      "PantryTales may use third-party AI services for ingredient recognition and recipe generation. These services process data temporarily and do not store your personal information.",
  },
];

const rights = [
  "Access, modify, or delete your personal data at any time through the app settings.",
  "Request a complete export of your data by contacting our support team.",
];

export default function PrivacyPolicyScreen() {
  const { colors } = useTheme();

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
        <Text className="text-xl font-semibold" style={{ color: colors.textPrimary }}>Privacy Policy</Text>
      </View>

      <ScrollView className="flex-1" contentContainerClassName="pb-16">
        <View className="px-6 pb-6 pt-4 items-center">
          <Text className="text-3xl font-semibold mt-2" style={{ color: colors.textPrimary }}>Privacy Policy</Text>
          <Text className="text-lg mt-1 text-center" style={{ color: colors.textSecondary }}>
            How PantryTales collects, uses, and protects your information.
          </Text>
        </View>

        <View className="px-5 pt-2">
          <View className="rounded-2xl border p-5 mb-4" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
            <Text className="text-lg font-semibold mb-1" style={{ color: colors.textPrimary }}>
              Your privacy, our promise
            </Text>
            <Text className="leading-6" style={{ color: colors.textSecondary }}>
              We designed PantryTales to keep your data on your device by default. This
              page explains what we collect, how we use it, and the choices you have.
            </Text>
          </View>

          {policySections.map((section) => (
            <View
              key={section.title}
              className="rounded-2xl border p-5 mb-4"
              style={{ backgroundColor: colors.card, borderColor: colors.border }}
            >
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                {section.title}
              </Text>
              <Text className="leading-6 mt-2" style={{ color: colors.textSecondary }}>{section.body}</Text>
            </View>
          ))}

          <View className="rounded-2xl border p-5" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
            <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>Your Rights</Text>
            <Text className="leading-6 mt-2" style={{ color: colors.textSecondary }}>
              You stay in control of your information:
            </Text>
            <View className="mt-3 gap-3">
              {rights.map((item) => (
                <View key={item} className="flex-row items-start">
                  <View className="w-2 h-2 rounded-full mt-2 mr-3" style={{ backgroundColor: colors.accent }} />
                  <Text className="leading-6 flex-1" style={{ color: colors.textSecondary }}>{item}</Text>
                </View>
              ))}
            </View>
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
