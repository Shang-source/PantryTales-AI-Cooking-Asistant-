import { useState, useEffect, useCallback } from "react";
import {
  View,
  Text,
  TouchableOpacity,
  ScrollView,
  StatusBar,
  Platform,
  Alert,
} from "react-native";
import { router, useFocusEffect } from "expo-router";
import { useAuth } from "@clerk/clerk-expo";
import { useQueryClient } from "@tanstack/react-query";
import * as SecureStore from "expo-secure-store";
import AsyncStorage from "@react-native-async-storage/async-storage";
import {
  X,
  User,
  Users,
  LogOut,
  ChevronRight,
  Info,
  Shield,
  Trash2,
  Palette,
} from "lucide-react-native";
import { Switch } from "../../components/switch";
import { Avatar, AvatarFallback, AvatarImage } from "../../components/avatar";
import { Skeleton } from "../../components/skeleton";
import { useAuthQuery } from "@/hooks/useApi";
import { ApiResponse } from "@/types/api";
import type { UserProfileResponse } from "../(tabs)/me";
import { PROFILE_CACHE_KEY } from "@/constants/constants";
import { getDefaultAvatarUrl } from "@/utils/avatar";
import { toast } from "@/components/sonner";
import { ThemePicker } from "@/components/ThemePicker";
import { useTheme } from "@/contexts/ThemeContext";

interface MenuItemProps {
  icon: React.ElementType;
  title: string;
  subtitle?: string;
  value?: string;
  onPress?: () => void;
  isLast?: boolean;
  isSwitch?: boolean;
  switchValue?: boolean;
  onSwitchChange?: (val: boolean) => void;
  highlight?: boolean;
}

const MenuItem = ({
  icon: Icon,
  title,
  subtitle,
  value,
  onPress,
  isSwitch,
  switchValue,
  onSwitchChange,
  highlight,
  themeColors,
}: MenuItemProps & { themeColors?: { bg: string; accent: string; card: string; border: string; textPrimary: string; textSecondary: string; textMuted: string } }) => {
  const handlePress = () => {
    if (isSwitch && onSwitchChange) {
      onSwitchChange(!switchValue);
    } else if (onPress) {
      onPress();
    }
  };

  return (
    <TouchableOpacity
      onPress={handlePress}
      activeOpacity={isSwitch ? 1 : 0.7}
      className="flex-row items-center justify-between px-5 py-4 rounded-2xl border"
      style={{ backgroundColor: themeColors?.card, borderColor: themeColors?.border }}
    >
      <View className="flex-row items-center flex-1">
        <View
          className="w-10 h-10 rounded-xl items-center justify-center mr-4"
          style={{ backgroundColor: themeColors?.card }}
        >
          <Icon size={20} color={themeColors?.textPrimary} />
        </View>

        <View className="flex-1">
          <Text className="text-lg font-semibold" style={{ color: themeColors?.textPrimary }}>{title}</Text>
          {subtitle && (
            <Text className="text-sm mt-0.5" style={{ color: themeColors?.textSecondary }}>{subtitle}</Text>
          )}
        </View>
      </View>

      {isSwitch ? (
        <Switch checked={switchValue} onCheckedChange={onSwitchChange} />
      ) : (
        <View className="flex-row items-center">
          {value && (
            <Text className="mr-2 text-base font-medium" style={{ color: themeColors?.textSecondary }}>
              {value}
            </Text>
          )}
          <ChevronRight size={18} color={themeColors?.textMuted} />
        </View>
      )}
    </TouchableOpacity>
  );
};

export default function SettingsScreen() {
  const { signOut } = useAuth();
  const queryClient = useQueryClient();
  const [isClearing, setIsClearing] = useState(false);
  const { colors } = useTheme();

  const [userProfile, setUserProfile] = useState<UserProfileResponse | null>(
    null,
  );

  const { data, refetch, isFetching, isLoading } = useAuthQuery<
    ApiResponse<UserProfileResponse>
  >(["user-profile"], "/api/users/me", {
    staleTime: 0,
    gcTime: 0,
    refetchOnMount: true,
  });

  useFocusEffect(
    useCallback(() => {
      void refetch();
    }, [refetch]),
  );

  useEffect(() => {
    if (data?.data) {
      setUserProfile(data.data);
    } else {
      setUserProfile(null);
    }
  }, [data]);

  const isProfileLoading = (isLoading || isFetching) && !userProfile;

  const handleClearCache = useCallback(() => {
    Alert.alert(
      "Clear Cache",
      "This will clear all cached data including smart recipes. You may need to reload some screens.",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Clear",
          style: "destructive",
          onPress: async () => {
            setIsClearing(true);
            try {
              // Clear React Query cache (smart recipes, inventory, etc.)
              queryClient.clear();

              // Clear SecureStore cached data
              if (Platform.OS !== "web") {
                try {
                  await SecureStore.deleteItemAsync(PROFILE_CACHE_KEY);
                } catch {}
              }

              // Clear AsyncStorage cached data (smart recipes prompt state, etc.)
              try {
                await AsyncStorage.removeItem("@smart_recipes_prompt_dismissed");
              } catch {}

              toast.success("Cache cleared successfully");
            } catch (error) {
              toast.error("Failed to clear cache");
            } finally {
              setIsClearing(false);
            }
          },
        },
      ],
    );
  }, [queryClient]);

  const avatarUrl =
    userProfile?.avatarUrl || getDefaultAvatarUrl(userProfile?.id) || undefined;

  return (
    <View className="flex-1" style={{ backgroundColor: colors.bg }}>
      <StatusBar barStyle="light-content" />

      <ScrollView className="flex-1" contentContainerClassName="pb-16">
        <View className="pt-16 pb-7 px-6 relative">
          <TouchableOpacity
            onPress={() => router.push("/(tabs)/me")}
            className="absolute top-16 right-6 w-9 h-9 items-center justify-center rounded-full"
          >
            <X size={22} color={colors.accent} />
          </TouchableOpacity>

          {isProfileLoading ? (
            <View className="flex-row items-center">
              <View className="h-16 w-16 rounded-full border-2 overflow-hidden p-0.5" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
                <Skeleton className="h-full w-full rounded-full" />
              </View>

              <View className="ml-4 flex-1 gap-2">
                <Skeleton className="h-6 w-32 rounded" />
                <Skeleton className="h-4 w-48 rounded" />
              </View>
            </View>
          ) : (
            <View className="flex-row items-center">
              <View className="h-16 w-16 rounded-full border-2 items-center justify-center overflow-hidden p-0.5" style={{ borderColor: colors.border, backgroundColor: colors.card }}>
                <Avatar className="h-full w-full">
                  {avatarUrl ? (
                    <AvatarImage
                      source={{
                        uri: avatarUrl,
                      }}
                      alt={userProfile?.nickname || "User"}
                    />
                  ) : null}
                  <AvatarFallback className="items-center justify-center h-full w-full" style={{ backgroundColor: colors.accent }}>
                    <Text className="text-xl font-bold" style={{ color: colors.bg }}>
                      {userProfile?.nickname?.[0]?.toUpperCase() || "U"}
                    </Text>
                  </AvatarFallback>
                </Avatar>
              </View>

              <View className="ml-4 flex-1 pr-8">
                <Text
                  className="text-xl font-bold"
                  style={{ color: colors.textPrimary }}
                  numberOfLines={1}
                >
                  {userProfile?.nickname || "Pantry User"}
                </Text>
                <Text
                  className="text-base mt-0.5"
                  style={{ color: colors.textSecondary }}
                  numberOfLines={1}
                >
                  {userProfile?.email || "email@example.com"}
                </Text>
              </View>
            </View>
          )}
        </View>

        <View className="px-5 mt-6">
          <Text className="text-sm font-semibold mb-3" style={{ color: colors.textSecondary }}>
            Appearance
          </Text>
          <View className="border rounded-2xl px-4 py-3" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
            <View className="flex-row items-center mb-2">
              <View className="w-10 h-10 rounded-xl items-center justify-center mr-4" style={{ backgroundColor: colors.card }}>
                <Palette size={20} color={colors.textPrimary} />
              </View>
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                Color Theme
              </Text>
            </View>
            <ThemePicker />
          </View>
        </View>

        <View className="px-5 mt-6">
          <Text className="text-sm font-semibold mb-3" style={{ color: colors.textSecondary }}>
            Account Settings
          </Text>
          <View className="gap-3">
            <MenuItem
              icon={User}
              title="Personal Profile"
              onPress={() => router.push({ pathname: "/updateprofile", params: { source: "settings" } })}
              themeColors={colors}
            />
            <MenuItem
              icon={Users}
              title="Family Management"
              subtitle="Manage shared inventory"
              onPress={() => router.push("/familymember")}
              themeColors={colors}
            />
          </View>
        </View>

        <View className="px-5 mt-10">
          <Text className="text-sm font-semibold mb-3" style={{ color: colors.textSecondary }}>
            Storage & Cache
          </Text>
          <View className="gap-3">
            <MenuItem
              icon={Trash2}
              title="Clear Cache"
              subtitle={isClearing ? "Clearing..." : "Free up space and reset data"}
              onPress={handleClearCache}
              themeColors={colors}
            />
          </View>
        </View>

        <View className="px-5 mt-10">
          <Text className="text-sm font-semibold mb-3" style={{ color: colors.textSecondary }}>
            Privacy & About
          </Text>
          <View className="gap-3">
            <MenuItem
              icon={Shield}
              title="Privacy Policy"
              onPress={() => router.push("/privacypolicy")}
              themeColors={colors}
            />
            <MenuItem
              icon={Info}
              title="About Us"
              onPress={() => router.push("/aboutus")}
              themeColors={colors}
            />
          </View>
        </View>

        <View className="mt-12 items-center">
          <TouchableOpacity
            className="flex-row items-center px-6 py-3 rounded-full border"
            style={{ backgroundColor: "rgba(239, 68, 68, 0.15)", borderColor: colors.border }}
            onPress={async () => {
              if (Platform.OS !== "web") {
                try {
                  await SecureStore.deleteItemAsync(PROFILE_CACHE_KEY);
                } catch {}
              }
              await signOut();
              router.replace("/(auth)/sign-in");
            }}
          >
            <LogOut size={18} color={colors.error} />
            <Text className="text-lg ml-2 font-semibold" style={{ color: colors.error }}>
              Log Out
            </Text>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </View>
  );
}
