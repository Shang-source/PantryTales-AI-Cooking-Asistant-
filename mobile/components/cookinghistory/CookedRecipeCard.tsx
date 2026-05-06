import {
  View,
  Text,
  TouchableOpacity,
  ActivityIndicator,
  Image,
} from "react-native";
import Icon from "react-native-vector-icons/Feather";

import { Card } from "@/components/card";
import type { MyCookedRecipeCardDto } from "@/types/recipes";
import MaterialCommunityIcons from "@expo/vector-icons/build/MaterialCommunityIcons";
import { Trash2 } from "lucide-react-native";
import { useTheme } from "@/contexts/ThemeContext";

interface CookedRecipeCardProps {
  item: MyCookedRecipeCardDto;
  onPress: () => void;
  onDelete: () => void;
}

function formatDate(dateString: string) {
  const date = new Date(dateString);
  return date.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

function toTitleCase(str: string) {
  return str.replace(
    /\w\S*/g,
    (txt) => txt.charAt(0).toUpperCase() + txt.slice(1).toLowerCase()
  );
}

export function CookedRecipeCard({
  item,
  onPress,
  onDelete,
}: CookedRecipeCardProps) {
  const { colors } = useTheme();
  return (
    <TouchableOpacity onPress={onPress} activeOpacity={0.7}>
      <Card className="border-0">
        <View className="flex-row">
          {/* Recipe Image */}
          <View className="w-16 h-16 rounded-xl items-center justify-center mr-4" style={{ backgroundColor: colors.card }}>
            {item.coverImageUrl ? (
              <Image
                source={{ uri: item.coverImageUrl }}
                className="w-16 h-16 rounded-xl"
              />
            ) : (
              <MaterialCommunityIcons
                name="food"
                size={28}
                color={colors.textMuted}
              />
            )}
          </View>

          {/* Content */}
          <View className="flex-1 p-1 justify-between">
            <View className="flex-row items-center">
              <View className="flex-1 pr-2">
                <Text
                  className="text-base font-semibold"
                  style={{ color: colors.textPrimary }}
                  numberOfLines={1}
                  ellipsizeMode="tail"
                >
                  {toTitleCase(item.title)}
                </Text>
              </View>
              {/* Delete button */}
              <TouchableOpacity
                onPress={onDelete}
                className="pl-2 justify-center"
              >
                <Trash2 size={18} color={colors.error} />
              </TouchableOpacity>
            </View>

            <View className="flex-row items-center justify-between mt-2">
              {/* Cook count badge */}
              <View className="flex-row items-center px-2 py-1 rounded-full" style={{ backgroundColor: colors.success }}>
                <Icon name="check-circle" size={12} color={colors.bg} />
                <Text className="text-xs font-medium ml-1" style={{ color: colors.bg }}>
                  Cooked {item.cookCount}x
                </Text>
              </View>

              {/* Last cooked date */}
              <Text className="text-xs ml-2" style={{ color: colors.textSecondary }}>
                {formatDate(item.lastCookedAt)}
              </Text>
            </View>
          </View>
        </View>
      </Card>
    </TouchableOpacity>
  );
}
