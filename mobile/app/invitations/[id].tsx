import React, { useEffect, useState, useRef } from "react";
import { View, Text, ActivityIndicator } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { useLocalSearchParams, useRouter } from "expo-router";
import { useAuth } from "@clerk/clerk-expo";
import { Ionicons } from "@expo/vector-icons";
import AsyncStorage from "@react-native-async-storage/async-storage";
import { useQueryClient } from "@tanstack/react-query";

import { Button } from "@/components/Button";
import { useAuthMutation } from "@/hooks/useApi";
import { ApiResponse } from "@/types/api";
import { PENDING_INVITATION_KEY } from "@/constants/constants";
import { useTheme } from "@/contexts/ThemeContext";

type AcceptStatus = "loading" | "success" | "error" | "needsLogin";

interface HouseholdMembershipDto {
  householdId: string;
  userId: string;
  role: string;
  joinedAt: string;
}

export default function AcceptInvitationScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { isSignedIn, isLoaded } = useAuth();
  const queryClient = useQueryClient();
  const { colors } = useTheme();

  const [status, setStatus] = useState<AcceptStatus>("loading");
  const [message, setMessage] = useState("");

  // Use a ref to track if we've already started the accept process
  const hasStartedRef = useRef(false);

  // Debug log when component mounts (only once)
  useEffect(() => {
    console.log("[AcceptInvitation] Screen mounted with id:", id);
  }, [id]);

  // Memoize the accept path to prevent recreating mutation on every render
  const acceptPath = React.useMemo(() => {
    if (!id) return "";
    return `/api/households/invitations/${id}/accept`;
  }, [id]);
  
  console.log("[AcceptInvitation] ID:", id);
  console.log("[AcceptInvitation] Accept path:", acceptPath);
  
  const acceptMutation = useAuthMutation<
    ApiResponse<HouseholdMembershipDto>,
    void
  >(acceptPath, "POST");

  // Retry function for error state
  const acceptInvitation = async () => {
    hasStartedRef.current = false; // Reset ref to allow retry
    setStatus("loading");
    setMessage("");

    try {
      console.log("[AcceptInvitation] Retrying accept API...");
      const result = await acceptMutation.mutateAsync(undefined);
      console.log("[AcceptInvitation] Retry result:", JSON.stringify(result));

      const isApiSuccess =
        (result.code === 0 || result.code === 200) && !!result.data;

      if (isApiSuccess) {
        queryClient.removeQueries({ queryKey: ["household-members"] });
        queryClient.removeQueries({ queryKey: ["inventory"] });
        queryClient.removeQueries({ queryKey: ["checklist"] });
        await queryClient.refetchQueries({ queryKey: ["households-me"] });

        // Navigate directly to household page
        console.log("[AcceptInvitation] Retry success, navigating to household...");
        router.replace("/(settings)/familymember");
        return;
      } else {
        setStatus("error");
        setMessage(result.message || "Failed to accept invitation");
      }
    } catch (error: any) {
      const apiMessage = error?.payload?.message || error?.message;
      const isAlreadyAccepted = error?.status === 400 &&
        (apiMessage?.includes("cannot be accepted") || apiMessage?.includes("already"));

      if (isAlreadyAccepted) {
        console.log("[AcceptInvitation] Invitation already accepted, navigating to household...");
        await queryClient.refetchQueries({ queryKey: ["households-me"] });
        router.replace("/(settings)/familymember");
        return;
      } else {
        console.error("[AcceptInvitation] Retry error:", error);
        setStatus("error");
        setMessage(
          apiMessage || "An error occurred while accepting the invitation",
        );
      }
    }
  };

  useEffect(() => {
    // Prevent running if not loaded yet
    if (!isLoaded) return;

    // Handle not signed in case
    if (!isSignedIn) {
      setStatus("needsLogin");
      setMessage("Please sign in to accept this invitation");
      return;
    }

    // Prevent duplicate calls - only run once
    if (hasStartedRef.current) {
      console.log("[AcceptInvitation] Already started, skipping...");
      return;
    }

    // No id provided
    if (!id) {
      console.log("[AcceptInvitation] No id provided");
      setStatus("error");
      setMessage("Invalid invitation link");
      return;
    }

    // Wait for accept path to be ready
    if (!acceptPath) {
      console.log("[AcceptInvitation] Accept path not ready yet, waiting...");
      return;
    }

    // Mark as started before doing anything async
    hasStartedRef.current = true;
    console.log("[AcceptInvitation] Starting accept process for id:", id);

    // Accept the invitation
    const doAccept = async () => {
      setStatus("loading");

      try {
        console.log("[AcceptInvitation] Calling accept API...");
        const result = await acceptMutation.mutateAsync(undefined);
        console.log("[AcceptInvitation] API result:", JSON.stringify(result));

        const isApiSuccess =
          (result.code === 0 || result.code === 200) && !!result.data;

        if (isApiSuccess) {
          // First, reset all household-related queries to clear stale data
          queryClient.removeQueries({ queryKey: ["household-members"] });
          queryClient.removeQueries({ queryKey: ["inventory"] });
          queryClient.removeQueries({ queryKey: ["checklist"] });

          // Then invalidate households-me to get the new activeHouseholdId
          await queryClient.refetchQueries({ queryKey: ["households-me"] });

          // Navigate directly to household page
          console.log("[AcceptInvitation] Success, navigating to household...");
          router.replace("/(settings)/familymember");
          return;
        } else {
          setStatus("error");
          setMessage(result.message || "Failed to accept invitation");
        }
      } catch (error: any) {
        const apiMessage = error?.payload?.message || error?.message;
        const isAlreadyAccepted = error?.status === 400 &&
          (apiMessage?.includes("cannot be accepted") || apiMessage?.includes("already"));

        if (isAlreadyAccepted) {
          // Invitation was already accepted - auto-navigate to household
          console.log("[AcceptInvitation] Invitation already accepted, navigating to household...");
          await queryClient.refetchQueries({ queryKey: ["households-me"] });
          router.replace("/(settings)/familymember");
          return;
        } else {
          console.error("[AcceptInvitation] Error:", error);
          console.error("[AcceptInvitation] Error payload:", error?.payload);
          setStatus("error");
          setMessage(
            apiMessage || "An error occurred while accepting the invitation",
          );
        }
      }
    };

    doAccept();
  }, [isLoaded, isSignedIn, id, acceptPath]);

  const goToHousehold = async () => {
    // Wait for households-me to be refetched before navigating
    // This ensures we have the correct activeHouseholdId
    console.log("[AcceptInvitation] Refetching households-me before navigation...");
    await queryClient.refetchQueries({ 
      queryKey: ["households-me"],
      exact: true,
    });
    console.log("[AcceptInvitation] Navigation to household page...");
    router.replace("/(settings)/familymember");
  };

  const goToSignIn = async () => {
    // Save the invitation ID so we can return after login
    if (id) {
      await AsyncStorage.setItem(PENDING_INVITATION_KEY, id);
    }
    router.push("/(auth)/sign-in");
  };

  const getStatusIcon = () => {
    switch (status) {
      case "success":
        return <Ionicons name="checkmark-circle" size={80} color={colors.success} />;
      case "error":
        return <Ionicons name="close-circle" size={80} color={colors.error} />;
      case "needsLogin":
        return <Ionicons name="person-circle" size={80} color="#FF9800" />;
      default:
        return <ActivityIndicator size="large" color={colors.accent} />;
    }
  };

  const getStatusTitle = () => {
    switch (status) {
      case "loading":
        return "Accepting Invitation...";
      case "success":
        return "Welcome!";
      case "error":
        return "Oops!";
      case "needsLogin":
        return "Sign In Required";
      default:
        return "";
    }
  };

  return (
    <SafeAreaView className="flex-1" style={{ backgroundColor: colors.bg }}>
      <View className="flex-1 items-center justify-center px-8">
        {/* Icon */}
        <View className="mb-6">{getStatusIcon()}</View>

        {/* Title */}
        <Text className="text-2xl font-bold mb-4 text-center" style={{ color: colors.textPrimary }}>
          {getStatusTitle()}
        </Text>

        {/* Message */}
        <Text className="text-lg text-center mb-8" style={{ color: colors.textSecondary }}>
          {message}
        </Text>

        {/* Action Buttons */}
        {status === "success" && (
          <Button onPress={goToHousehold} className="w-full">
            <Text className="font-semibold" style={{ color: colors.bg }}>Go to Household</Text>
          </Button>
        )}

        {status === "error" && (
          <View className="w-full gap-3">
            <Button onPress={acceptInvitation} className="w-full">
              <Text className="font-semibold" style={{ color: colors.bg }}>Try Again</Text>
            </Button>
            <Button onPress={goToHousehold} className="w-full" style={{ backgroundColor: colors.card }}>
              <Text className="font-semibold" style={{ color: colors.textPrimary }}>Go to Household</Text>
            </Button>
          </View>
        )}

        {status === "needsLogin" && (
          <Button onPress={goToSignIn} className="w-full">
            <Text className="font-semibold" style={{ color: colors.bg }}>Sign In</Text>
          </Button>
        )}
      </View>
    </SafeAreaView>
  );
}
