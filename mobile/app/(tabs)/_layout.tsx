import { Redirect, Tabs, Link } from "expo-router";
import { useUser } from "@clerk/clerk-expo";
import { Ionicons } from "@expo/vector-icons";
import { Pressable, View } from "react-native";
import { useEffect, useRef } from "react";
import { useAuthLazyQuery } from "@/hooks/useApi";
import type { ApiResponse } from "@/types/api";
import type { UserProfileResponse } from "./me";
import { useTheme } from "@/contexts/ThemeContext";

export default function TabsLayout() {
  const { isSignedIn } = useUser();
  const { colors } = useTheme();
  const didBootstrapRef = useRef(false);
  const { refetch } = useAuthLazyQuery<ApiResponse<UserProfileResponse>>(
    ["bootstrap-user-profile"],
    "/api/users/me",
    { retry: 1 },
  );

  useEffect(() => {
    if (!isSignedIn) return;
    if (didBootstrapRef.current) return;
    didBootstrapRef.current = true;
    void refetch().catch((err) => {
      console.warn("[tabs] bootstrap /api/users/me failed", err);
    });
  }, [isSignedIn, refetch]);

  if (!isSignedIn) return <Redirect href="/sign-in" />;

  return (
    <Tabs
      screenOptions={{
        tabBarStyle: {
          backgroundColor: colors.bg,
          borderTopWidth: 0,
          height: 82,
          paddingBottom: 12,
          paddingTop: 10,
        },
        tabBarActiveTintColor: colors.accent,
        tabBarInactiveTintColor: colors.textMuted,
        tabBarLabelStyle: { fontSize: 9, marginTop: 2 },
        tabBarItemStyle: { paddingHorizontal: 4, flex: 1 },
        tabBarIconStyle: { marginBottom: -2 },
        headerShown: false,
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: "Home",
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="home-outline" size={size} color={color} />
          ),
        }}
      />
      <Tabs.Screen
        name="community"
        options={{
          title: "Community",
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="people-outline" size={size} color={color} />
          ),
        }}
      />
      <Tabs.Screen
        name="add"
        options={{
          title: "",
          tabBarButton: (props) => {
            const { ref, style, children, disabled, ...rest } = props;
            return (
              <Link href="/add" asChild>
                <Pressable
                  {...rest}
                  disabled={disabled}
                  style={({ pressed }) => [
                    style,
                    {
                      opacity: pressed ? 0.9 : 1,
                      alignItems: "center",
                      justifyContent: "center",
                      marginBottom: 10,
                    },
                  ]}
                >
                  <View
                    style={{
                      width: 56,
                      height: 56,
                      borderRadius: 28,
                      backgroundColor: colors.accent,
                      alignItems: "center",
                      justifyContent: "center",
                      shadowColor: "#000",
                      shadowOpacity: 0.18,
                      shadowRadius: 8,
                      shadowOffset: { width: 0, height: 4 },
                      elevation: 5,
                    }}
                  >
                    <Ionicons name="add" size={28} color={colors.bg} />
                  </View>
                </Pressable>
              </Link>
            );
          },
        }}
      />
      <Tabs.Screen
        name="checklist"
        options={{
          title: "Checklist",
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="cart-outline" size={size} color={color} />
          ),
        }}
      />
      <Tabs.Screen
        name="me"
        options={{
          title: "Profile",
          tabBarIcon: ({ color, size }) => (
            <Ionicons name="person-outline" size={size} color={color} />
          ),
        }}
      />
      <Tabs.Screen
        name="recipe/[recipeId]"
        options={{
          // Hidden detail route accessed from the community tab
          href: null,
        }}
      />
      <Tabs.Screen
        name="cook"
        options={{
          // Hidden route: we navigate to it programmatically with a recipeId param
          href: null,
        }}
      />
      <Tabs.Screen
        name="KnowledgeBase"
        options={{
          href: null,
          headerShown: false,
        }}
      />
      <Tabs.Screen
        name="MyInventory"
        options={{
          // Hidden route: we navigate to it programmatically with a recipeId param
          href: null,
        }}
      />
      <Tabs.Screen
        name="ai-detected-recipes"
        options={{
          // Hidden route: accessed from AI Detect button on home screen
          href: null,
        }}
      />
      <Tabs.Screen
        name="smart-recipes"
        options={{
          // Hidden route: accessed from Smart Recipes button on home screen
          href: null,
        }}
      />
      <Tabs.Screen
        name="recommended-recipes"
        options={{
          // Hidden route: accessed from Recommended Recipes button on home screen
          href: null,
        }}
      />
    </Tabs>
  );
}
