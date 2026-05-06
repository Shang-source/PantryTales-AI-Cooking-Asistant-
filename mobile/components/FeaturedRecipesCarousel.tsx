import { useCallback, useRef, useState, useEffect } from "react";
import {
  View,
  Text,
  Image,
  TouchableOpacity,
  FlatList,
  NativeSyntheticEvent,
  NativeScrollEvent,
  StyleSheet,
} from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { useRouter } from "expo-router";
import Icon from "react-native-vector-icons/Feather";
import type { FeaturedRecipe } from "@/hooks/useFeaturedRecipes";
import { Skeleton } from "./skeleton";
import { useTheme } from "@/contexts/ThemeContext";

const AUTO_SCROLL_INTERVAL = 5000; // 5 seconds

interface FeaturedRecipesCarouselProps {
  recipes: FeaturedRecipe[];
  isLoading?: boolean;
  onRecipePress?: (recipe: FeaturedRecipe) => void;
}

/**
 * Carousel component for displaying featured community recipes.
 * Features auto-scroll, dot indicators, and smooth animations.
 */
export function FeaturedRecipesCarousel({
  recipes,
  isLoading = false,
  onRecipePress,
}: FeaturedRecipesCarouselProps) {
  const { colors } = useTheme();
  const router = useRouter();
  const flatListRef = useRef<FlatList>(null);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [containerWidth, setContainerWidth] = useState(0);
  const autoScrollTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Auto-scroll effect
  useEffect(() => {
    if (recipes.length <= 1) return;

    const startAutoScroll = () => {
      autoScrollTimer.current = setTimeout(() => {
        const nextIndex = (currentIndex + 1) % recipes.length;
        flatListRef.current?.scrollToIndex({ index: nextIndex, animated: true });
      }, AUTO_SCROLL_INTERVAL);
    };

    startAutoScroll();

    return () => {
      if (autoScrollTimer.current) {
        clearTimeout(autoScrollTimer.current);
      }
    };
  }, [currentIndex, recipes.length]);

  const handleScroll = useCallback(
    (event: NativeSyntheticEvent<NativeScrollEvent>) => {
      if (!containerWidth) return;
      const offsetX = event.nativeEvent.contentOffset.x;
      const index = Math.round(offsetX / containerWidth);
      if (index >= 0 && index < recipes.length && index !== currentIndex) {
        setCurrentIndex(index);
      }
    },
    [containerWidth, recipes.length, currentIndex]
  );

  const handleRecipePress = useCallback(
    (recipe: FeaturedRecipe) => {
      if (onRecipePress) {
        onRecipePress(recipe);
      } else {
        router.push({
          pathname: "/recipe/[recipeId]",
          params: { recipeId: recipe.id, source: "featured" },
        });
      }
    },
    [onRecipePress, router]
  );

  const handleLayout = useCallback((event: any) => {
    setContainerWidth(event.nativeEvent.layout.width);
  }, []);

  // Show skeleton while loading
  if (isLoading) {
    return (
      <View style={styles.container}>
        <Skeleton className="h-full w-full rounded-xl" style={{ backgroundColor: colors.card }} />
      </View>
    );
  }

  // Empty state
  if (recipes.length === 0) {
    return (
      <View style={[styles.container, styles.emptyContainer, { backgroundColor: colors.card, borderColor: colors.border }]}>
        <Icon name="star" size={24} color={colors.textMuted} />
        <Text style={[styles.emptyText, { color: colors.textMuted }]}>No featured recipes</Text>
      </View>
    );
  }

  return (
    <View style={styles.container} onLayout={handleLayout}>
      {containerWidth > 0 && (
        <>
          <FlatList
            ref={flatListRef}
            data={recipes}
            horizontal
            pagingEnabled
            showsHorizontalScrollIndicator={false}
            snapToAlignment="center"
            decelerationRate="fast"
            keyExtractor={(item) => item.id}
            onMomentumScrollEnd={handleScroll}
            getItemLayout={(_, index) => ({
              length: containerWidth,
              offset: containerWidth * index,
              index,
            })}
            renderItem={({ item }) => (
              <RecipeCarouselItem
                recipe={item}
                width={containerWidth}
                onPress={() => handleRecipePress(item)}
                accentColor={colors.accent}
              />
            )}
          />
          {/* Dot Indicators */}
          {recipes.length > 1 && (
            <View style={styles.dotsContainer}>
              {recipes.map((_, index) => (
                <View
                  key={index}
                  style={[
                    styles.dot,
                    index === currentIndex ? styles.dotActive : styles.dotInactive,
                  ]}
                />
              ))}
            </View>
          )}
        </>
      )}
    </View>
  );
}

interface RecipeCarouselItemProps {
  recipe: FeaturedRecipe;
  width: number;
  onPress: () => void;
  accentColor: string;
}

function RecipeCarouselItem({ recipe, width, onPress, accentColor }: RecipeCarouselItemProps) {
  const hasImage = Boolean(recipe.coverImageUrl);

  return (
    <TouchableOpacity
      activeOpacity={0.9}
      onPress={onPress}
      style={[styles.itemContainer, { width }]}
    >
      <View style={styles.card}>
        {/* Full-bleed Image */}
        {hasImage ? (
          <Image
            source={{ uri: recipe.coverImageUrl! }}
            style={styles.fullImage}
            resizeMode="cover"
          />
        ) : (
          <View style={styles.imagePlaceholder}>
            <Icon name="image" size={32} color="rgba(255, 255, 255, 0.3)" />
          </View>
        )}

        {/* Community Badge - Top Left */}
        <View style={styles.badge}>
          {recipe.authorAvatarUrl ? (
            <Image
              source={{ uri: recipe.authorAvatarUrl }}
              style={styles.badgeAvatar}
            />
          ) : (
            <Icon name="users" size={10} color="white" />
          )}
          <Text style={styles.badgeText}>Community</Text>
        </View>

        {/* Gradient Overlay with Content - Bottom */}
        <LinearGradient
          colors={["transparent", "rgba(0,0,0,0.8)"]}
          style={styles.gradientOverlay}
        >
          <View style={styles.overlayContent}>
            <Text style={styles.title} numberOfLines={1}>
              {recipe.title}
            </Text>
            <View style={styles.metaRow}>
              {recipe.authorNickname && (
                <Text style={styles.author} numberOfLines={1}>
                  by {recipe.authorNickname}
                </Text>
              )}
              <View style={styles.statsRow}>
                <View style={styles.stat}>
                  <Icon name="heart" size={11} color={accentColor} />
                  <Text style={styles.statText}>{recipe.likesCount}</Text>
                </View>
                <View style={styles.stat}>
                  <Icon name="bookmark" size={11} color="white" />
                  <Text style={styles.statText}>{recipe.savedCount}</Text>
                </View>
              </View>
            </View>
          </View>
        </LinearGradient>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  container: {
    height: 130,
  },
  emptyContainer: {
    borderRadius: 12,
    borderWidth: 1,
    alignItems: "center",
    justifyContent: "center",
  },
  emptyText: {
    fontSize: 12,
    marginTop: 8,
  },
  dotsContainer: {
    position: "absolute",
    bottom: 6,
    left: 0,
    right: 0,
    flexDirection: "row",
    justifyContent: "center",
    alignItems: "center",
    gap: 5,
  },
  dot: {
    width: 5,
    height: 5,
    borderRadius: 3,
  },
  dotActive: {
    backgroundColor: "white",
  },
  dotInactive: {
    backgroundColor: "rgba(255, 255, 255, 0.3)",
  },
  itemContainer: {
    paddingHorizontal: 1,
  },
  card: {
    flex: 1,
    borderRadius: 12,
    overflow: "hidden",
    backgroundColor: "rgba(255, 255, 255, 0.1)",
  },
  fullImage: {
    ...StyleSheet.absoluteFillObject,
  },
  imagePlaceholder: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: "rgba(255, 255, 255, 0.05)",
    alignItems: "center",
    justifyContent: "center",
  },
  badge: {
    position: "absolute",
    top: 8,
    left: 8,
    backgroundColor: "rgba(0, 0, 0, 0.6)",
    borderRadius: 10,
    paddingHorizontal: 6,
    paddingVertical: 3,
    flexDirection: "row",
    alignItems: "center",
  },
  badgeAvatar: {
    width: 14,
    height: 14,
    borderRadius: 7,
  },
  badgeText: {
    color: "white",
    fontSize: 9,
    marginLeft: 4,
    fontWeight: "500",
  },
  gradientOverlay: {
    position: "absolute",
    bottom: 0,
    left: 0,
    right: 0,
    height: 60,
    justifyContent: "flex-end",
    paddingHorizontal: 10,
    paddingBottom: 8,
  },
  overlayContent: {
    gap: 2,
  },
  title: {
    color: "white",
    fontWeight: "600",
    fontSize: 14,
    textShadowColor: "rgba(0, 0, 0, 0.5)",
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 2,
  },
  metaRow: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
  },
  author: {
    color: "rgba(255, 255, 255, 0.8)",
    fontSize: 11,
    flex: 1,
  },
  statsRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
  },
  stat: {
    flexDirection: "row",
    alignItems: "center",
  },
  statText: {
    color: "white",
    fontSize: 11,
    marginLeft: 3,
    fontWeight: "500",
  },
});

export default FeaturedRecipesCarousel;
