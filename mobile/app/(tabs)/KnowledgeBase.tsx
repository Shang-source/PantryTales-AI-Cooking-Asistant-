import { useEffect, useMemo, useState, useCallback, useRef } from "react";
import { useFocusEffect, useLocalSearchParams, useRouter } from "expo-router";
import { View, Text, ScrollView, TouchableOpacity, FlatList, ActivityIndicator, ListRenderItem } from "react-native";
import { useAuth } from "@clerk/clerk-expo";
import { useBottomTabBarHeight } from "@react-navigation/bottom-tabs";
import { SafeAreaView, useSafeAreaInsets } from "react-native-safe-area-context";
import {
  Flame,
  Apple,
  AlertTriangle,
  Calendar,
  ChevronRight,
  ArrowLeft,
  BookOpen,
  FlaskConical,
  ChefHat,
  Utensils,
  Thermometer,
  Droplets,
  HeartPulse,
} from "lucide-react-native";
import { Skeleton } from "@/components/skeleton";
import { Card, CardContent } from "@/components/card";
import { useAuthQuery, useAuthInfiniteQuery } from "@/hooks/useApi";
import type { ApiResponse, PagedResponse } from "@/types/api";
import MainHeader from "@/components/MainHeader";
import { SearchBar } from "@/components/SearchBar";
import { useTheme } from "@/contexts/ThemeContext";

interface KnowledgebaseTag {
  id: number;
  name: string;
  displayName: string;
  icon?: string | null;
}

interface KnowledgebaseArticleListItem {
  id: string;
  title: string;
  subtitle?: string | null;
  iconName?: string | null;
}

interface KnowledgebaseArticleDetail {
  id: string;
  tagId: number;
  title: string;
  subtitle?: string | null;
  iconName?: string | null;
  content: string;
}

const getIconComponent = (
  iconName: string | null | undefined,
  size: number,
  color: string
) => {
  const props = { size, color };
  const normalized = (iconName || "").toLowerCase();

  if (normalized.includes("chem") || normalized.includes("chim") || normalized.includes("science") || normalized.includes("lab")) return <FlaskConical {...props} />;
  if (normalized.includes("cook") || normalized.includes("chef") || normalized.includes("kitchen") || normalized.includes("recipe") || normalized.includes("bake")) return <ChefHat {...props} />;
  if (normalized.includes("food") || normalized.includes("eat") || normalized.includes("utensil") || normalized.includes("diet") || normalized.includes("nutri") || normalized.includes("meal")) return <Utensils {...props} />;
  if (normalized.includes("temp") || normalized.includes("heat") || normalized.includes("therm")) return <Thermometer {...props} />;
  if (normalized.includes("liquid") || normalized.includes("water") || normalized.includes("oil") || normalized.includes("fluid")) return <Droplets {...props} />;
  if (normalized.includes("allerg") || normalized.includes("health") || normalized.includes("safe") || normalized.includes("medic")) return <HeartPulse {...props} />;

  switch (normalized) {
    case "flame":
    case "fire":
      return <Flame {...props} />;
    case "apple":
    case "fruit":
      return <Apple {...props} />;
    case "alert":
    case "alerttriangle":
    case "warning":
      return <AlertTriangle {...props} />;
    case "calendar":
    case "date":
      return <Calendar {...props} />;
    default:
      return <BookOpen {...props} />;
  }
};

const ACCENT = "#D4A5A5";
const FALLBACK_TAB_BAR_HEIGHT = 82;
const ARTICLE_COLORS = [
  "#D4A5A5",
  "#8BC5B0",
  "#F5C156",
  "#9FA6FF",
  "#F08BB4",
  "#7AD7F0",
  "#FFB7B2",
  "#B5EAD7",
  "#E2F0CB",
  "#FFDAC1",
  "#C7CEEA",
  "#FF9AA2",
  "#E0BBE4",
  "#957DAD",
  "#D291BC",
  "#FEC8D8",
  "#FFDFD3",
  "#A0E7E5",
  "#B4F8C8",
  "#FBE7C6",
];

const getArticleColor = (articleId: string | null | undefined) => {
  if (!articleId) return ACCENT;
  let hash = 0;
  for (let i = 0; i < articleId.length; i += 1) {
    hash = (hash * 31 + articleId.charCodeAt(i)) >>> 0;
  }
  return ARTICLE_COLORS[hash % ARTICLE_COLORS.length];
};

export default function KnowledgeBaseScreen() {
  const { getToken } = useAuth();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { colors } = useTheme();

  // Deep-link params from homepage cooking tips ticker
  const { articleId: paramArticleId, tagId: paramTagId } = useLocalSearchParams<{
    articleId?: string;
    tagId?: string;
  }>();
  const hasHandledDeepLink = useRef(false);
  // Track if user entered via deep-link to handle back navigation
  const enteredViaDeepLink = useRef(false);
  // Store the last handled params to detect when they change/clear
  const lastHandledParams = useRef<{ articleId?: string; tagId?: string }>({});

  let tabBarHeight = 0;
  try {
    tabBarHeight = useBottomTabBarHeight();
  } catch {
    tabBarHeight = FALLBACK_TAB_BAR_HEIGHT + insets.bottom;
  }
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedTagId, setSelectedTagId] = useState<number | null>(null);
  const [selectedArticleId, setSelectedArticleId] = useState<string | null>(
    null
  );
  const [selectedArticleMeta, setSelectedArticleMeta] =
    useState<KnowledgebaseArticleListItem | null>(null);

  const { data: tagResponse, isLoading: tagsLoading, error: tagsError} = useAuthQuery<ApiResponse<KnowledgebaseTag[]>>(
    ["knowledgebase-tags"],
    "/api/knowledgebase/taglist"
  );

  const tags = useMemo(() => tagResponse?.data ?? [], [tagResponse]);

  useFocusEffect(
    useCallback(() => {
      return () => {
        setSearchTerm("");
        setDebouncedSearch("");
        setSelectedTagId(null);
        // Reset deep-link tracking when leaving the screen
        // This ensures fresh navigation from "Cooking Tips" button works correctly
        hasHandledDeepLink.current = false;
        enteredViaDeepLink.current = false;
        lastHandledParams.current = {};
      };
    }, [])
  );

  useEffect(() => {
    const trimmed = searchTerm.trim();
    const handle = setTimeout(() => setDebouncedSearch(trimmed), 500);
    return () => clearTimeout(handle);
  }, [searchTerm]);

  const trimmedSearch = debouncedSearch;
  const isSearching = trimmedSearch.length > 0;

  useEffect(() => {
    if (!isSearching && selectedTagId === null && tags.length > 0) {
      setSelectedTagId(tags[0].id);
    }
  }, [tags, selectedTagId, isSearching]);

  // Handle deep-link from homepage cooking tips ticker
  useEffect(() => {
    // Reset deep-link handler when params are cleared (user navigated via "Cooking Tips" button)
    const paramsCleared = !paramArticleId && !paramTagId && lastHandledParams.current.articleId;
    if (paramsCleared) {
      hasHandledDeepLink.current = false;
      enteredViaDeepLink.current = false;
      lastHandledParams.current = {};
      return;
    }

    // Handle deep-link when articleId and tagId are present
    if (
      paramArticleId &&
      paramTagId &&
      !hasHandledDeepLink.current &&
      tags.length > 0
    ) {
      hasHandledDeepLink.current = true;
      enteredViaDeepLink.current = true;
      lastHandledParams.current = { articleId: paramArticleId, tagId: paramTagId };
      const tagIdNum = parseInt(paramTagId, 10);
      if (!isNaN(tagIdNum)) {
        setSelectedTagId(tagIdNum);
        setSelectedArticleId(paramArticleId);
      }
    }
  }, [paramArticleId, paramTagId, tags.length]);

  useEffect(() => {
    if (isSearching) {
      setSelectedTagId(null);
    }
  }, [isSearching]);

  // Infinite Query for Articles by Tag
  const currentTag = useMemo(() => tags.find(t => t.id === selectedTagId), [tags, selectedTagId]);

  const {
    data: infiniteArticlesData,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isLoading: isInfiniteLoading,
    isError: isInfiniteError,
    error: infiniteError,
  } = useAuthInfiniteQuery<PagedResponse<KnowledgebaseArticleListItem>>(
    ["knowledgebase-articles-infinite", selectedTagId ? String(selectedTagId) : "none"],
    selectedTagId ? `/api/knowledgebase/articlelist/${selectedTagId}` : "", 
    getToken,
    { enabled: !!selectedTagId }
  );

  // Search Query
  const {
    data: searchResponse,
    isLoading: searchLoading,
    error: searchError,
  } = useAuthQuery<ApiResponse<KnowledgebaseArticleDetail[]>>(
    ["knowledgebase-search", trimmedSearch],
    isSearching
      ? `/api/knowledgebase/articles/search?keyword=${encodeURIComponent(
          trimmedSearch
        )}`
      : "",
    { enabled: isSearching }
  );

  const searchArticles = searchResponse?.data ?? [];
  
  // Flatten infinite query data
  const flattenedArticles = useMemo(() => {
    if (!infiniteArticlesData) return [];
    
    const allItems = infiniteArticlesData.pages.flatMap((page) => {
        // Only support standard ApiResponse<PagedResponse> format
        return page?.data?.items ?? [];
    });

    // Deduplicate by ID
    const uniqueMap = new Map();
    allItems.forEach((item) => {
        if (item && item.id) {
            uniqueMap.set(item.id, item);
        }
    });
    return Array.from(uniqueMap.values());

  }, [infiniteArticlesData]);

  const displayArticles = isSearching
    ? searchArticles.map((item) => ({
        id: item.id,
        title: item.title,
        subtitle: item.subtitle,
        iconName: item.iconName,
      }))
    : flattenedArticles;

  const displayLoading = isSearching ? searchLoading : isInfiniteLoading;
  const displayError = isSearching ? searchError : isInfiniteError ? infiniteError : null;

  const {
    data: articleDetailResponse,
    isLoading: detailLoading,
    error: detailError,
  } = useAuthQuery<ApiResponse<KnowledgebaseArticleDetail>>(
    ["knowledgebase-article", selectedArticleId ?? "none"],
    selectedArticleId ? `/api/knowledgebase/article/${selectedArticleId}` : "",
    { enabled: selectedArticleId !== null }
  );

  const articleDetail = articleDetailResponse?.data ?? null;

  const handleSelectTag = (tagId: number) => {
    setSelectedTagId(tagId);
    setSearchTerm("");
    setSelectedArticleId(null);
    setSelectedArticleMeta(null);
  };

  const handleOpenArticle = useCallback((item: KnowledgebaseArticleListItem) => {
    setSelectedArticleMeta(item);
    setSelectedArticleId(item.id);
  }, []);

  const renderArticleItem = useCallback(({ item }: { item: KnowledgebaseArticleListItem }) => (
    <TouchableOpacity
      onPress={() => handleOpenArticle(item)}
      activeOpacity={0.7}
      style={{ marginBottom: 12 }}
    >
      <Card className="w-full rounded-2xl p-4 border mx-0 mb-0 backdrop-blur-none" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
        <CardContent className="p-0">
          <View className="flex-row items-start gap-3">
            <View className="mt-0.5">
              {getIconComponent(
                item.iconName || currentTag?.icon || currentTag?.displayName || currentTag?.name,
                24,
                getArticleColor(item.id)
              )}
            </View>

            <View className="flex-1">
              <Text className="text-base font-bold mb-1" style={{ color: colors.textPrimary }}>
                {item.title}
              </Text>
              {item.subtitle ? (
                <Text
                  className="text-sm"
                  numberOfLines={2}
                  style={{ color: colors.textSecondary }}
                >
                  {item.subtitle}
                </Text>
              ) : null}
            </View>

            <View className="self-center">
              <ChevronRight
                size={20}
                color={colors.textMuted}
              />
            </View>
          </View>
        </CardContent>
      </Card>
    </TouchableOpacity>
  ), [currentTag, handleOpenArticle, colors]);

  // Detail View
  if (selectedArticleId) {
    const title =
      articleDetail?.title ?? selectedArticleMeta?.title ?? "Article";
    const subtitle =
      articleDetail?.subtitle ?? selectedArticleMeta?.subtitle ?? "";
    const iconName =
      articleDetail?.iconName ?? selectedArticleMeta?.iconName ?? undefined;
    const articleColor = getArticleColor(selectedArticleId);
    
    const contentLines =
        articleDetail?.content
        ?.split(/\r?\n/)
        .map((line) => line.trim())
        .filter(Boolean) ?? [];

    return (
      <SafeAreaView
        style={{ flex: 1, backgroundColor: colors.bg, position: "relative" }}
        edges={["top", "left", "right"]}
      >
        <View className="flex-1">
          <MainHeader />
          <View className="border-b px-4 py-4" style={{ borderBottomColor: colors.border }}>
            <TouchableOpacity
              onPress={() => {
                if (enteredViaDeepLink.current) {
                  // Navigate back to homepage if entered via deep-link
                  // Reset all deep-link tracking state
                  enteredViaDeepLink.current = false;
                  hasHandledDeepLink.current = false;
                  lastHandledParams.current = {};
                  // Clear the article to show list view, then navigate back
                  setSelectedArticleId(null);
                  router.back();
                } else {
                  // Just close the article detail view
                  setSelectedArticleId(null);
                }
              }}
              className="flex-row items-center"
              activeOpacity={0.7}
            >
              <ArrowLeft size={20} color={colors.accent} />
              <Text className="ml-1 text-base font-medium" style={{ color: colors.accent }}>
                Back
              </Text>
            </TouchableOpacity>
          </View>

          <ScrollView
            className="flex-1"
            showsVerticalScrollIndicator={false}
            contentContainerClassName="p-4"
            contentContainerStyle={{ paddingBottom: tabBarHeight + 24 }}
          >
            <Card className="rounded-2xl p-6 border mb-8 mx-0 backdrop-blur-none" style={{ backgroundColor: colors.card, borderColor: colors.border }}>
              <CardContent className="p-0">
                <View className="flex-row items-start gap-4 mb-4">
                  <View className="mt-1">
                    {getIconComponent(iconName, 24, articleColor)}
                  </View>
                  <Text className="text-xl font-bold flex-1" style={{ color: colors.textPrimary }}>{title}</Text>
                </View>
                {subtitle ? (
                  <Text className="mb-6 text-base leading-6" style={{ color: colors.textSecondary }}>
                    {subtitle}
                  </Text>
                ) : null}

                <View className="gap-3">
                    {detailLoading ? (
                         <ActivityIndicator color={colors.accent} />
                    ) : contentLines.length > 0 ? (
                        contentLines.map((text, index) => (
                        <View key={index} className="flex-row gap-3">
                            <Text className="text-lg leading-6" style={{ color: colors.accent }}>-</Text>
                            <Text className="text-base flex-1 leading-6" style={{ color: colors.textPrimary }}>{text}</Text>
                        </View>
                        ))
                    ) : (
                         <Text className="text-base" style={{ color: colors.textSecondary }}>No content available.</Text>
                    )}
                </View>
              </CardContent>
            </Card>
          </ScrollView>
        </View>
      </SafeAreaView>
    );
  }

  return <KnowledgeBaseList
            tags={tags}
            tagsLoading={tagsLoading}
            tagsError={tagsError}
            selectedTagId={selectedTagId}
            onSelectTag={handleSelectTag}
            searchTerm={searchTerm}
            setSearchTerm={setSearchTerm}
            displayArticles={displayArticles}
            isLoading={displayLoading}
            error={displayError}
            tabBarHeight={tabBarHeight}
            renderArticleItem={renderArticleItem}
            isSearching={isSearching}
            fetchNextPage={fetchNextPage}
            hasNextPage={hasNextPage}
            isFetchingNextPage={isFetchingNextPage}
            setDebouncedSearch={setDebouncedSearch}
            bgColor={colors.bg}
            colors={colors}
         />;
}

interface KnowledgeBaseListProps {
  tags: KnowledgebaseTag[];
  tagsLoading: boolean;
  tagsError: unknown;
  selectedTagId: number | null;
  onSelectTag: (tagId: number) => void;
  searchTerm: string;
  setSearchTerm: (text: string) => void;
  displayArticles: KnowledgebaseArticleListItem[];
  isLoading: boolean;
  error: unknown;
  tabBarHeight: number;
  renderArticleItem: ListRenderItem<KnowledgebaseArticleListItem>;
  isSearching: boolean;
  fetchNextPage: () => void;
  hasNextPage: boolean | undefined;
  isFetchingNextPage: boolean;
  setDebouncedSearch: (text: string) => void;
  bgColor: string;
  colors: any;
}

function KnowledgeBaseList({
    tags, tagsLoading, tagsError, selectedTagId, onSelectTag,
    searchTerm, setSearchTerm,
    displayArticles, isLoading, error,
    tabBarHeight, renderArticleItem, isSearching,
    fetchNextPage, hasNextPage, isFetchingNextPage,
    setDebouncedSearch,
    bgColor,
    colors,
}: KnowledgeBaseListProps) {

    return (
    <SafeAreaView
      style={{ flex: 1, backgroundColor: bgColor, position: "relative" }}
      edges={["top", "left", "right"]}
    >
      <View className="flex-1">
        <MainHeader />
        <View className="px-4 pt-2 pb-4 border-b" style={{ borderBottomColor: colors.border }}>
          <SearchBar
            placeholder="Search articles or tags"
            value={searchTerm}
            onChangeText={setSearchTerm}
            onSubmitEditing={() => setDebouncedSearch(searchTerm.trim())}
            returnKeyType="search"
            blurOnSubmit
          />
        </View>

        {/* Tags Section */}
        <View className="border-b py-4 h-[72px] justify-center" style={{ borderBottomColor: colors.border }}>
            {tagsLoading ? (
                <View className="flex-row px-4 gap-3">
                   <Skeleton className="h-[42px] w-28 rounded-full" style={{ backgroundColor: colors.card }} />
                   <Skeleton className="h-[42px] w-28 rounded-full" style={{ backgroundColor: colors.card }} />
                   <Skeleton className="h-[42px] w-28 rounded-full" style={{ backgroundColor: colors.card }} />
                </View>
            ) : (
                <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerClassName="px-4">
                     <View className="flex-row gap-3">
                        {tags.map((tag: any) => (
                           <TouchableOpacity
                             key={tag.id}
                             onPress={() => onSelectTag(tag.id)}
                             className="flex-row items-center gap-2 px-4 py-2.5 rounded-full border"
                             style={{
                               backgroundColor: selectedTagId === tag.id ? colors.accent : colors.card,
                               borderColor: selectedTagId === tag.id ? colors.accent : colors.border,
                             }}
                           >
                             {getIconComponent(tag.icon || tag.displayName || tag.name, 20, selectedTagId === tag.id ? colors.bg : colors.textPrimary)}
                             <Text className="font-medium text-sm" style={{ color: selectedTagId === tag.id ? colors.bg : colors.textPrimary }}>{tag.displayName || tag.name}</Text>
                           </TouchableOpacity>
                        ))}
                     </View>
                </ScrollView>
            )}
        </View>

        {/* List Section */}
        <View className="flex-1 px-4">
             {isLoading && displayArticles.length === 0 ? (
                <View className="mt-4 gap-3">
                   <Skeleton className="h-24 w-full rounded-2xl" style={{ backgroundColor: colors.card }} />
                   <Skeleton className="h-24 w-full rounded-2xl" style={{ backgroundColor: colors.card }} />
                   <Skeleton className="h-24 w-full rounded-2xl" style={{ backgroundColor: colors.card }} />
                </View>
             ) : error ? (
                <View className="p-4">
                  <Text style={{ color: colors.textSecondary }}>Failed to load articles.</Text>
                </View>
             ) : (
                <FlatList
                    data={displayArticles}
                    showsVerticalScrollIndicator={false}
                    renderItem={renderArticleItem}
                    keyExtractor={(item) => item.id}
                    contentContainerStyle={{ paddingBottom: tabBarHeight + 24, paddingTop: 16 }}
                    onEndReached={() => {
                        if (hasNextPage && !isFetchingNextPage && !isSearching) {
                            fetchNextPage();
                        }
                    }}
                    onEndReachedThreshold={0.5}
                    ListFooterComponent={
                        isFetchingNextPage ? (
                            <View className="py-4">
                                <ActivityIndicator color={colors.accent} />
                            </View>
                        ) : null
                    }
                    ListEmptyComponent={
                        <Text className="text-center mt-10" style={{ color: colors.textSecondary }}>
                            {isSearching ? "No articles found." : "No articles in this category."}
                        </Text>
                    }
                />
             )}
        </View>
      </View>
    </SafeAreaView>
    );
}
