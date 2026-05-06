import { Redirect, Slot, useRouter } from "expo-router";
import { useAuth } from "@clerk/clerk-expo";
import { useEffect, useState } from "react";
import AsyncStorage from "@react-native-async-storage/async-storage";
import {
  PENDING_INVITATION_KEY,
  PENDING_INVITATION_TOKEN_KEY,
} from "@/constants/constants";

export default function AuthRoutesLayout() {
  const { isSignedIn } = useAuth();
  const router = useRouter();
  const [isChecking, setIsChecking] = useState(true);
  const [pendingInvitation, setPendingInvitation] = useState<string | null>(
    null,
  );
  const [pendingToken, setPendingToken] = useState<string | null>(null);

  useEffect(() => {
    const checkPendingInvitation = async () => {
      try {
        const invitationId = await AsyncStorage.getItem(PENDING_INVITATION_KEY);
        const token = await AsyncStorage.getItem(PENDING_INVITATION_TOKEN_KEY);
        setPendingInvitation(invitationId);
        setPendingToken(token);
      } catch (error) {
        console.error("Error checking pending invitation:", error);
      } finally {
        setIsChecking(false);
      }
    };
    checkPendingInvitation();
  }, []);

  useEffect(() => {
    const handleRedirectAfterLogin = async () => {
      if (isSignedIn) {
        if (pendingToken) {
          // Clear the pending token
          await AsyncStorage.removeItem(PENDING_INVITATION_TOKEN_KEY);
          // Navigate to the token invitation page
          router.replace(`/invitations/token/${pendingToken}`);
        } else if (pendingInvitation) {
          // Clear the pending invitation
          await AsyncStorage.removeItem(PENDING_INVITATION_KEY);
          // Navigate to the invitation page
          router.replace(`/invitations/${pendingInvitation}`);
        }
      }
    };

    if (!isChecking) {
      handleRedirectAfterLogin();
    }
  }, [isSignedIn, pendingInvitation, pendingToken, isChecking, router]);

  if (isSignedIn && !pendingInvitation && !pendingToken && !isChecking) {
    return <Redirect href={"/"} />;
  }

  return <Slot />;
}
