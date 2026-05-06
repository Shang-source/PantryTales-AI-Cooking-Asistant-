import React, { useState, useEffect, useCallback, useRef } from "react";
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  TextInput,
  Pressable,
  Share,
  RefreshControl,
} from "react-native";
import { useAuthQuery, useAuthMutation } from "@/hooks/useApi";
import { useRouter, useLocalSearchParams, useFocusEffect } from "expo-router";
import { useQueryClient } from "@tanstack/react-query";
import * as Linking from "expo-linking";
import QRCode from "react-native-qrcode-svg";
import { ApiResponse } from "@/types/api";
import {
  HouseholdMembersListDto,
  HouseholdMemberDetailDto,
  HouseholdMemberStatus,
  HouseholdInvitationResponseDto,
  CreateLinkInvitationRequest,
  LeaveHouseholdResponseDto,
} from "@/types/household";

// Use existing components
import { Card, CardContent } from "@/components/card";
import StatCard from "@/components/ui/StatCard";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/avatar";
import { Badge } from "@/components/badge";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogFooter,
  DialogTitle,
  DialogDescription,
  DialogClose,
} from "@/components/dialog";
import { useTheme } from "@/contexts/ThemeContext";

import {
  X,
  Trash2,
  Plus,
  Mail,
  Users,
  Shield,
  Clock,
  CheckCircle,
  LogOut,
  QrCode,
  Share2,
  Copy,
} from "lucide-react-native";
import * as Clipboard from "expo-clipboard";
import { toast } from "@/components/sonner";

// Avatar background colors rotation
const AVATAR_COLORS = [
  "#C9A9A9",
  "#6B7B6B",
  "#7B8B9B",
  "#9B8B7B",
  "#8B7B9B",
  "#7B9B8B",
];

function getAvatarColor(index: number): string {
  return AVATAR_COLORS[index % AVATAR_COLORS.length];
}

// Component to poll for QR invitation status inside the dialog
interface QRInvitationPollerProps {
  householdId: string;
  linkInvitation: HouseholdInvitationResponseDto;
  initialMemberCountRef: React.MutableRefObject<number>;
  currentMemberCount: number;
  onMemberJoined: () => void; // Called when member count increases (background refresh)
  onAccepted: () => void; // Called when invitation is accepted (close dialog)
}

function QRInvitationPoller({
  householdId,
  linkInvitation,
  initialMemberCountRef,
  currentMemberCount,
  onMemberJoined,
  onAccepted,
}: QRInvitationPollerProps) {
  // Track if we've already triggered to prevent duplicate notifications
  const hasTriggeredRef = useRef(false);
  // Track if polling should stop due to error (e.g., user left household)
  const shouldStopPollingRef = useRef(false);

  // Query without automatic polling - we'll poll manually
  const { data: linkStatusResp, error, refetch: refetchLinkStatus } = useAuthQuery<ApiResponse<HouseholdInvitationResponseDto | null>>(
    ["link-invitation-status", householdId],
    `/api/households/${householdId}/invitations/link`,
    {
      staleTime: 0,
      gcTime: 0, // Don't cache this query
      retry: false, // Don't retry on error (e.g., 403)
    },
  );

  // Stop polling if we get an error (e.g., user left household)
  useEffect(() => {
    if (error) {
      console.log("[QRInvitationPoller] Error detected, stopping polling:", error);
      shouldStopPollingRef.current = true;
    }
  }, [error]);

  // Manual polling with setInterval for reliable polling
  useEffect(() => {
    console.log("[QRInvitationPoller] Starting manual polling...");

    // Fetch immediately on mount
    refetchLinkStatus();

    // Set up polling interval
    const intervalId = setInterval(() => {
      if (shouldStopPollingRef.current) {
        console.log("[QRInvitationPoller] Polling stopped due to error");
        return;
      }
      console.log("[QRInvitationPoller] Polling tick...");
      refetchLinkStatus();
    }, 2000);

    return () => {
      console.log("[QRInvitationPoller] Cleaning up polling interval");
      clearInterval(intervalId);
    };
  }, [refetchLinkStatus]);

  // Detection effect
  useEffect(() => {
    if (hasTriggeredRef.current) return;
    if (linkStatusResp === undefined) {
      console.log("[QRInvitationPoller] Waiting for first API response...");
      return;
    }

    const linkStatus = linkStatusResp?.data;
    const initialCount = initialMemberCountRef.current;

    console.log("[QRInvitationPoller] Checking status:", {
      linkStatusId: linkStatus?.id ?? 'null',
      originalId: linkInvitation?.id,
      initialCount,
      currentMemberCount,
    });

    const invitationStillExists = !!linkStatus && linkStatus.id === linkInvitation.id;
    const invitationWasAccepted = !!linkInvitation && !invitationStillExists;
    const newMemberJoined = currentMemberCount > initialCount;

    console.log("[QRInvitationPoller] Evaluation:", {
      invitationStillExists,
      invitationWasAccepted,
      newMemberJoined,
    });

    // If new member joined but invitation still exists, refresh in background
    if (newMemberJoined && invitationStillExists) {
      console.log("[QRInvitationPoller] New member detected, refreshing list...");
      onMemberJoined();
      initialMemberCountRef.current = currentMemberCount;
      return;
    }

    // If invitation was accepted (no longer exists) or member joined without invitation
    if (invitationWasAccepted || (newMemberJoined && !invitationStillExists)) {
      console.log("[QRInvitationPoller] Invitation accepted! Closing dialog...");
      hasTriggeredRef.current = true;
      onAccepted();
    }
  }, [linkStatusResp, linkInvitation, currentMemberCount, initialMemberCountRef, onMemberJoined, onAccepted]);

  return null;
}

export default function FamilyMemberScreen() {
  const router = useRouter();
  const params = useLocalSearchParams();
  const queryClient = useQueryClient();
  const { colors } = useTheme();
  const householdIdParam = (params.householdId as string) || undefined;

  // Get current household info
  const {
    data: meResp,
    isLoading: meLoading,
    refetch: refetchMe,
  } = useAuthQuery<ApiResponse<any>>(["households-me"], "/api/households/me");

  const activeHouseholdId =
    householdIdParam ??
    meResp?.data?.activeHouseholdId ??
    meResp?.data?.ActiveHouseholdId;
  const activeHouseholdName =
    meResp?.data?.activeHouseholdName ?? meResp?.data?.ActiveHouseholdName;

  // Fetch members from the new API
  // Only enable when:
  // 1. We have an activeHouseholdId
  // 2. The households-me query has finished loading (to ensure we have the correct ID)
  const membersQueryEnabled = !!activeHouseholdId && !meLoading;
  
  const {
    data: membersResp,
    isLoading: membersLoading,
    refetch: refetchMembers,
    error: membersError,
  } = useAuthQuery<ApiResponse<HouseholdMembersListDto>>(
    ["household-members", activeHouseholdId],
    activeHouseholdId ? `/api/households/${activeHouseholdId}/members` : "",
    { 
      enabled: membersQueryEnabled,
      retry: (failureCount, error: any) => {
        // Don't retry on 404 (household not found) or 403 (not a member)
        if (error?.status === 404 || error?.status === 403) {
          console.log("[FamilyMember] Not retrying due to:", error?.status);
          return false;
        }
        return failureCount < 3;
      },
    },
  );

  // Track if background polling should stop due to error
  const stopMemberPollingRef = useRef(false);

  // Reset polling stop flag when household changes
  useEffect(() => {
    stopMemberPollingRef.current = false;
  }, [activeHouseholdId]);

  // Background polling to keep member list up to date
  useEffect(() => {
    if (!activeHouseholdId) return;

    console.log("[FamilyMember] Starting background polling for members...");
    const interval = setInterval(() => {
      if (stopMemberPollingRef.current) {
        console.log("[FamilyMember] Background polling stopped due to error");
        return;
      }
      console.log("[FamilyMember] Background refresh of members...");
      refetchMembers();
    }, 5000); // Poll every 5 seconds

    return () => {
      console.log("[FamilyMember] Stopping background polling");
      clearInterval(interval);
    };
  }, [activeHouseholdId, refetchMembers]);

  // Handle members fetch error
  useEffect(() => {
    if (membersError) {
      const status = (membersError as any)?.status;
      if (status === 404 || status === 403) {
        // Household not found or not a member - stop polling and refresh user info
        // This is expected during leave operations, so don't log as error
        console.log("[FamilyMember] Stopping polling due to:", status);
        stopMemberPollingRef.current = true;
        queryClient.removeQueries({ queryKey: ["household-members"] });
        refetchMe();
      } else {
        console.error("[FamilyMember] Members fetch error:", membersError);
      }
    }
  }, [membersError, queryClient, refetchMe]);

  const membersData = membersResp?.data;
  const members: HouseholdMemberDetailDto[] = membersData?.members ?? [];
  const activeMemberCount = membersData?.activeMemberCount ?? 0;
  const pendingCount = membersData?.pendingCount ?? 0;

  // State for QR code dialog (declared before useFocusEffect which references it)
  const [qrDialogOpen, setQrDialogOpen] = useState(false);
  const qrDialogOpenRef = useRef(qrDialogOpen);
  qrDialogOpenRef.current = qrDialogOpen;

  // Refresh data when screen gains focus
  useFocusEffect(
    useCallback(() => {
      console.log("[FamilyMember] Screen focused, refreshing data...");

      // Close QR code dialog (in case invitee just accepted and navigated back)
      if (qrDialogOpenRef.current) {
        console.log("[FamilyMember] Closing QR dialog on focus...");
        setQrDialogOpen(false);
      }

      // Only refetch households-me first, then members will be refetched
      // automatically when activeHouseholdId updates via the enabled dependency
      refetchMe();
    }, [refetchMe]),
  );

  // Refetch members when activeHouseholdId changes
  useEffect(() => {
    if (activeHouseholdId) {
      refetchMembers();
    }
  }, [activeHouseholdId, refetchMembers]);

  const [inviteEmail, setInviteEmail] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const inviteEmailIsValid = /^\S+@\S+\.\S+$/.test(inviteEmail.trim());

  const [linkInvitation, setLinkInvitation] =
    useState<HouseholdInvitationResponseDto | null>(null);
  
  // Track initial member count using ref to avoid async state issues
  const initialMemberCountRef = useRef<number>(0);

  // State for delete confirmation dialog
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [memberToDelete, setMemberToDelete] =
    useState<HouseholdMemberDetailDto | null>(null);

  // State for leave household confirmation dialog
  const [leaveDialogOpen, setLeaveDialogOpen] = useState(false);

  // Pull to refresh state
  const [refreshing, setRefreshing] = useState(false);

  // Pull to refresh handler
  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    console.log("[FamilyMember] Pull to refresh triggered");
    try {
      await refetchMe();
      if (activeHouseholdId) {
        await refetchMembers();
      }
    } finally {
      setRefreshing(false);
    }
  }, [refetchMe, refetchMembers, activeHouseholdId]);

  // Check if current user is the owner of this household
  const currentUserIsOwner =
    meResp?.data?.memberships?.some(
      (membership: any) =>
        membership.householdId === activeHouseholdId && membership.isOwner,
    ) ?? false;

  // Mutation for sending invitations
  const inviteMutation = useAuthMutation<
    ApiResponse<HouseholdInvitationResponseDto>,
    { email: string; expirationDays: number }
  >(
    activeHouseholdId ? `/api/households/${activeHouseholdId}/invitations` : "",
    "POST",
    {
      onSuccess: () => {
        console.log("[FamilyMember] Invite success");
        setInviteEmail("");
        setDialogOpen(false);
        refetchMembers();
      },
      onError: (error) => {
        console.error("[FamilyMember] Invite failed", error);
        setDialogOpen(false);
        setInviteEmail("");
        toast.error(
          "Invitation Failed",
          error instanceof Error
            ? error.message
            : "Failed to send invitation. Please try again.",
        );
      },
    },
  );

  // Mutation for removing members
  const removeMemberMutation = useAuthMutation<ApiResponse<object>, void>(
    memberToDelete && activeHouseholdId
      ? `/api/households/${activeHouseholdId}/members/${memberToDelete.id}`
      : "",
    "DELETE",
    {
      onSuccess: () => {
        console.log("[FamilyMember] Delete success");
        setDeleteDialogOpen(false);
        setMemberToDelete(null);
        refetchMembers();
      },
      onError: (error) => {
        console.error("[FamilyMember] Delete failed", error);
        setDeleteDialogOpen(false);
        setMemberToDelete(null);
        toast.error(
          "Remove Failed",
          error instanceof Error
            ? error.message
            : "Failed to remove member. Please try again.",
        );
      },
    },
  );

  // Mutation for creating link invitation
  const createLinkInvitationMutation = useAuthMutation<
    ApiResponse<HouseholdInvitationResponseDto>,
    CreateLinkInvitationRequest & { householdId: string }
  >(
    (body) => `/api/households/${body.householdId}/invitations/link`,
    "POST",
    {
      onSuccess: (response) => {
        if (response.data) {
          setLinkInvitation(response.data);
        }
      },
      onError: (error) => {
        console.error("[FamilyMember] Create link invitation failed", error);
        toast.error(
          "QR Code Failed",
          error instanceof Error
            ? error.message
            : "Failed to create QR code invitation.",
        );
      },
    },
  );

  // Mutation for leaving household
  const leaveHouseholdMutation = useAuthMutation<
    ApiResponse<LeaveHouseholdResponseDto>,
    void
  >(
    activeHouseholdId ? `/api/households/${activeHouseholdId}/members/me` : "",
    "DELETE",
    {
      onSuccess: async (response) => {
        console.log("[FamilyMember] Leave household success", response);
        setLeaveDialogOpen(false);
        // Stop background polling immediately to prevent 403 errors
        stopMemberPollingRef.current = true;
        // Remove cached data for the old household (don't invalidate - that would refetch and cause 403)
        queryClient.removeQueries({ queryKey: ["household-members"] });
        queryClient.removeQueries({ queryKey: ["link-invitation-status"] });
        queryClient.removeQueries({ queryKey: ["inventory"] });
        queryClient.removeQueries({ queryKey: ["inventory-all"] });
        queryClient.removeQueries({ queryKey: ["checklist"] });
        // Invalidate households-me to get the new household assignment
        // Wait for it to complete so the page refreshes with new data
        await queryClient.invalidateQueries({ queryKey: ["households-me"] });
        // Reset polling flag so it can resume with new household
        stopMemberPollingRef.current = false;
        // Stay on the page - it will show the new household or empty state
      },
      onError: (error) => {
        console.error("[FamilyMember] Leave household failed", error);
        setLeaveDialogOpen(false);
        // Resume polling since leave failed
        stopMemberPollingRef.current = false;
        toast.error(
          "Leave Failed",
          error instanceof Error
            ? error.message
            : "Failed to leave household. Please try again.",
        );
      },
    },
  );

  useEffect(() => {
    setDialogOpen(false);
    setQrDialogOpen(false);
    setDeleteDialogOpen(false);
    setLeaveDialogOpen(false);
    setLinkInvitation(null);
  }, [activeHouseholdId]);

  const handleLeaveHousehold = () => {
    if (!activeHouseholdId) {
      return;
    }
    console.log("[FamilyMember] Leaving household:", activeHouseholdId);
    // Stop polling immediately before making the leave request
    stopMemberPollingRef.current = true;
    // Cancel any in-flight requests to prevent 403 errors
    queryClient.cancelQueries({ queryKey: ["household-members", activeHouseholdId] });
    queryClient.cancelQueries({ queryKey: ["link-invitation-status", activeHouseholdId] });
    leaveHouseholdMutation.mutate(undefined as unknown as void);
  };

  const handleSendInvite = () => {
    if (!inviteEmail.trim()) {
      console.log("[FamilyMember] No email entered");
      return;
    }
    if (!activeHouseholdId) {
      console.error("[FamilyMember] No activeHouseholdId available");
      return;
    }

    console.log(
      "[FamilyMember] Sending invite to:",
      inviteEmail,
      "householdId:",
      activeHouseholdId,
    );

    inviteMutation.mutate({ email: inviteEmail, expirationDays: 7 });
  };

  const handleDeleteMember = () => {
    if (!memberToDelete || !activeHouseholdId) {
      return;
    }

    console.log(
      "[FamilyMember] Deleting member:",
      memberToDelete.id,
      "from household:",
      activeHouseholdId,
    );

    removeMemberMutation.mutate(undefined as unknown as void);
  };

  const handleClose = () => {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace("/(tabs)");
    }
  };

  const handleOpenQrDialog = () => {
    if (!activeHouseholdId) {
      toast.error("Error", "No active household found");
      return;
    }
    // Store current member count using ref to avoid async state issues
    initialMemberCountRef.current = members.length;
    console.log("[FamilyMember] Opening QR dialog, initial member count:", members.length);
    setQrDialogOpen(true);
    // Just create/refresh - backend returns existing invitation if one exists
    createLinkInvitationMutation.mutate({
      householdId: activeHouseholdId,
      expirationDays: 7,
    });
  };

  const getInviteDeepLink = () => {
    if (!linkInvitation?.token) return "";
    // Use Linking.createURL to generate correct URL for both Expo Go and production builds
    return Linking.createURL(`invitations/token/${linkInvitation.token}`);
  };

  const handleCopyLink = async () => {
    const link = getInviteDeepLink();
    if (link) {
      await Clipboard.setStringAsync(link);
      toast.success("Link Copied", "Invitation link copied to clipboard");
    }
  };

  const handleShareLink = async () => {
    const link = getInviteDeepLink();
    if (link) {
      try {
        await Share.share({
          message: `Join my family on PantryTales! ${link}`,
        });
      } catch (error) {
        console.error("[FamilyMember] Share failed", error);
      }
    }
  };

  const formatExpirationDate = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString("en-US", {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "numeric",
        minute: "2-digit",
      });
    } catch {
      return dateStr;
    }
  };

  const formatJoinDate = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString("en-US", {
        year: "numeric",
        month: "short",
        day: "numeric",
      });
    } catch {
      return dateStr;
    }
  };

  const getStatusBadge = (status: HouseholdMemberStatus) => {
    switch (status) {
      case "owner":
        return (
          <Badge
            className="bg-[#FEF3C7] border-transparent"
            textClassName="text-[#D97706]"
          >
            <Shield size={12} color="#D97706" />
            Owner
          </Badge>
        );
      case "joined":
        return (
          <Badge
            className="bg-[#D1FAE5] border-transparent"
            textClassName="text-[#059669]"
          >
            <CheckCircle size={12} color="#059669" />
            Joined
          </Badge>
        );
      case "pending":
        return (
          <Badge
            className="bg-[#FEE2E2] border-transparent"
            textClassName="text-[#DC2626]"
          >
            <Clock size={12} color="#DC2626" />
            Pending
          </Badge>
        );
    }
  };

  return (
    <View className="flex-1" style={{ backgroundColor: colors.bg }}>
      {/* Top Navigation Bar */}
      <View className="flex-row items-center justify-between px-4 pt-14 pb-3">
        <TouchableOpacity
          onPress={handleClose}
          className="w-9 h-9 items-center justify-center -ml-1"
          accessibilityLabel="Close"
          accessibilityRole="button"
        >
          <X size={20} color={colors.textPrimary} />
        </TouchableOpacity>

        {/* Invite Buttons (for owner) or Leave Button (for member) */}
        {currentUserIsOwner ? (
          <View className="flex-row items-center gap-2 mt-4">
            <TouchableOpacity
              onPress={handleOpenQrDialog}
              disabled={!activeHouseholdId}
              className="flex-row items-center px-3 py-2 rounded-lg"
              style={{ opacity: activeHouseholdId ? 1 : 0.5, backgroundColor: colors.card }}
            >
              <QrCode size={16} color={colors.textPrimary} />
              <Text className="font-medium ml-2" style={{ color: colors.textPrimary }}>QR Code</Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={() => {
                console.log("[FamilyMember] Invite button pressed");
                setDialogOpen(true);
              }}
              className="flex-row items-center px-3 py-2 rounded-lg"
              style={{ backgroundColor: colors.card }}
            >
              <Plus size={16} color={colors.textPrimary} />
              <Text className="font-medium ml-2" style={{ color: colors.textPrimary }}>Email</Text>
            </TouchableOpacity>
          </View>
        ) : (
          <TouchableOpacity
            onPress={() => {
              console.log("[FamilyMember] Leave button pressed");
              setLeaveDialogOpen(true);
            }}
            className="flex-row items-center px-4 py-2 rounded-lg mt-4"
            style={{ backgroundColor: colors.card }}
          >
            <LogOut size={16} color={colors.textPrimary} />
            <Text className="font-medium ml-2" style={{ color: colors.textPrimary }}>Leave Family</Text>
          </TouchableOpacity>
        )}

        <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
          <DialogContent>
            <DialogHeader className="items-start">
              <DialogTitle className="text-left">
                Invite Family Member
              </DialogTitle>
              <DialogDescription className="text-left">
                Enter the email address of the member you want to invite. Once
                they accept the invitation, they can share the family inventory.
              </DialogDescription>
            </DialogHeader>

            <View className="mt-2">
              <Text className="text-sm font-medium mb-2" style={{ color: colors.textPrimary }}>
                Email Address
              </Text>
              <View className="flex-row items-center border rounded-lg px-3 py-3" style={{ borderColor: colors.border }}>
                <Mail size={18} color={colors.textMuted} />
                <TextInput
                  value={inviteEmail}
                  onChangeText={setInviteEmail}
                  placeholder="member@example.com"
                  placeholderTextColor={colors.textMuted}
                  keyboardType="email-address"
                  autoCapitalize="none"
                  autoComplete="email"
                  className="flex-1 ml-2"
                  style={{ fontSize: 16, lineHeight: 20, color: colors.textPrimary }}
                />
              </View>
            </View>

            <DialogFooter>
              <DialogClose className="flex-1 py-3 rounded-xl border items-center justify-center h-12" style={{ borderColor: colors.border }}>
                <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
              </DialogClose>
              <Pressable
                className="flex-1 py-3 rounded-xl items-center justify-center h-12"
                onPress={handleSendInvite}
                disabled={
                  inviteMutation.isPending ||
                  !inviteEmail.trim() ||
                  !inviteEmailIsValid
                }
                style={{
                  backgroundColor: colors.accent,
                  opacity:
                    inviteMutation.isPending ||
                    !inviteEmail.trim() ||
                    !inviteEmailIsValid
                      ? 0.5
                      : 1,
                }}
              >
                <Text className="font-medium" style={{ color: colors.bg }}>
                  {inviteMutation.isPending ? "Sending..." : "Send Invitation"}
                </Text>
              </Pressable>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </View>

      <ScrollView 
        contentContainerStyle={{ paddingBottom: 40 }}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={onRefresh}
            tintColor={colors.accent}
            colors={[colors.accent]}
          />
        }
      >
        {/* Header Section */}
        <View className="px-4 pt-4">
          <Card className="mx-0 p-4" style={{ backgroundColor: colors.card, borderColor: colors.border, borderWidth: 1 }}>
            <View className="flex-row items-center mb-4">
              <View className="w-10 h-10 rounded-full items-center justify-center mr-3" style={{ backgroundColor: colors.bg }}>
                <Users size={20} color={colors.textPrimary} />
              </View>
              <View>
                <Text className="text-xl font-bold" style={{ color: colors.textPrimary }}>
                  {membersData?.householdName ??
                    activeHouseholdName ??
                    "My Family"}
                </Text>
                <Text className="text-sm" style={{ color: colors.textSecondary }}>
                  Share inventory, share food
                </Text>
              </View>
            </View>

            {/* Stats Row - Using StatCard component */}
            <View className="flex-row">
              <View
                className="flex-1 mr-2 rounded-xl"
                style={{ backgroundColor: `${colors.success}20`, borderWidth: 1, borderColor: `${colors.success}40` }}
              >
                <StatCard
                  value={activeMemberCount}
                  label="Active Members"
                  valueStyle={{ color: colors.success }}
                />
              </View>
              <View
                className="flex-1 ml-2 rounded-xl"
                style={{ backgroundColor: `${colors.accent}20`, borderWidth: 1, borderColor: `${colors.accent}40` }}
              >
                <StatCard
                  value={pendingCount}
                  label="Pending"
                  valueStyle={{ color: colors.accent }}
                />
              </View>
            </View>
          </Card>
        </View>

        {/* Family Features Card - Using Card component */}
        <View className="mx-4 mt-4">
          <Card className="mx-0 overflow-hidden flex-row p-0" style={{ backgroundColor: colors.card, borderColor: colors.border, borderWidth: 1 }}>
            <View className="w-1" style={{ backgroundColor: colors.accent }} />
            <CardContent className="flex-1 p-4">
              <View className="flex-row items-center mb-2">
                <View className="w-6 h-6 rounded-full items-center justify-center mr-2" style={{ backgroundColor: `${colors.accent}30` }}>
                  <Shield size={14} color={colors.accent} />
                </View>
                <Text className="font-semibold text-base" style={{ color: colors.accent }}>
                  Family Features
                </Text>
              </View>
              <Text className="text-sm leading-5" style={{ color: colors.textSecondary }}>
                • Share ingredient inventory with family members
              </Text>
              <Text className="text-sm leading-5" style={{ color: colors.textSecondary }}>
                • Real-time sync of inventory updates and expiration reminders
              </Text>
            </CardContent>
          </Card>
        </View>

        {/* Family Members Section */}
        <View className="mx-4 mt-6">
          <Text className="font-semibold text-base mb-3" style={{ color: colors.textSecondary }}>
            Family Members ({members.length})
          </Text>

          {/* Loading state */}
          {(meLoading || membersLoading) && (
            <View className="py-8 items-center">
              <ActivityIndicator size="large" color={colors.accent} />
              <Text className="mt-2" style={{ color: colors.textMuted }}>Loading members...</Text>
            </View>
          )}

          {/* Error state */}
          {membersError && !membersLoading && (
            <Card className="mx-0 py-8" style={{ backgroundColor: colors.card, borderColor: colors.border, borderWidth: 1 }}>
              <View className="items-center">
                <Text className="text-base mb-2" style={{ color: colors.error }}>
                  Failed to load members
                </Text>
                <Text className="text-sm text-center px-4" style={{ color: colors.textMuted }}>
                  {(membersError as any)?.status === 404
                    ? "Household not found. Refreshing..."
                    : "Pull down to refresh or try again later."}
                </Text>
              </View>
            </Card>
          )}

          {/* Empty state */}
          {!meLoading && !membersLoading && members.length === 0 && (
            <Card className="mx-0 py-8" style={{ backgroundColor: colors.card, borderColor: colors.border, borderWidth: 1 }}>
              <View className="items-center">
                <Users size={48} color={colors.textMuted} />
                <Text className="mt-2" style={{ color: colors.textSecondary }}>No members yet</Text>
                <Text className="text-sm" style={{ color: colors.textMuted }}>
                  Invite family members to share your inventory
                </Text>
              </View>
            </Card>
          )}

          {/* Member Cards - Using Card and Avatar components */}
          {members.map((member, index) => (
            <Card
              key={member.id}
              className="mx-0 flex-row items-center"
              style={{ backgroundColor: colors.card, borderColor: colors.border, borderWidth: 1 }}
            >
              {/* Avatar */}
              <Avatar className="w-12 h-12 mr-3">
                {member.avatarUrl ? (
                  <AvatarImage source={{ uri: member.avatarUrl }} />
                ) : null}
                <AvatarFallback
                  className="items-center justify-center"
                  style={{ backgroundColor: getAvatarColor(index) }}
                >
                  <Text className="text-lg font-semibold" style={{ color: colors.overlayText }}>
                    {member.displayName && member.displayName.length > 0
                      ? member.displayName[0].toUpperCase()
                      : "?"}
                  </Text>
                </AvatarFallback>
              </Avatar>

              {/* Member Info */}
              <View className="flex-1">
                <View className="flex-row items-center mb-1 flex-wrap gap-1">
                  <Text className="font-semibold text-base mr-1" style={{ color: colors.textPrimary }}>
                    {member.displayName}
                  </Text>
                  {getStatusBadge(member.status)}
                </View>
                <View className="flex-row items-center mb-1">
                  <Mail size={12} color={colors.textMuted} />
                  <Text className="text-sm ml-1" style={{ color: colors.textSecondary }}>
                    {member.email}
                  </Text>
                </View>
                <Text className="text-xs" style={{ color: colors.textMuted }}>
                  {member.status === "pending" ? "Invited: " : "Joined: "}
                  {formatJoinDate(member.joinedAt)}
                </Text>
              </View>

              {/* Delete Button (only for owner, not for themselves) */}
              {member.status !== "owner" && currentUserIsOwner && (
                <TouchableOpacity
                  className="p-2"
                  onPress={() => {
                    setMemberToDelete(member);
                    setDeleteDialogOpen(true);
                  }}
                >
                  <Trash2 size={20} color="#EF4444" />
                </TouchableOpacity>
              )}
            </Card>
          ))}
        </View>
      </ScrollView>

      {/* Delete Confirmation Dialog */}
      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader className="items-start">
            <DialogTitle className="text-left">
              {memberToDelete?.status === "pending"
                ? "Cancel Invitation"
                : "Remove Member"}
            </DialogTitle>
            <DialogDescription className="text-left">
              {memberToDelete?.status === "pending"
                ? `Are you sure you want to cancel the invitation to ${memberToDelete?.email}?`
                : `Are you sure you want to remove ${memberToDelete?.displayName} from this household? They will lose access to the shared inventory.`}
            </DialogDescription>
          </DialogHeader>

          <DialogFooter>
            <DialogClose className="flex-1 py-3 rounded-xl border items-center" style={{ borderColor: colors.border }}>
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
            </DialogClose>
            <Pressable
              className="flex-1 py-3 rounded-xl items-center"
              style={{ backgroundColor: colors.error }}
              onPress={handleDeleteMember}
              disabled={removeMemberMutation.isPending}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>
                {removeMemberMutation.isPending
                  ? "Removing..."
                  : memberToDelete?.status === "pending"
                    ? "Cancel Invite"
                    : "Remove"}
              </Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Leave Household Confirmation Dialog */}
      <Dialog open={leaveDialogOpen} onOpenChange={setLeaveDialogOpen}>
        <DialogContent>
          <DialogHeader className="items-start">
            <DialogTitle className="text-left">Leave Family</DialogTitle>
            <DialogDescription className="text-left">
              Are you sure you want to leave this family? Once you leave, you
              will no longer have access to the shared inventory.
            </DialogDescription>
          </DialogHeader>

          <DialogFooter>
            <DialogClose className="flex-1 py-3 rounded-xl border items-center" style={{ borderColor: colors.border }}>
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Cancel</Text>
            </DialogClose>
            <Pressable
              className="flex-1 py-3 rounded-xl items-center"
              style={{ backgroundColor: colors.error }}
              onPress={handleLeaveHousehold}
              disabled={leaveHouseholdMutation.isPending}
            >
              <Text className="font-medium" style={{ color: colors.overlayText }}>
                {leaveHouseholdMutation.isPending ? "Leaving..." : "Continue"}
              </Text>
            </Pressable>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* QR Code Dialog */}
      <Dialog 
        open={qrDialogOpen} 
        onOpenChange={(open) => {
          setQrDialogOpen(open);
          if (!open) {
            // Clear link invitation when dialog closes so a fresh one is created next time
            setLinkInvitation(null);
          }
        }}
      >
        <DialogContent>
          {/* Polling component inside QR dialog to detect acceptance */}
          {qrDialogOpen && activeHouseholdId && linkInvitation && (
            <QRInvitationPoller
              key={linkInvitation.id} // Force remount when invitation changes
              householdId={activeHouseholdId}
              linkInvitation={linkInvitation}
              initialMemberCountRef={initialMemberCountRef}
              currentMemberCount={members.length}
              onMemberJoined={() => {
                // Refresh member list in background while dialog is still open
                refetchMembers();
              }}
              onAccepted={() => {
                // Close dialog after acceptance
                setQrDialogOpen(false);
                setLinkInvitation(null);
                refetchMembers();
                toast.success("New Member Joined!", "Someone has joined your household.");
              }}
            />
          )}
          
          <DialogHeader className="items-center">
            <DialogTitle className="text-center">
              Invite via QR Code
            </DialogTitle>
            <DialogDescription className="text-center">
              Have a family member scan this QR code to join your household.
              This invitation can only be used once.
            </DialogDescription>
          </DialogHeader>

          <View className="items-center py-4">
            {createLinkInvitationMutation.isPending || !linkInvitation ? (
              <View className="w-48 h-48 items-center justify-center">
                <ActivityIndicator size="large" color={colors.accent} />
                <Text className="mt-2" style={{ color: colors.textMuted }}>
                  Generating QR Code...
                </Text>
              </View>
            ) : (
              <>
                <View className="p-4 rounded-xl" style={{ backgroundColor: colors.card }}>
                  <QRCode value={getInviteDeepLink()} size={180} />
                </View>
                <Text className="text-sm mt-3 text-center" style={{ color: colors.textMuted }}>
                  Expires: {formatExpirationDate(linkInvitation.expiredAt)}
                </Text>
              </>
            )}
          </View>

          {linkInvitation && (
            <View className="flex-row gap-2 mb-2">
              <Pressable
                className="flex-1 py-3 rounded-xl border items-center flex-row justify-center"
                style={{ borderColor: colors.border }}
                onPress={handleCopyLink}
              >
                <Copy size={16} color={colors.accent} />
                <Text className="font-medium ml-2" style={{ color: colors.textPrimary }}>Copy</Text>
              </Pressable>
              <Pressable
                className="flex-1 py-3 rounded-xl border items-center flex-row justify-center"
                style={{ borderColor: colors.border }}
                onPress={handleShareLink}
              >
                <Share2 size={16} color={colors.accent} />
                <Text className="font-medium ml-2" style={{ color: colors.textPrimary }}>Share</Text>
              </Pressable>
            </View>
          )}

          <DialogFooter>
            <DialogClose className="flex-1 py-3 rounded-xl border items-center" style={{ borderColor: colors.border }}>
              <Text className="font-medium" style={{ color: colors.textPrimary }}>Close</Text>
            </DialogClose>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </View>
  );
}
