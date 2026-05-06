import { useEffect, useMemo, useRef, useState, useCallback } from "react";
import {
  ActivityIndicator,
  BackHandler,
  KeyboardAvoidingView,
  NativeScrollEvent,
  NativeSyntheticEvent,
  Platform,
  ScrollView,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { useLocalSearchParams, useRouter, useFocusEffect } from "expo-router";
import { useKeepAwake } from "expo-keep-awake";
import { Ionicons } from "@expo/vector-icons";
import { useQueryClient } from "@tanstack/react-query";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

import { Button } from "@/components/Button";
import { Progress } from "@/components/progress";
import { toast } from "@/components/sonner";
import { VoiceControlButton } from "@/components/VoiceControlButton";
import { useCookingSession } from "@/hooks/useCookingSession";
import { useRecipeCookComplete } from "@/hooks/useRecipeCooks";
import { useAuthQuery, useAuthMutation } from "@/hooks/useApi";
import { useVoiceControl } from "@/hooks/useVoiceControl";
import { ApiResponse } from "@/types/api";
import { RecipeDetailDto } from "@/types/recipes";
import ErrorView from "@/components/ui/ErrorView";
import LoadingView from "@/components/ui/LoadingView";

interface DeductForRecipeRequest {
  recipeId: string;
  servings: number;
}

interface DeductionResult {
  totalDeducted: number;
  itemsFullyDeducted: number;
  itemsPartiallyDeducted: number;
  itemsNotFound: number;
}

export default function CookingAssistantScreen() {
  const { colors } = useTheme();
  const { bg, card, border, accent, textPrimary, textSecondary, textMuted } = colors;
  // Keep screen awake while cooking
  useKeepAwake();

  const { recipeId, source } = useLocalSearchParams<{
    recipeId?: string;
    source?: string;
  }>();
  const router = useRouter();

  const { data, isLoading, isError, refetch } = useCookingSession(recipeId);
  const queryClient = useQueryClient();

  // Fetch recipe details to get servings
  const { data: recipeData } = useAuthQuery<ApiResponse<RecipeDetailDto>>(
    ["recipe", recipeId ?? ""],
    recipeId ? `/api/recipes/${recipeId}` : "",
    { enabled: Boolean(recipeId) },
  );

  const cookComplete = useRecipeCookComplete();

  // Mutation for deducting inventory after cooking
  const deductInventory = useAuthMutation<
    ApiResponse<DeductionResult>,
    DeductForRecipeRequest
  >("/api/inventory/deduct-for-recipe", "POST", {
    onSuccess: (response) => {
      // Invalidate inventory queries to refresh the data
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      if (response.data && response.data.totalDeducted > 0) {
        toast.success(`Updated ${response.data.totalDeducted} inventory items`);
      }
    },
    onError: (err) => {
      console.error("Failed to deduct inventory:", err);
      // Silent fail - don't block the user experience
    },
  });

  const steps = data?.data?.steps ?? [];
  const title = data?.data?.title ?? "";
  const totalSteps = data?.data?.totalSteps ?? steps.length;

  const [currentIndex, setCurrentIndex] = useState(0);
  const [initialSeconds, setInitialSeconds] = useState(0);
  const [remainingSeconds, setRemainingSeconds] = useState(0);
  const [isRunning, setIsRunning] = useState(false);
  const [customMinutes, setCustomMinutes] = useState("");
  const [selectedPresetSeconds, setSelectedPresetSeconds] = useState<
    number | null
  >(null);
  const timerIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const scrollViewRef = useRef<ScrollView>(null);
  const [isTimerCollapsed, setIsTimerCollapsed] = useState(false);
  const [completedSteps, setCompletedSteps] = useState<Set<number>>(new Set());
  const isCompletingRef = useRef(false);

  // Voice control state
  const [isVoiceEnabled, setIsVoiceEnabled] = useState(false);

  // Approximate step item height for auto-scroll
  const stepHeightRef = useRef<number>(80);

  const currentStep = steps[currentIndex];
  const completedCount = completedSteps.size;
  const progressValue =
    totalSteps > 0 ? (completedCount / totalSteps) * 100 : 0;

  const clearTimerInterval = useCallback(() => {
    if (!timerIntervalRef.current) return;
    clearInterval(timerIntervalRef.current);
    timerIntervalRef.current = null;
  }, []);

  useEffect(() => clearTimerInterval, [clearTimerInterval]);

  // Auto-scroll to current step when it changes
  useEffect(() => {
    if (steps.length === 0) return;

    // Calculate scroll position to center the current step
    // Timer section is about 200px, each step is about 80px + 12px gap
    const timerSectionHeight = 200;
    const stepItemHeight = stepHeightRef.current + 12; // step height + gap
    const allStepsHeaderHeight = 50; // "All Steps" title + padding

    // Position of current step in the scroll content
    const currentStepY =
      timerSectionHeight + allStepsHeaderHeight + currentIndex * stepItemHeight;

    // Scroll to position that centers the step (offset by ~150px to center in viewable area)
    const scrollTarget = Math.max(0, currentStepY - 100);

    const frameId = requestAnimationFrame(() => {
      scrollViewRef.current?.scrollTo({ y: scrollTarget, animated: true });
    });

    return () => cancelAnimationFrame(frameId);
  }, [currentIndex, steps.length]);

  const startInterval = useCallback(() => {
    clearTimerInterval();
    setIsRunning(true);

    timerIntervalRef.current = setInterval(() => {
      setRemainingSeconds((prev) => {
        const next = Math.max(0, prev - 1);
        if (next === 0) {
          clearTimerInterval();
          setIsRunning(false);
        }
        return next;
      });
    }, 1000);
  }, [clearTimerInterval]);

  const parseMinutes = (value: string) => {
    const normalized = value.trim();
    if (!/^\d+$/.test(normalized)) return null;
    const minutes = Number.parseInt(normalized, 10);
    if (!Number.isFinite(minutes) || minutes <= 0) return null;
    return minutes;
  };

  const setAndStart = useCallback(
    (seconds: number) => {
      const safeSeconds = Math.max(0, Math.floor(seconds));
      if (safeSeconds <= 0) {
        toast.error("Enter minutes");
        return;
      }
      setInitialSeconds(safeSeconds);
      setRemainingSeconds(safeSeconds);
      startInterval();
    },
    [startInterval],
  );

  const pause = useCallback(() => {
    clearTimerInterval();
    setIsRunning(false);
  }, [clearTimerInterval]);

  const reset = useCallback(() => {
    pause();
    setRemainingSeconds(initialSeconds);
  }, [initialSeconds, pause]);

  const restart = useCallback(() => {
    if (initialSeconds <= 0) {
      toast.error("Set a timer first");
      return;
    }
    setRemainingSeconds(initialSeconds);
    startInterval();
  }, [initialSeconds, startInterval]);

  const formatTime = (total: number) => {
    const mins = Math.floor(total / 60)
      .toString()
      .padStart(2, "0");
    const secs = Math.floor(total % 60)
      .toString()
      .padStart(2, "0");
    return `${mins}:${secs}`;
  };

  const goNext = useCallback(() => {
    pause();
    setInitialSeconds(0);
    setRemainingSeconds(0);
    setCustomMinutes("");
    setSelectedPresetSeconds(null);
    setCompletedSteps((prev) => new Set(prev).add(currentIndex));
    setCurrentIndex((idx) => (idx + 1 < steps.length ? idx + 1 : idx));
  }, [pause, currentIndex, steps.length]);
  // Note: pause is included for exhaustive-deps compliance

  // Voice control handler - shows toast on last step instead of completing
  const handleVoiceNextStep = useCallback(() => {
    if (currentIndex >= steps.length - 1) {
      toast.info("You're on the last step. Tap Complete when ready.");
      return;
    }
    goNext();
  }, [currentIndex, steps.length, goNext]);

  // Voice control handler for previous step
  const handleVoicePreviousStep = useCallback(() => {
    if (currentIndex <= 0) {
      toast.info("You're on the first step.");
      return;
    }
    // Go to previous step - inline logic to avoid dependency on handleStepPress
    const prevIndex = currentIndex - 1;
    pause();
    setInitialSeconds(0);
    setRemainingSeconds(0);
    setCustomMinutes("");
    setSelectedPresetSeconds(null);
    setCompletedSteps(() => {
      const nextSet = new Set<number>();
      for (let i = 0; i < prevIndex; i += 1) {
        nextSet.add(i);
      }
      return nextSet;
    });
    setCurrentIndex(prevIndex);
  }, [currentIndex, pause]);

  // Voice control hook
  const {
    isListening,
    isSupported: isVoiceSupported,
    error: voiceError,
  } = useVoiceControl({
    onNextStep: handleVoiceNextStep,
    onPreviousStep: handleVoicePreviousStep,
    enabled: isVoiceEnabled && steps.length > 0,
  });

  const toggleVoiceControl = useCallback(() => {
    setIsVoiceEnabled((prev) => !prev);
  }, []);

  const presetButtons = [60, 180, 300, 600];

  // Timer progress for mini bar (0-100)
  const timerProgress = useMemo(() => {
    if (initialSeconds <= 0) return 0;
    return ((initialSeconds - remainingSeconds) / initialSeconds) * 100;
  }, [initialSeconds, remainingSeconds]);

  // Animation starts when Timer section starts scrolling off (around 50px)
  // Animation completes when Timer section is mostly off-screen (around 200px)
  const ANIMATION_START = 50;
  const ANIMATION_END = 200;

  // Track scroll position for mini timer visibility
  const [scrollPosition, setScrollPosition] = useState(0);

  // Scroll handler to update scroll position
  const handleScroll = useCallback(
    (event: NativeSyntheticEvent<NativeScrollEvent>) => {
      const offsetY = event.nativeEvent.contentOffset.y;
      setScrollPosition(offsetY);

      const shouldCollapse =
        offsetY > ANIMATION_START && (isRunning || remainingSeconds > 0);
      if (shouldCollapse !== isTimerCollapsed) {
        setIsTimerCollapsed(shouldCollapse);
      }
    },
    [isTimerCollapsed, isRunning, remainingSeconds],
  );

  // Calculate mini timer opacity and height based on scroll position
  const miniTimerProgress = useMemo(() => {
    if (scrollPosition <= ANIMATION_START) return 0;
    if (scrollPosition >= ANIMATION_END) return 1;
    return (
      (scrollPosition - ANIMATION_START) / (ANIMATION_END - ANIMATION_START)
    );
  }, [scrollPosition]);

  const scrollToTop = useCallback(() => {
    scrollViewRef.current?.scrollTo({ y: 0, animated: true });
  }, []);

  const handleBack = useCallback(() => {
    if (recipeId) {
      router.push({
        pathname: "/recipe/[recipeId]",
        params: { recipeId, source },
      });
      return;
    }
  }, [recipeId, router, source]);

  const handleStepPress = useCallback(
    (idx: number) => {
      pause();
      setInitialSeconds(0);
      setRemainingSeconds(0);
      setCustomMinutes("");
      setSelectedPresetSeconds(null);
      setCompletedSteps(() => {
        const nextSet = new Set<number>();
        for (let i = 0; i < idx; i += 1) {
          nextSet.add(i);
        }
        return nextSet;
      });
      setCurrentIndex(idx);
    },
    [pause],
  );

  useFocusEffect(
    useCallback(() => {
      const onBackPress = () => {
        handleBack();
        return true;
      };
      const sub = BackHandler.addEventListener(
        "hardwareBackPress",
        onBackPress,
      );
      return () => sub.remove();
    }, [handleBack]),
  );

  // Reset timer and steps every time the Cook screen is entered.
  useFocusEffect(
    useCallback(() => {
      pause();
      setInitialSeconds(0);
      setRemainingSeconds(0);
      setCustomMinutes("");
      setSelectedPresetSeconds(null);
      setCompletedSteps(new Set());
      setCurrentIndex(0);
      isCompletingRef.current = false;

      // Disable voice control when leaving screen
      return () => {
        setIsVoiceEnabled(false);
      };
    }, [pause]),
  );

  useEffect(() => {
    setCompletedSteps(new Set());
    setCurrentIndex(0);
    isCompletingRef.current = false;
  }, [recipeId]);

  if (!recipeId) {
    return (
      <SafeAreaView
        className="flex-1 items-center justify-center"
        style={{ backgroundColor: bg }}
      >
        <Text className="text-lg" style={{ color: textPrimary }}>Missing recipe id</Text>
      </SafeAreaView>
    );
  }

  const renderEmpty = (
    <View className="mt-10 items-center gap-2">
      <Text className="text-lg" style={{ color: textPrimary }}>No steps available.</Text>
      <Text className="text-sm" style={{ color: textSecondary }}>
        This recipe does not contain any instructions.
      </Text>
    </View>
  );

  return (
    <SafeAreaView
      className="flex-1"
      style={{ backgroundColor: bg }}
      edges={["top"]}
    >
      <KeyboardAvoidingView
        className="flex-1"
        behavior={Platform.OS === "ios" ? "padding" : undefined}
      >
        {/* Fixed Header Section */}
        <View style={{ paddingHorizontal: 16, paddingTop: 16 }}>
          <View className="mb-4 flex-row items-center justify-between gap-3">
            <View className="flex-1 flex-row items-center gap-2">
              <Button
                variant="ghost"
                size="icon"
                onPress={handleBack}
                className="h-10 w-10 rounded-full shrink-0"
                style={{ backgroundColor: colors.card }}
              >
                <Ionicons name="chevron-back" size={22} color={textPrimary} />
              </Button>
              <View className="flex-1">
                <Text className="text-lg font-semibold" style={{ color: textPrimary }}>
                  Cooking Assistant
                </Text>
                <Text numberOfLines={1} style={{ color: textSecondary, textTransform: 'capitalize' }}>{title}</Text>
              </View>
            </View>

            {/* Voice Control Toggle - only shown when native speech recognition module is available */}
            {!isLoading && !isError && steps.length > 0 && isVoiceSupported && (
              <View className="shrink-0">
                <VoiceControlButton
                  isEnabled={isVoiceEnabled}
                  isListening={isListening}
                  hasError={!!voiceError}
                  onToggle={toggleVoiceControl}
                />
              </View>
            )}
          </View>

          {isLoading && <LoadingView />}

          {isError && (
            <ErrorView errorPage="cooking assistant" refetch={refetch} />
          )}

          {!isLoading && !isError && steps.length === 0 && renderEmpty}

          {/* Progress bar - fixed at top */}
          {!isLoading && !isError && steps.length > 0 && (
            <View className="gap-2 mb-4">
              <View className="flex-row items-center justify-between">
                <Text style={{ color: textSecondary }}>
                  Progress: {completedCount} / {totalSteps}
                </Text>
                <Text className="text-sm" style={{ color: textMuted }}>
                  Step {currentIndex + 1} of {steps.length || 1}
                </Text>
              </View>
              <Progress
                value={progressValue}
                className="h-3"
                indicatorStyle={{ backgroundColor: accent }}
              />
            </View>
          )}

          {/* Current step card - fixed at top */}
          {!isLoading && !isError && steps.length > 0 && (
            <View
              className="rounded-3xl border mb-4"
              style={{ borderColor: border, backgroundColor: card }}
            >
              <View className="flex-row items-center justify-between px-4 py-3">
                <View className="flex-row items-center gap-3">
                  <View className="h-10 w-10 items-center justify-center rounded-full" style={{ backgroundColor: colors.card }}>
                    <Text className="font-semibold" style={{ color: textPrimary }}>
                      {currentIndex + 1}
                    </Text>
                  </View>
                  <Text style={{ color: textSecondary }}>of {steps.length || 1}</Text>
                </View>
                <Button
                  onPress={
                    currentIndex >= steps.length - 1
                      ? () => {
                          // Debounce: prevent multiple clicks
                          if (isCompletingRef.current) return;
                          isCompletingRef.current = true;

                          // Mark all steps as completed
                          setCompletedSteps(
                            new Set(steps.map((_, idx) => idx)),
                          );
                          // Record cooking completion to history
                          if (recipeId) {
                            cookComplete.mutate(recipeId, {
                              onSuccess: () => {
                                toast.success(
                                  "Recipe added to cooking history!",
                                );

                                // Deduct inventory items used in recipe
                                const servings =
                                  recipeData?.data?.servings ?? 2;
                                deductInventory.mutate({
                                  recipeId,
                                  servings: Math.max(1, Math.round(servings)),
                                });

                                setTimeout(() => handleBack(), 300);
                              },
                              onError: (err) => {
                                console.error(
                                  "Failed to record cook completion:",
                                  err,
                                );
                                isCompletingRef.current = false;
                                // Still navigate back even on error
                                setTimeout(() => handleBack(), 300);
                              },
                            });
                          } else {
                            // No recipeId, just go back
                            setTimeout(() => handleBack(), 300);
                          }
                        }
                      : goNext
                  }
                  disabled={
                    currentIndex >= steps.length - 1 &&
                    (cookComplete.isPending ||
                      deductInventory.isPending ||
                      isCompletingRef.current)
                  }
                  className={cn(
                    "rounded-full px-4",
                    currentIndex >= steps.length - 1 && "bg-green-500",
                  )}
                >
                  {currentIndex >= steps.length - 1
                    ? cookComplete.isPending
                      ? "Completing..."
                      : "Complete"
                    : "Next Step"}
                </Button>
              </View>

              <View className="px-4 pb-5">
                <Text className="text-lg leading-7" style={{ color: textPrimary }}>
                  {currentStep?.instruction ?? "No instruction"}
                </Text>
              </View>
            </View>
          )}

          {/* Mini Timer Bar - compact bar that appears when timer is active and scrolled */}
          {!isLoading &&
            !isError &&
            steps.length > 0 &&
            (isRunning || remainingSeconds > 0) && (
              <View
                style={{
                  opacity: miniTimerProgress,
                  height: miniTimerProgress * 44,
                  overflow: "hidden",
                }}
                pointerEvents={isTimerCollapsed ? "auto" : "none"}
              >
                <TouchableOpacity
                  onPress={scrollToTop}
                  activeOpacity={0.9}
                  className="rounded-xl border px-3 py-2"
                  style={{ borderColor: border, backgroundColor: card }}
                >
                  <View className="flex-row items-center gap-2">
                    <TouchableOpacity
                      onPress={(e) => {
                        e.stopPropagation();
                        if (isRunning) pause();
                        else if (remainingSeconds > 0) startInterval();
                        else if (initialSeconds > 0) restart();
                      }}
                      className="h-7 w-7 items-center justify-center rounded-full"
                      style={{ backgroundColor: accent }}
                    >
                      <Ionicons
                        name={isRunning ? "pause" : "play"}
                        size={12}
                        color={colors.bg}
                      />
                    </TouchableOpacity>

                    <Ionicons name="timer-outline" size={14} color={accent} />

                    <View className="flex-1">
                      <View className="h-1.5 rounded-full overflow-hidden" style={{ backgroundColor: colors.border }}>
                        <View
                          className="h-full rounded-full"
                          style={{ width: `${100 - timerProgress}%`, backgroundColor: accent }}
                        />
                      </View>
                    </View>

                    <Text className="font-semibold text-xs" style={{ color: textPrimary }}>
                      {formatTime(remainingSeconds)}
                    </Text>
                  </View>
                </TouchableOpacity>
              </View>
            )}
        </View>

        {/* Scrollable Middle Section - Timer and All Steps */}
        {!isLoading && !isError && steps.length > 0 && (
          <ScrollView
            ref={scrollViewRef}
            className="flex-1"
            contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 16 }}
            showsVerticalScrollIndicator={false}
            onScroll={handleScroll}
            scrollEventThrottle={16}
          >
            {/* Timer section - compact */}
            <View
              className="rounded-2xl border p-3 gap-3 mb-4"
              style={{ borderColor: border, backgroundColor: card }}
            >
              <View className="flex-row items-center justify-between">
                <View className="flex-row items-center gap-2">
                  <Ionicons name="timer-outline" size={16} color={accent} />
                  <Text className="font-semibold text-sm" style={{ color: textPrimary }}>
                    Timer
                  </Text>
                </View>
                {currentStep?.suggestedSeconds ? (
                  <Text className="text-xs" style={{ color: textMuted }}>
                    Suggested: {Math.round(currentStep.suggestedSeconds / 60)}m
                  </Text>
                ) : null}
              </View>

              <View className="flex-row items-center justify-between">
                <Text className="text-[42px] font-semibold tracking-wider" style={{ color: accent }}>
                  {formatTime(remainingSeconds)}
                </Text>

                <View className="flex-row items-center gap-2">
                  <Button
                    onPress={() => {
                      if (isRunning) return pause();
                      if (remainingSeconds > 0) return startInterval();
                      if (initialSeconds > 0) return restart();
                      toast.error("Enter minutes");
                    }}
                    className="h-9 rounded-xl px-4 border"
                    style={{ backgroundColor: accent, borderColor: colors.border }}
                    textClassName="font-semibold text-sm"
                    textStyle={{ color: colors.bg }}
                  >
                    <View className="flex-row items-center gap-1.5">
                      <Ionicons
                        name={isRunning ? "pause" : "play"}
                        size={14}
                        color={colors.bg}
                      />
                      <Text className="font-semibold text-sm" style={{ color: colors.bg }}>
                        {isRunning ? "Pause" : "Start"}
                      </Text>
                    </View>
                  </Button>

                  <Button
                    onPress={reset}
                    disabled={initialSeconds <= 0 && remainingSeconds <= 0}
                    className="h-9 rounded-xl px-4 border"
                    style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  >
                    <View className="flex-row items-center gap-1.5">
                      <Ionicons name="refresh" size={14} color={textPrimary} />
                      <Text className="font-semibold text-sm" style={{ color: textPrimary }}>
                        Reset
                      </Text>
                    </View>
                  </Button>
                </View>
              </View>

              <View className="flex-row gap-2">
                {presetButtons.map((seconds) => (
                  <Button
                    key={seconds}
                    className={cn(
                      "flex-1 rounded-xl h-9 border",
                      selectedPresetSeconds === seconds && "opacity-80",
                    )}
                    style={{
                      backgroundColor: selectedPresetSeconds === seconds ? colors.card : "transparent",
                      borderColor: colors.border,
                    }}
                    textClassName="font-semibold text-sm"
                    onPress={() => {
                      setSelectedPresetSeconds(seconds);
                      setCustomMinutes("");
                      setAndStart(seconds);
                    }}
                  >
                    <Text className="font-semibold text-sm" style={{ color: textPrimary }}>{`${Math.round(seconds / 60)}m`}</Text>
                  </Button>
                ))}
              </View>

              <View className="flex-row items-center gap-2">
                <TextInput
                  placeholder="Custom (min)"
                  placeholderTextColor={textMuted}
                  keyboardType="numeric"
                  value={customMinutes}
                  onChangeText={(text) => {
                    setCustomMinutes(text);
                    setSelectedPresetSeconds(null);
                  }}
                  className="flex-1 rounded-xl border px-3 py-2 text-sm"
                  style={{ borderColor: border, backgroundColor: colors.card, color: textPrimary }}
                />
                <Button
                  className="rounded-xl px-5 h-9 border"
                  style={{ backgroundColor: colors.card, borderColor: colors.border }}
                  disabled={!parseMinutes(customMinutes)}
                  onPress={() => {
                    const minutes = parseMinutes(customMinutes);
                    if (!minutes) {
                      toast.error("Enter minutes");
                      return;
                    }
                    setSelectedPresetSeconds(null);
                    setAndStart(minutes * 60);
                  }}
                >
                  <Text className="font-semibold text-sm" style={{ color: textPrimary }}>Set</Text>
                </Button>
              </View>
            </View>

            {/* All Steps section */}
            <View
              className="rounded-3xl border p-4 gap-3"
              style={{ borderColor: border, backgroundColor: card }}
            >
              <Text className="font-semibold mb-1" style={{ color: textPrimary }}>All Steps</Text>
              <View className="gap-3">
                {steps.map((step, idx) => {
                  const isCompleted = completedSteps.has(idx);
                  const isCurrent = idx === currentIndex;

                  return (
                    <TouchableOpacity
                      key={`${step.order}-${idx}`}
                      activeOpacity={0.8}
                      onPress={() => handleStepPress(idx)}
                      className="rounded-2xl border px-3 py-3"
                      style={{
                        borderColor: isCurrent
                          ? colors.accent
                          : isCompleted
                            ? "rgba(34, 197, 94, 0.4)"
                            : colors.border,
                        backgroundColor: isCurrent
                          ? colors.card
                          : isCompleted
                            ? "rgba(34, 197, 94, 0.1)"
                            : colors.card,
                      }}
                    >
                      <View className="flex-row items-center gap-3">
                        <View
                          className={cn(
                            "h-8 w-8 items-center justify-center rounded-full border",
                            isCompleted && "bg-green-500/20 border-green-500/40",
                          )}
                          style={!isCompleted ? { backgroundColor: colors.card, borderColor: colors.border } : undefined}
                        >
                          {isCompleted ? (
                            <Ionicons
                              name="checkmark"
                              size={18}
                              color="#22c55e"
                            />
                          ) : (
                            <Text className="font-semibold" style={{ color: textPrimary }}>
                              {idx + 1}
                            </Text>
                          )}
                        </View>
                        <View className="flex-1">
                          <Text style={{ color: isCompleted ? textSecondary : textPrimary }}>
                            {step.instruction}
                          </Text>
                          {step.suggestedSeconds ? (
                            <Text className="text-sm" style={{ color: textMuted }}>
                              {Math.round(step.suggestedSeconds / 60)} min
                            </Text>
                          ) : null}
                        </View>
                        {isCompleted ? (
                          <TouchableOpacity
                            className="h-9 w-9 items-center justify-center"
                            onPress={() => handleStepPress(idx)}
                          >
                            <Ionicons
                              name="checkmark-circle"
                              size={24}
                              color="#22c55e"
                            />
                          </TouchableOpacity>
                        ) : (
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-9 w-9 rounded-full"
                            style={{ backgroundColor: colors.card }}
                            onPress={() => handleStepPress(idx)}
                          >
                            <Ionicons
                              name={
                                isCurrent
                                  ? "radio-button-on"
                                  : "radio-button-off"
                              }
                              size={22}
                              color={textPrimary}
                            />
                          </Button>
                        )}
                      </View>
                    </TouchableOpacity>
                  );
                })}
                {!steps.length && (
                  <Text style={{ color: textSecondary }}>
                    No steps available for this recipe.
                  </Text>
                )}
              </View>
            </View>
          </ScrollView>
        )}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
