import { useOAuth, useSignIn } from "@clerk/clerk-expo";
import { Link, useRouter } from "expo-router";
import {
  KeyboardAvoidingView,
  Platform,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { useCallback, useEffect, useState } from "react";
import { Ionicons } from "@expo/vector-icons";
import * as Linking from "expo-linking";
import * as WebBrowser from "expo-web-browser";
import { Image } from "@/lib/nativewind";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";
import type { SignInResource, SignInSecondFactor } from "@clerk/types";

const logoImage = require("../../assets/images/logo.jpg");

WebBrowser.maybeCompleteAuthSession();

export default function SignInScreen() {
  const { signIn, setActive, isLoaded } = useSignIn();
  const { startOAuthFlow: startGoogleOAuth } = useOAuth({
    strategy: "oauth_google",
  });
  const router = useRouter();
  const { colors } = useTheme();

  const [emailAddress, setEmailAddress] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [focusedField, setFocusedField] = useState<
    "email" | "password" | "second" | null
  >(null);
  const [pendingSignIn, setPendingSignIn] = useState<SignInResource | null>(
    null,
  );
  const [selectedSecondFactor, setSelectedSecondFactor] = useState<
    SignInSecondFactor["strategy"] | null
  >(null);
  const [secondFactorCode, setSecondFactorCode] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [isSendingSecondFactor, setIsSendingSecondFactor] = useState(false);
  const [secondFactorHint, setSecondFactorHint] = useState("");
  const [isOAuthLoading, setIsOAuthLoading] = useState(false);

  const supportedSecondFactorStrategies = [
    "email_code",
    "phone_code",
    "totp",
    "backup_code",
  ] as const;
  type SupportedSecondFactorStrategy =
    (typeof supportedSecondFactorStrategies)[number];

  const availableSecondFactors = (pendingSignIn?.supportedSecondFactors ?? [])
    .filter((factor): factor is SignInSecondFactor => Boolean(factor))
    .filter((factor) =>
      supportedSecondFactorStrategies.includes(
        factor.strategy as SupportedSecondFactorStrategy,
      ),
    );
  const isSecondFactorStep = Boolean(pendingSignIn);

  useEffect(() => {
    if (pendingSignIn) {
      setSelectedSecondFactor(availableSecondFactors[0]?.strategy ?? null);
      setSecondFactorCode("");
      setError("");
    } else {
      setSelectedSecondFactor(null);
      setSecondFactorCode("");
    }
  }, [pendingSignIn]);

  const finishSignIn = async (completed: SignInResource) => {
    if (!completed.createdSessionId) {
      setError("Unable to create a session. Please try again.");
      return;
    }
    if (!setActive) {
      setError("Unable to complete sign-in. Please try again.");
      return;
    }
    await setActive({ session: completed.createdSessionId });
    router.replace("/");
  };

  const handleFirstFactor = async () => {
    if (!signIn) {
      setError("Sign-in is not ready yet. Please try again.");
      return;
    }
    const signInAttempt = await signIn.create({
      identifier: emailAddress.trim(),
      password,
    });

    if (signInAttempt.status === "complete") {
      await finishSignIn(signInAttempt);
      return;
    }

    if (signInAttempt.status === "needs_second_factor") {
      setPendingSignIn(signInAttempt);
      return;
    }

    setError("Unexpected sign-in status. Please try again.");
  };

  const handleSecondFactor = async () => {
    if (!pendingSignIn || !selectedSecondFactor) {
      setError("Select a verification method to continue.");
      return;
    }

    if (!secondFactorCode.trim()) {
      setError("Enter the verification code.");
      return;
    }

    const attempt = await pendingSignIn.attemptSecondFactor({
      strategy: selectedSecondFactor as SupportedSecondFactorStrategy,
      code: secondFactorCode.trim(),
    });

    if (attempt.status === "complete") {
      setPendingSignIn(null);
      await finishSignIn(attempt);
      return;
    }

      setError("Verification failed. Double-check the code and try again.");
  };

  const onSubmit = async () => {
    if (!isLoaded || isProcessing) return;
    setError("");
    setIsProcessing(true);

    try {
      if (isSecondFactorStep) {
        await handleSecondFactor();
      } else {
        await handleFirstFactor();
      }
    } catch (err: any) {
      if (err.errors?.[0]?.code === "form_password_incorrect") {
        setError("Password is incorrect. Please try again.");
      } else if (err.errors?.[0]?.message) {
        setError(err.errors[0].message);
      } else {
        setError("Something went wrong. Please try again.");
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const resetSecondFactorState = () => {
    setPendingSignIn(null);
    setError("");
  };

  const getSecondFactorLabel = (
    strategy: SignInSecondFactor["strategy"] | null,
  ) => {
    switch (strategy) {
      case "totp":
        return "Authenticator code";
      case "backup_code":
        return "Backup code";
      default:
        return "Verification code";
    }
  };

  const getSecondFactorPlaceholder = (
    strategy: SignInSecondFactor["strategy"] | null,
  ) => {
    switch (strategy) {
      case "totp":
        return "6-digit code";
      case "backup_code":
        return "e.g. word-word-word";
      case "email_code":
        return "Code from your email";
      case "phone_code":
        return "Code from SMS";
      default:
        return "Enter code";
    }
  };

  const getSecondFactorTabTitle = (factor: SignInSecondFactor) => {
    switch (factor.strategy) {
      case "totp":
        return "Authenticator";
      case "backup_code":
        return "Backup code";
      case "email_code":
        return "Email code";
      case "phone_code":
        return "SMS code";
      default:
        return "Verification";
    }
  };

  const requiresCodeDelivery = (
    strategy: SignInSecondFactor["strategy"] | null,
  ) => strategy === "email_code" || strategy === "phone_code";

  const sendSecondFactorCode = useCallback(
    async (showError = true) => {
      if (
        !pendingSignIn ||
        !selectedSecondFactor ||
        !requiresCodeDelivery(selectedSecondFactor)
      ) {
        return;
      }

      setIsSendingSecondFactor(true);
      try {
        await pendingSignIn.prepareSecondFactor({
          strategy: selectedSecondFactor,
        });
        if (selectedSecondFactor === "email_code") {
          const target =
            pendingSignIn.identifier ??
            emailAddress.trim() ??
            "your email";
          setSecondFactorHint(`We sent a code to ${target}.`);
        } else if (selectedSecondFactor === "phone_code") {
          const target = pendingSignIn.identifier ?? "your phone";
          setSecondFactorHint(`We texted a code to ${target}.`);
        }
      } catch (prepErr: any) {
        if (showError) {
          setError(
            prepErr.errors?.[0]?.message ??
              "Unable to send the verification code. Please try again.",
          );
        }
      } finally {
        setIsSendingSecondFactor(false);
      }
    },
    [pendingSignIn, selectedSecondFactor],
  );

  const handleGoogleSignIn = useCallback(async () => {
    if (!isLoaded || isOAuthLoading) return;
    setError("");
    setIsOAuthLoading(true);

    try {
      const redirectUrl = Linking.createURL("/");
      const { createdSessionId, signIn: oauthSignIn, signUp, setActive: setActiveFromOAuth } =
        await startGoogleOAuth({
          redirectUrl,
        });

      const sessionId =
        createdSessionId ??
        oauthSignIn?.createdSessionId ??
        signUp?.createdSessionId;

      if (sessionId && setActiveFromOAuth) {
        await setActiveFromOAuth({ session: sessionId });
        router.replace("/");
        return;
      }

      setError("Google sign-in did not complete. Please try again.");
    } catch (err: any) {
      setError(
        err?.errors?.[0]?.message ??
          "Google sign-in failed. Please try again.",
      );
    } finally {
      setIsOAuthLoading(false);
    }
  }, [isLoaded, isOAuthLoading, router, startGoogleOAuth]);

  useEffect(() => {
    if (!pendingSignIn || !selectedSecondFactor) {
      setSecondFactorHint("");
      return;
    }

    if (requiresCodeDelivery(selectedSecondFactor)) {
      void sendSecondFactorCode(false);
      return;
    }

    if (selectedSecondFactor === "totp") {
      setSecondFactorHint(
        "Open your authenticator app to view the rotating 6-digit code.",
      );
      return;
    }

    if (selectedSecondFactor === "backup_code") {
      setSecondFactorHint(
        "Enter one of the backup codes you saved when enabling 2FA.",
      );
      return;
    }

    setSecondFactorHint("Enter the verification code to continue.");
  }, [pendingSignIn, selectedSecondFactor, sendSecondFactorCode]);

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
              <Text className="mt-1 text-base" style={{ color: colors.textSecondary }}>Welcome Back</Text>
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

            <View className="space-y-5">
              <TouchableOpacity
                onPress={() => {
                  void handleGoogleSignIn();
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
                  className={cn(
                    "flex-row items-center rounded-2xl border px-4",
                    focusedField === "email" && "border-2",
                  )}
                  style={{
                    backgroundColor: colors.card,
                    borderColor: focusedField === "email" ? colors.accent : colors.border,
                  }}
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
                    onFocus={() => setFocusedField("email")}
                    onBlur={() => setFocusedField(null)}
                    className="h-12 flex-1 text-base"
                    style={{ color: colors.textPrimary }}
                    keyboardType="email-address"
                  />
                </View>
              </View>

              <View className="space-y-2">
                <Text className="text-sm" style={{ color: colors.textSecondary }}>Password</Text>
                <View
                  className={cn(
                    "flex-row items-center rounded-2xl border px-4",
                    focusedField === "password" && "border-2",
                  )}
                  style={{
                    backgroundColor: colors.card,
                    borderColor: focusedField === "password" ? colors.accent : colors.border,
                  }}
                >
                  <Ionicons
                    name="lock-closed-outline"
                    size={20}
                    color={colors.textMuted}
                    style={{ marginRight: 8 }}
                  />
                  <TextInput
                    value={password}
                    placeholder="Enter password"
                    placeholderTextColor={colors.textMuted}
                    secureTextEntry
                    onChangeText={setPassword}
                    onFocus={() => setFocusedField("password")}
                    onBlur={() => setFocusedField(null)}
                    className="h-12 flex-1 text-base"
                    style={{ color: colors.textPrimary }}
                  />
              </View>
            </View>

            {isSecondFactorStep ? (
              <View
                className="space-y-3 rounded-2xl border p-4"
                style={{ backgroundColor: colors.card, borderColor: colors.border }}
              >
                <View className="flex-row items-center justify-between">
                  <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                    Two-step verification
                  </Text>
                  <TouchableOpacity onPress={resetSecondFactorState}>
                    <Text className="text-xs" style={{ color: colors.textSecondary }}>
                      Use a different account
                    </Text>
                  </TouchableOpacity>
                </View>

                {availableSecondFactors.length > 1 && (
                  <View
                    className="flex-row rounded-2xl border p-1"
                    style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  >
                    {availableSecondFactors.map((factor, index) => {
                      const key = `${factor.strategy}-${index}`;
                      const isSelected =
                        selectedSecondFactor === factor.strategy;
                      return (
                        <TouchableOpacity
                          key={key}
                          activeOpacity={0.9}
                          className="flex-1 rounded-xl py-2"
                          style={isSelected ? { backgroundColor: colors.accent } : undefined}
                          onPress={() =>
                            setSelectedSecondFactor(factor.strategy)
                          }
                        >
                          <Text
                            className="text-center text-sm font-medium"
                            style={{ color: isSelected ? colors.textPrimary : colors.textSecondary }}
                          >
                            {getSecondFactorTabTitle(factor)}
                          </Text>
                        </TouchableOpacity>
                      );
                    })}
                  </View>
                )}

                <View className="space-y-2">
                  <Text className="text-sm" style={{ color: colors.textSecondary }}>
                    {getSecondFactorLabel(selectedSecondFactor)}
                  </Text>
                  <View
                    className={cn(
                      "flex-row items-center rounded-2xl border px-4",
                      focusedField === "second" && "border-2",
                    )}
                    style={{
                      backgroundColor: colors.card,
                      borderColor: focusedField === "second" ? colors.accent : colors.border,
                    }}
                  >
                    <Ionicons
                      name={
                        selectedSecondFactor === "backup_code"
                          ? "key-outline"
                          : "shield-outline"
                      }
                      size={20}
                      color={colors.textMuted}
                      style={{ marginRight: 8 }}
                    />
                    <TextInput
                      value={secondFactorCode}
                      placeholder={getSecondFactorPlaceholder(
                        selectedSecondFactor,
                      )}
                      placeholderTextColor={colors.textMuted}
                      onChangeText={setSecondFactorCode}
                      className="h-12 flex-1 text-base"
                      style={{ color: colors.textPrimary }}
                      keyboardType={
                        selectedSecondFactor === "totp"
                          ? "number-pad"
                          : "default"
                      }
                      autoCapitalize="none"
                      onFocus={() => setFocusedField("second")}
                      onBlur={() => setFocusedField(null)}
                    />
                  </View>
                  {secondFactorHint ? (
                    <Text className="text-xs" style={{ color: colors.textSecondary }}>
                      {secondFactorHint}
                    </Text>
                  ) : null}
                  {requiresCodeDelivery(selectedSecondFactor) ? (
                    <TouchableOpacity
                      onPress={() => {
                        void sendSecondFactorCode();
                      }}
                      disabled={isSendingSecondFactor}
                      className="self-start rounded-xl px-3 py-1"
                    >
                      <Text className="text-xs font-medium" style={{ color: colors.accent }}>
                        {isSendingSecondFactor ? "Sending..." : "Resend code"}
                      </Text>
                    </TouchableOpacity>
                  ) : null}
                </View>
              </View>
            ) : null}

            <TouchableOpacity
              onPress={onSubmit}
              disabled={!isLoaded || isProcessing}
              activeOpacity={0.9}
              className={cn(
                "mt-2 h-14 items-center justify-center rounded-2xl",
                (!isLoaded || isProcessing) && "opacity-70",
              )}
              style={{ backgroundColor: colors.accent }}
            >
              <Text className="text-base font-semibold" style={{ color: colors.textPrimary }}>
                {isProcessing
                  ? "Please wait..."
                  : isSecondFactorStep
                    ? "Verify code"
                    : "Sign In"}
              </Text>
            </TouchableOpacity>
          </View>

            <View className="mt-8 flex-row items-center justify-center">
              <Text className="text-base" style={{ color: colors.textSecondary }}>
                Don&apos;t have an account?
              </Text>
              <Link href="/sign-up" asChild>
                <TouchableOpacity activeOpacity={0.85}>
                  <Text className="ml-2 text-base font-semibold" style={{ color: colors.accent }}>
                    Sign up
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
