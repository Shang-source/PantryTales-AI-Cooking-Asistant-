import { useCallback, useState } from "react";
import {
  KeyboardAvoidingView,
  Platform,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { useOAuth, useSignUp } from "@clerk/clerk-expo";
import { Link, useRouter } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import * as Linking from "expo-linking";
import * as WebBrowser from "expo-web-browser";
import { Image } from "@/lib/nativewind";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

const logoImage = require("../../assets/images/logo.jpg");

WebBrowser.maybeCompleteAuthSession();

export default function SignUpScreen() {
  const { isLoaded, signUp, setActive } = useSignUp();
  const { startOAuthFlow: startGoogleOAuth } = useOAuth({
    strategy: "oauth_google",
  });
  const router = useRouter();
  const { colors } = useTheme();

  const [emailAddress, setEmailAddress] = useState("");
  const [password, setPassword] = useState("");
  const [pendingVerification, setPendingVerification] = useState(false);
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [isOAuthLoading, setIsOAuthLoading] = useState(false);

  const onSignUpPress = async () => {
    if (!isLoaded || isProcessing) return;
    setError("");
    setIsProcessing(true);

    try {
      await signUp.create({
        emailAddress: emailAddress.trim(),
        password,
      });

      await signUp.prepareEmailAddressVerification({
        strategy: "email_code",
      });

      setPendingVerification(true);
    } catch (err: any) {
      if (err.errors?.[0]?.code === "form_identifier_exists") {
        setError("Email already exists. Try signing in instead.");
      } else if (err.errors?.[0]?.message) {
        setError(err.errors[0].message);
      } else {
        setError("Something went wrong. Please try again.");
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const onVerifyPress = async () => {
    if (!isLoaded || isProcessing) return;
    setError("");
    setIsProcessing(true);

    try {
      const signUpAttempt = await signUp.attemptEmailAddressVerification({
        code: code.trim(),
      });

      if (signUpAttempt.status === "complete") {
        await setActive({ session: signUpAttempt.createdSessionId });
        router.replace("/");
      } else {
        setError("Additional steps are required to finish signing up.");
      }
    } catch (err: any) {
      if (err.errors?.[0]?.message) {
        setError(err.errors[0].message);
      } else {
        setError("Verification failed. Check the code and try again.");
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const handleGoogleSignUp = useCallback(async () => {
    if (!isLoaded || isOAuthLoading) return;
    setError("");
    setIsOAuthLoading(true);

    try {
      const redirectUrl = Linking.createURL("/");
      const { createdSessionId, signIn, signUp: oauthSignUp, setActive: setActiveFromOAuth } =
        await startGoogleOAuth({
          redirectUrl,
        });

      const sessionId =
        createdSessionId ??
        signIn?.createdSessionId ??
        oauthSignUp?.createdSessionId;

      if (sessionId && setActiveFromOAuth) {
        await setActiveFromOAuth({ session: sessionId });
        router.replace("/");
        return;
      }

      setError("Google sign-up did not complete. Please try again.");
    } catch (err: any) {
      setError(
        err?.errors?.[0]?.message ??
          "Google sign-up failed. Please try again.",
      );
    } finally {
      setIsOAuthLoading(false);
    }
  }, [isLoaded, isOAuthLoading, router, startGoogleOAuth]);

  return (
    <View className="flex-1" style={{ backgroundColor: colors.bg }}>
      <KeyboardAvoidingView
        className="flex-1"
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        keyboardVerticalOffset={Platform.OS === "ios" ? 260 : 0}
      >
        <View className="flex-1 items-center justify-center px-6">
          <View
            className="w-full max-w-[380px] rounded-[26px] border p-7 shadow-2xl"
            style={{ backgroundColor: colors.card, borderColor: colors.border }}
          >
            <View className="mb-6 items-center">
              <View
                className="mb-4 h-24 w-24 overflow-hidden rounded-[26px] border"
                style={{ borderColor: colors.border }}
              >
                <Image source={logoImage} className="h-full w-full" />
              </View>
              <Text className="text-3xl font-semibold" style={{ color: colors.textPrimary }}>
                PantryTales
              </Text>
              <Text className="mt-1 text-base" style={{ color: colors.textSecondary }}>
                {pendingVerification ? "Check your inbox" : "Create account"}
              </Text>
            </View>

            {error ? (
              <View className="mb-4 flex-row items-center rounded-2xl border border-red-300/30 bg-red-500/20 px-3 py-2">
                <Ionicons
                  name="alert-circle"
                  size={18}
                  color={colors.error}
                  style={{ marginRight: 8 }}
                />
                <Text className="flex-1 text-sm" style={{ color: colors.error }}>{error}</Text>
              </View>
            ) : null}

            {pendingVerification ? (
              <View className="space-y-4">
                <View className="space-y-2">
                  <Text className="text-sm" style={{ color: colors.textSecondary }}>
                    Verification code
                  </Text>
                  <View
                    className="flex-row items-center rounded-2xl border px-4"
                    style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  >
                    <Ionicons
                      name="mail-open-outline"
                      size={20}
                      color={colors.textMuted}
                      style={{ marginRight: 8 }}
                    />
                    <TextInput
                      value={code}
                      placeholder="Enter the 6-digit code"
                      placeholderTextColor={colors.textMuted}
                      onChangeText={setCode}
                      className="h-12 flex-1 text-base"
                      style={{ color: colors.textPrimary }}
                      keyboardType="number-pad"
                      autoCapitalize="none"
                    />
                  </View>
                  <Text className="text-xs" style={{ color: colors.textSecondary }}>
                    We sent a verification code to {emailAddress || "your email"}
                  </Text>
                </View>

                <TouchableOpacity
                  onPress={onVerifyPress}
                  disabled={!isLoaded || isProcessing}
                  activeOpacity={0.9}
                  className={cn(
                    "mt-2 h-14 items-center justify-center rounded-2xl",
                    (!isLoaded || isProcessing) && "opacity-70",
                  )}
                  style={{ backgroundColor: colors.accent }}
                >
                  <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                    {isProcessing ? "Verifying..." : "Verify email"}
                  </Text>
                </TouchableOpacity>

                <TouchableOpacity
                  onPress={() => {
                    setPendingVerification(false);
                    setCode("");
                    setError("");
                  }}
                  className="items-center"
                >
                  <Text className="text-sm" style={{ color: colors.textSecondary }}>
                    Entered the wrong email? Go back
                  </Text>
                </TouchableOpacity>
              </View>
            ) : (
              <View className="space-y-5">
                <TouchableOpacity
                  onPress={() => {
                    void handleGoogleSignUp();
                  }}
                  disabled={!isLoaded || isOAuthLoading}
                  activeOpacity={0.9}
                  className={cn(
                    "h-12 flex-row items-center justify-center rounded-2xl border",
                    (!isLoaded || isOAuthLoading) && "opacity-70",
                  )}
                  style={{ backgroundColor: colors.accent, borderColor: colors.border }}
                >
                  <Ionicons
                    name="logo-google"
                    size={20}
                    color={colors.textPrimary}
                    style={{ marginRight: 10 }}
                  />
                  <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                    {isOAuthLoading ? "Connecting..." : "Continue with Google"}
                  </Text>
                </TouchableOpacity>

                <View className="flex-row items-center space-x-3 pt-2">
                  <View className="h-px flex-1" style={{ backgroundColor: colors.border }} />
                  <Text className="text-xs uppercase tracking-wide" style={{ color: colors.textSecondary }}>
                    or email
                  </Text>
                  <View className="h-px flex-1" style={{ backgroundColor: colors.border }} />
                </View>

                <View className="space-y-2">
                  <Text className="text-sm" style={{ color: colors.textSecondary }}>Email</Text>
                  <View
                    className="flex-row items-center rounded-2xl border px-4"
                    style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  >
                    <Ionicons
                      name="mail-outline"
                      size={20}
                      color={colors.textMuted}
                      style={{ marginRight: 8 }}
                    />
                    <TextInput
                      autoCapitalize="none"
                      value={emailAddress}
                      placeholder="Enter email"
                      placeholderTextColor={colors.textMuted}
                      onChangeText={setEmailAddress}
                      className="h-12 flex-1 text-base"
                      style={{ color: colors.textPrimary }}
                      keyboardType="email-address"
                    />
                  </View>
                </View>

                <View className="space-y-2">
                  <Text className="text-sm" style={{ color: colors.textSecondary }}>Password</Text>
                  <View
                    className="flex-row items-center rounded-2xl border px-4"
                    style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  >
                    <Ionicons
                      name="lock-closed-outline"
                      size={20}
                      color={colors.textMuted}
                      style={{ marginRight: 8 }}
                    />
                    <TextInput
                      value={password}
                      placeholder="Create password"
                      placeholderTextColor={colors.textMuted}
                      secureTextEntry
                      onChangeText={setPassword}
                      className="h-12 flex-1 text-base"
                      style={{ color: colors.textPrimary }}
                    />
                  </View>
                </View>

                <TouchableOpacity
                  onPress={onSignUpPress}
                  disabled={!isLoaded || isProcessing}
                  activeOpacity={0.9}
                  className={cn(
                    "mt-2 h-14 items-center justify-center rounded-2xl",
                    (!isLoaded || isProcessing) && "opacity-70",
                  )}
                  style={{ backgroundColor: colors.accent }}
                >
                  <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                    {isProcessing ? "Creating..." : "Create account"}
                  </Text>
                </TouchableOpacity>
              </View>
            )}

            <View className="mt-8 flex-row items-center justify-center">
              <Text className="text-base" style={{ color: colors.textSecondary }}>
                Already have an account?
              </Text>
              <Link href="/sign-in" asChild>
                <TouchableOpacity activeOpacity={0.85}>
                  <Text className="ml-2 text-base font-semibold" style={{ color: colors.accent }}>
                    Sign in
                  </Text>
                </TouchableOpacity>
              </Link>
            </View>
          </View>
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}
