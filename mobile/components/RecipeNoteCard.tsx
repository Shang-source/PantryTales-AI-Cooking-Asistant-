import { Image, Text, TouchableOpacity, View } from "react-native";
import Icon from "react-native-vector-icons/Feather";
import type { RecipeCardDto } from "@/types/recipes";
import { useTheme } from "@/contexts/ThemeContext";

interface RecipeNoteCardProps {
  recipe: RecipeCardDto;
  onView?: (recipeId: string) => void;
  onEdit?: (recipeId: string) => void;
}

export function RecipeNoteCard({ recipe, onView, onEdit }: RecipeNoteCardProps) {
  const { colors } = useTheme();
  const badgeIcon = recipe.visibility === "Public" ? "globe" : "lock";
  const likes = recipe.likesCount ?? 0;
  const saves = recipe.savedCount ?? 0;

  // Check for valid cover image - must be a valid http(s) URL
  const rawCover = recipe.coverImageUrl?.trim();
  const cover = rawCover && (rawCover.startsWith('http://') || rawCover.startsWith('https://')) ? rawCover : null;

  // Layout with image
  if (cover) {
    return (
      <View className="mb-3 flex-row gap-3 rounded-2xl p-3" style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}>
        <Image
          source={{ uri: cover }}
          className="h-20 w-20 rounded-xl"
          style={{ backgroundColor: colors.card }}
          resizeMode="cover"
        />
        <View className="flex-1">
          <View className="flex-row items-center justify-between">
            <Text
              className="flex-1 text-base font-semibold"
              style={{ color: colors.textPrimary }}
              numberOfLines={2}
            >
              {recipe.title}
            </Text>
            <View
              className="ml-2 flex-row items-center gap-1 rounded-full px-2.5 py-1 border bg-transparent"
              style={{ borderColor: colors.border }}
            >
              <Icon name={badgeIcon} size={12} color={colors.textPrimary} />
              <Text className="text-xs font-semibold" style={{ color: colors.textPrimary }}>
                {recipe.visibility}
              </Text>
            </View>
          </View>

          <View className="mt-1 flex-row items-center gap-3">
            <View className="flex-row items-center gap-1">
              <Icon name="heart" size={14} color={colors.accent} />
              <Text className="text-xs font-medium" style={{ color: colors.textSecondary }}>{likes}</Text>
            </View>
            <View className="flex-row items-center gap-1">
              <Icon name="bookmark" size={14} color={colors.accent} />
              <Text className="text-xs font-medium" style={{ color: colors.textSecondary }}>{saves} saves</Text>
            </View>
          </View>

          <View className="mt-2 flex-row gap-2">
            <TouchableOpacity
              onPress={() => onView?.(recipe.id)}
              className="flex-1 flex-row items-center justify-center gap-1.5 rounded-full border px-3 py-1.5"
              style={{ borderColor: colors.border }}
              activeOpacity={0.85}
            >
              <Icon name="eye" size={14} color={colors.textPrimary} />
              <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>View</Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={() => onEdit?.(recipe.id)}
              className="flex-1 flex-row items-center justify-center gap-1.5 rounded-full border px-3 py-1.5"
              style={{ borderColor: colors.border }}
              activeOpacity={0.85}
            >
              <Icon name="edit-2" size={14} color={colors.textPrimary} />
              <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Edit</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    );
  }

  // Layout without image - full width text
  return (
    <View className="mb-3 rounded-2xl p-4" style={{ borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card }}>
      <View className="flex-row items-start justify-between">
        <Text
          className="flex-1 text-base font-semibold mr-2"
          style={{ color: colors.textPrimary }}
          numberOfLines={2}
        >
          {recipe.title}
        </Text>
        <View
          className="flex-row items-center gap-1 rounded-full px-2.5 py-1 border bg-transparent"
          style={{ borderColor: colors.border }}
        >
          <Icon name={badgeIcon} size={12} color={colors.textPrimary} />
          <Text className="text-xs font-semibold" style={{ color: colors.textPrimary }}>
            {recipe.visibility}
          </Text>
        </View>
      </View>

      <View className="mt-2 flex-row items-center gap-3">
        <View className="flex-row items-center gap-1">
          <Icon name="heart" size={14} color={colors.accent} />
          <Text className="text-xs font-medium" style={{ color: colors.textSecondary }}>{likes}</Text>
        </View>
        <View className="flex-row items-center gap-1">
          <Icon name="bookmark" size={14} color={colors.accent} />
          <Text className="text-xs font-medium" style={{ color: colors.textSecondary }}>{saves} saves</Text>
        </View>
      </View>

      {recipe.tags?.length ? (
        <View className="mt-2 flex-row flex-wrap gap-2">
          {recipe.tags.slice(0, 4).map((tag) => (
            <View
              key={`${recipe.id}-${tag}`}
              className="rounded-full px-2.5 py-1"
              style={{ backgroundColor: `${colors.accent}15` }}
            >
              <Text className="text-[11px] font-medium" style={{ color: colors.textSecondary }}>
                {tag}
              </Text>
            </View>
          ))}
        </View>
      ) : null}

      <View className="mt-3 flex-row gap-2">
        <TouchableOpacity
          onPress={() => onView?.(recipe.id)}
          className="flex-1 flex-row items-center justify-center gap-1.5 rounded-full border px-3 py-2"
          style={{ borderColor: colors.border }}
          activeOpacity={0.85}
        >
          <Icon name="eye" size={14} color={colors.textPrimary} />
          <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>View</Text>
        </TouchableOpacity>
        <TouchableOpacity
          onPress={() => onEdit?.(recipe.id)}
          className="flex-1 flex-row items-center justify-center gap-1.5 rounded-full border px-3 py-2"
          style={{ borderColor: colors.border }}
          activeOpacity={0.85}
        >
          <Icon name="edit-2" size={14} color={colors.textPrimary} />
          <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>Edit</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}
