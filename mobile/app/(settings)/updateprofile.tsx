import { useEffect, useMemo, useState, useCallback } from "react";
import {
  View,
  Text,
  TouchableOpacity,
  ScrollView,
  ActivityIndicator,
  Alert,
} from "react-native";
import { router, useFocusEffect, useLocalSearchParams } from "expo-router";
import * as ImagePicker from "expo-image-picker";
import { useQueryClient } from "@tanstack/react-query";
import { X, Camera } from "lucide-react-native";

import { Avatar, AvatarFallback, AvatarImage } from "@/components/avatar";
import { Input } from "@/components/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/select";
import { Skeleton } from "@/components/skeleton";
import { ApiResponse } from "@/types/api";
import { useAuthMutation, useAuthQuery, useImageUpload } from "@/hooks/useApi";
import { getDefaultAvatarUrl } from "@/utils/avatar";
import { useTheme } from "@/contexts/ThemeContext";
import type {
  UserGender,
  UserPreferenceRelation,
  UserProfileResponse,
} from "@/app/(tabs)/me";

interface TagChip {
  id: number;
  name: string;
  displayName: string;
  type: string;
}

interface TagGroup {
  type: string;
  typeDisplayName: string;
  tags: TagChip[];
}

type PreferenceMap = Record<number, UserPreferenceRelation>;

type UpdateProfilePayload = {
  nickname?: string;
  avatarUrl?: string | null;
  age?: number | null;
  gender?: UserGender;
  height?: number | null;
  weight?: number | null;
  preferences?: { tagId: number; relation: UserPreferenceRelation }[];
};

const genderOptions: { label: string; value: UserGender }[] = [
  { label: "Male", value: "Male" },
  { label: "Female", value: "Female" },
  { label: "Unknown", value: "Unknown" },
  { label: "N/A", value: "NotApplicable" },
];

const FormLabel = ({ children, textColor }: { children: React.ReactNode; textColor?: string }) => (
  <Text className="text-base font-semibold mb-2 mt-4" style={{ color: textColor }}>
    {children}
  </Text>
);

const Pill = ({
  label,
  active,
  onPress,
  accentColor,
  activeTextColor,
  cardColor,
  borderColor,
  textColor,
}: {
  label: string;
  active: boolean;
  onPress: () => void;
  accentColor: string;
  activeTextColor: string;
  cardColor?: string;
  borderColor?: string;
  textColor?: string;
}) => (
  <TouchableOpacity
    onPress={onPress}
    activeOpacity={0.85}
    className="px-4 py-2 rounded-full border mr-3 mb-3"
    style={{
      backgroundColor: active ? accentColor : (cardColor || "rgba(255,255,255,0.1)"),
      borderColor: active ? accentColor : (borderColor || "rgba(255,255,255,0.25)"),
    }}
  >
    <Text
      className="text-base font-semibold"
      style={{ color: active ? activeTextColor : textColor }}
    >
      {label}
    </Text>
  </TouchableOpacity>
);

const numberOrNull = (value: string) => {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
};

export default function PersonalProfileScreen() {
  const { source } = useLocalSearchParams<{ source?: string }>();
  const queryClient = useQueryClient();
  const { colors } = useTheme();

  const {
    data: profileData,
    isLoading: loadingProfile,
    refetch,
  } = useAuthQuery<ApiResponse<UserProfileResponse>>(
    ["user-profile"],
    "/api/users/me",
    { staleTime: 0, gcTime: 0, refetchOnMount: true },
  );

  // Public grouped tags for display (now includes id)
  const { data: tagGroupsData, isLoading: loadingTagGroups } = useAuthQuery<
    TagGroup[]
  >(["public-tag-groups"], "/api/tags", { staleTime: 5 * 60 * 1000 });

  const { mutateAsync: uploadImage, isPending: uploadingImage } =
    useImageUpload();

  const { mutateAsync: saveProfile, isPending: saving } = useAuthMutation<
    ApiResponse<UserProfileResponse>,
    UpdateProfilePayload
  >("/api/users/me", "PUT");

  const profile = profileData?.data;
  const tagGroups = useMemo(() => tagGroupsData ?? [], [tagGroupsData]);

  const [nickname, setNickname] = useState("");
  const [email, setEmail] = useState("");
  const [age, setAge] = useState("");
  const [gender, setGender] = useState<UserGender>("Unknown");
  const [height, setHeight] = useState("");
  const [weight, setWeight] = useState("");
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [preferences, setPreferences] = useState<PreferenceMap>({});

  const displayAvatarUrl = avatarUrl ?? getDefaultAvatarUrl(profile?.id);

  useFocusEffect(
    useCallback(() => {
      void refetch();
    }, [refetch]),
  );

  useEffect(() => {
    if (!profile) return;
    setNickname(profile.nickname ?? "");
    setEmail(profile.email ?? "");
    setAge(profile.age ? String(profile.age) : "");
    setGender(profile.gender ?? "Unknown");
    setHeight(profile.height ? String(profile.height) : "");
    setWeight(profile.weight ? String(profile.weight) : "");
    setAvatarUrl(profile.avatarUrl ?? null);
    const prefMap: PreferenceMap = {};
    (profile.preferences ?? []).forEach((p) => {
      prefMap[p.tagId] = p.relation;
    });
    setPreferences(prefMap);
  }, [profile]);

  const groupedChips = useMemo(() => {
    const find = (keys: string[]) =>
      tagGroups.find((g) => keys.includes(g.type.toLowerCase()))?.tags ?? [];
    return {
      goals: find(["goal", "goals"]),
      dietary: find(["dietary", "diet", "preference", "preferences"]),
      allergies: find(["allergen", "allergy", "allergens"]),
      restrictions: find(["restriction", "restrictions"]),
    };
  }, [tagGroups]);

  const togglePreference = (
    tagId: number,
    relation: UserPreferenceRelation,
  ) => {
    setPreferences((prev) => {
      const next = { ...prev };
      if (next[tagId] === relation) {
        delete next[tagId];
      } else {
        next[tagId] = relation;
      }
      return next;
    });
  };

  const handlePickAvatar = async () => {
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (permission.status !== "granted") {
      Alert.alert(
        "Permission needed",
        "Please allow photo library access to change avatar.",
      );
      return;
    }
    const result = await ImagePicker.launchImageLibraryAsync({
      allowsEditing: true,
      quality: 0.8,
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
    });
    if (result.canceled || !result.assets?.length) return;

    try {
      const uploaded = await uploadImage(result.assets[0].uri);
      setAvatarUrl(uploaded.url);
    } catch (error: any) {
      Alert.alert("Upload failed", error?.message ?? "Could not upload image.");
    }
  };

  const handleBack = useCallback(() => {
    // Navigate back based on where user came from
    if (source === "settings") {
      router.replace("/settings");
    } else {
      router.replace("/me");
    }
  }, [source]);

  const handleSave = async () => {
    try {
      const payload = {
        nickname: nickname.trim() || undefined,
        avatarUrl: avatarUrl ?? undefined,
        age: numberOrNull(age),
        gender,
        height: numberOrNull(height), // decimal (e.g., 170.5)
        weight: numberOrNull(weight), // decimal (e.g., 60.5)
        preferences: Object.entries(preferences).map(([tagId, relation]) => ({
          tagId: Number(tagId),
          relation,
        })),
      };
      const updated = await saveProfile(payload);
      if (updated?.data) {
        queryClient.setQueryData(["user-profile"], updated);
        Alert.alert("Saved", "Profile updated successfully.");
        handleBack();
      }
    } catch (error: any) {
      Alert.alert("Save failed", error?.message ?? "Could not update profile.");
    }
  };

  const isLoading = loadingProfile && !profile;

  return (
    <View className="flex-1" style={{ backgroundColor: colors.bg }}>
      <View className="flex-row items-center justify-between px-4 pt-14 pb-4 border-b" style={{ borderBottomColor: colors.border }}>
        <TouchableOpacity onPress={handleBack} className="p-2">
          <X size={24} color={colors.textPrimary} />
        </TouchableOpacity>
        <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
          Personal Profile
        </Text>
        <TouchableOpacity
          onPress={handleSave}
          disabled={saving || isLoading || uploadingImage}
          className="px-4 py-2 rounded-lg"
          style={{ backgroundColor: saving || uploadingImage ? `${colors.accent}80` : colors.accent }}
        >
          {saving || uploadingImage ? (
            <ActivityIndicator color={colors.bg} />
          ) : (
            <Text className="font-semibold" style={{ color: colors.bg }}>Save</Text>
          )}
        </TouchableOpacity>
      </View>

      <ScrollView className="flex-1" contentContainerClassName="pb-12 px-5">
        <View className="items-center mt-6 mb-6">
          {isLoading ? (
            <Skeleton className="w-28 h-28 rounded-full" style={{ backgroundColor: `${colors.textMuted}33` }} />
          ) : (
            <View className="relative">
              <Avatar className="w-28 h-28">
                {displayAvatarUrl ? (
                  <AvatarImage
                    source={{ uri: displayAvatarUrl }}
                    alt="Avatar"
                  />
                ) : null}
                <AvatarFallback className="items-center justify-center" style={{ backgroundColor: colors.accent }}>
                  <Text className="text-4xl font-bold" style={{ color: colors.bg }}>
                    {nickname?.[0]?.toUpperCase() || "U"}
                  </Text>
                </AvatarFallback>
              </Avatar>
              <TouchableOpacity
                onPress={handlePickAvatar}
                disabled={uploadingImage}
                className="absolute bottom-2 right-2 w-10 h-10 rounded-full items-center justify-center shadow-md"
                style={{ backgroundColor: colors.accent }}
              >
                {uploadingImage ? (
                  <ActivityIndicator color={colors.bg} />
                ) : (
                  <Camera size={18} color={colors.bg} />
                )}
              </TouchableOpacity>
            </View>
          )}
        </View>

        <FormLabel textColor={colors.textPrimary}>Username</FormLabel>
        <Input
          value={nickname}
          onChangeText={setNickname}
          placeholder="Your nickname"
          placeholderTextColor={colors.textMuted}
          className="rounded-xl"
          style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
        />

        <FormLabel textColor={colors.textPrimary}>Email</FormLabel>
        <Input
          value={email}
          editable={false}
          placeholder="email@example.com"
          placeholderTextColor={colors.textMuted}
          className="rounded-xl"
          style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textMuted }}
        />
        <Text className="text-sm mt-1" style={{ color: colors.textMuted }}>
          Email address cannot be changed
        </Text>

        <FormLabel textColor={colors.textPrimary}>Age</FormLabel>
        <Input
          value={age}
          onChangeText={(text) => {
            if (/^\d*$/.test(text)) {
              setAge(text);
            }
          }}
          keyboardType="number-pad"
          placeholder="18"
          placeholderTextColor={colors.textMuted}
          className="rounded-xl"
          style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
        />

        <FormLabel textColor={colors.textPrimary}>Gender</FormLabel>
        <Select
          value={gender}
          onValueChange={(val) => setGender(val as UserGender)}
        >
          <SelectTrigger className="rounded-xl h-12 px-4" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
            <SelectValue
              placeholder="Select gender"
              className="text-base"
              style={{ color: colors.textPrimary }}
            />
          </SelectTrigger>
          <SelectContent className="rounded-xl">
            {genderOptions.map((option) => (
              <SelectItem
                key={option.value}
                value={option.value}
                label={option.label}
              >
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <FormLabel textColor={colors.textPrimary}>Height (cm)</FormLabel>
        <Input
          value={height}
          onChangeText={setHeight}
          keyboardType="decimal-pad"
          placeholder="170"
          placeholderTextColor={colors.textMuted}
          className="rounded-xl"
          style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
        />

        <FormLabel textColor={colors.textPrimary}>Weight (kg)</FormLabel>
        <Input
          value={weight}
          onChangeText={setWeight}
          keyboardType="decimal-pad"
          placeholder="60"
          placeholderTextColor={colors.textMuted}
          className="rounded-xl"
          style={{ backgroundColor: colors.card, borderColor: colors.border, color: colors.textPrimary }}
        />

        {(
          [
            {
              title: "Goals",
              relation: "Goal" as UserPreferenceRelation,
              items: groupedChips.goals,
            },
            {
              title: "Dietary Preferences",
              relation: "Like" as UserPreferenceRelation,
              items: groupedChips.dietary,
            },
            {
              title: "Allergies",
              relation: "Allergy" as UserPreferenceRelation,
              items: groupedChips.allergies,
            },
            {
              title: "Restrictions",
              relation: "Restriction" as UserPreferenceRelation,
              items: groupedChips.restrictions,
            },
          ] satisfies {
            title: string;
            relation: UserPreferenceRelation;
            items: TagChip[];
          }[]
        ).map((section) => (
          <View key={section.title}>
            <FormLabel textColor={colors.textPrimary}>{section.title}</FormLabel>
            <View className="flex-row flex-wrap">
              {section.items.map((chip) => {
                const tagId = chip.id;
                const isActive = preferences[tagId] === section.relation;
                return (
                  <Pill
                    key={`${chip.type}-${chip.name}`}
                    label={chip.displayName || chip.name}
                    active={isActive}
                    onPress={() => {
                      togglePreference(tagId, section.relation);
                    }}
                    accentColor={colors.accent}
                    activeTextColor={colors.bg}
                    cardColor={colors.card}
                    borderColor={colors.border}
                    textColor={colors.textSecondary}
                  />
                );
              })}
              {section.items.length === 0 && !loadingTagGroups && (
                <Text style={{ color: colors.textMuted }}>
                  No {section.title.toLowerCase()} tags available.
                </Text>
              )}
            </View>
          </View>
        ))}
      </ScrollView>
    </View>
  );
}
