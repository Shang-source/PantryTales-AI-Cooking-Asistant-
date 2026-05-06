import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, AppState } from "react-native";

// Conditionally import expo-speech-recognition to avoid crashes in Expo Go
let ExpoSpeechRecognitionModule: any = null;
let useSpeechRecognitionEvent: any = () => {};

try {
  const speechModule = require("expo-speech-recognition");
  ExpoSpeechRecognitionModule = speechModule.ExpoSpeechRecognitionModule;
  useSpeechRecognitionEvent = speechModule.useSpeechRecognitionEvent;
} catch {
  // Module not available (Expo Go) - features will be disabled
}

type VoiceCommand = "next" | "previous" | "unknown";

interface UseVoiceControlOptions {
  onNextStep: () => void;
  onPreviousStep?: () => void;
  enabled: boolean;
}

interface UseVoiceControlReturn {
  isListening: boolean;
  isSupported: boolean;
  permissionGranted: boolean | null;
  lastRecognizedText: string | null;
  error: string | null;
  startListening: () => Promise<void>;
  stopListening: () => void;
  requestPermission: () => Promise<boolean>;
}

// Check if voice control is supported (native module available)
export const isVoiceControlSupported = ExpoSpeechRecognitionModule !== null;

const COMMAND_PATTERNS: Record<VoiceCommand, RegExp[]> = {
  next: [
    /\bnext\s*step\b/i,
    /\bnext\b/i,
    /\bgo\s*next\b/i,
    /\bcontinue\b/i,
    /\bproceed\b/i,
    /\bmove\s*on\b/i,
    /下一步/, // Chinese: next step
  ],
  previous: [
    /\bprevious\s*step\b/i,
    /\bprevious\b/i,
    /\bgo\s*back\b/i,
    /\bback\b/i,
    /上一步/, // Chinese: previous step
  ],
  unknown: [],
};

function detectCommand(text: string): VoiceCommand {
  const trimmed = text.trim();
  if (!trimmed) return "unknown";

  // Check if the text contains any command keywords
  for (const [command, patterns] of Object.entries(COMMAND_PATTERNS)) {
    if (command === "unknown") continue;
    for (const pattern of patterns) {
      if (pattern.test(trimmed)) {
        return command as VoiceCommand;
      }
    }
  }

  return "unknown";
}

export function useVoiceControl({
  onNextStep,
  onPreviousStep,
  enabled,
}: UseVoiceControlOptions): UseVoiceControlReturn {
  const [isListening, setIsListening] = useState(false);
  const [permissionGranted, setPermissionGranted] = useState<boolean | null>(
    null,
  );
  const [lastRecognizedText, setLastRecognizedText] = useState<string | null>(
    null,
  );
  const [error, setError] = useState<string | null>(null);

  // Use refs to avoid stale closures in event handlers
  const lastCommandTimeRef = useRef<number>(0);
  const lastTranscriptRef = useRef<string>("");
  const isStartingRef = useRef(false);
  const enabledRef = useRef(enabled);
  const onNextStepRef = useRef(onNextStep);
  const onPreviousStepRef = useRef(onPreviousStep);
  const permissionGrantedRef = useRef(permissionGranted);

  // Keep refs in sync with props/state
  useEffect(() => {
    enabledRef.current = enabled;
  }, [enabled]);

  useEffect(() => {
    onNextStepRef.current = onNextStep;
  }, [onNextStep]);

  useEffect(() => {
    onPreviousStepRef.current = onPreviousStep;
  }, [onPreviousStep]);

  useEffect(() => {
    permissionGrantedRef.current = permissionGranted;
  }, [permissionGranted]);

  // Debounce commands to prevent rapid-fire triggers
  const COMMAND_DEBOUNCE_MS = 1000;

  const requestPermission = useCallback(async (): Promise<boolean> => {
    if (!ExpoSpeechRecognitionModule) return false;
    try {
      const result =
        await ExpoSpeechRecognitionModule.requestPermissionsAsync();
      const granted = result.granted;
      setPermissionGranted(granted);

      if (!granted) {
        Alert.alert(
          "Permission Required",
          "Voice control needs microphone and speech recognition permissions. Please enable them in Settings.",
          [{ text: "OK" }],
        );
      }

      return granted;
    } catch (err) {
      console.error("Failed to request speech recognition permission:", err);
      setError("Failed to request permission");
      return false;
    }
  }, []);

  const stopListening = useCallback(() => {
    if (!ExpoSpeechRecognitionModule) return;
    try {
      ExpoSpeechRecognitionModule.stop();
    } catch (err) {
      console.error("Failed to stop speech recognition:", err);
    }
    setIsListening(false);
  }, []);

  const startListening = useCallback(async () => {
    if (!ExpoSpeechRecognitionModule) return;
    if (isStartingRef.current) return;
    isStartingRef.current = true;

    try {
      setError(null);

      // Check permission first using ref for current value
      if (permissionGrantedRef.current === null) {
        const granted = await requestPermission();
        if (!granted) {
          isStartingRef.current = false;
          return;
        }
      } else if (!permissionGrantedRef.current) {
        Alert.alert(
          "Permission Required",
          "Voice control needs microphone and speech recognition permissions. Please enable them in Settings.",
          [{ text: "OK" }],
        );
        isStartingRef.current = false;
        return;
      }

      // Start speech recognition with iOS-specific options for reliability
      ExpoSpeechRecognitionModule.start({
        lang: "en-US",
        interimResults: true,
        continuous: true,
        contextualStrings: [
          "next step",
          "next",
          "go next",
          "continue",
          "proceed",
          "move on",
          "previous step",
          "previous",
          "go back",
          "back",
        ],
        // iOS-specific: helps with voice command recognition
        iosTaskHint: "dictation",
      });

      setIsListening(true);
    } catch (err) {
      console.error("Failed to start speech recognition:", err);
      setError("Failed to start voice recognition");
      setIsListening(false);
    } finally {
      isStartingRef.current = false;
    }
  }, [requestPermission]);

  // Handle speech recognition results - use refs for callbacks to avoid stale closures
  useSpeechRecognitionEvent("result", (event: any) => {
    if (!enabledRef.current || !event.results || event.results.length === 0)
      return;

    // Get the latest result - each result has a transcript property directly
    const latestResult = event.results[event.results.length - 1];
    if (!latestResult) return;

    const transcript = latestResult.transcript ?? "";
    setLastRecognizedText(transcript);

    // Only process if transcript has grown (new speech detected)
    const previousTranscript = lastTranscriptRef.current;
    if (transcript.length <= previousTranscript.length) {
      return;
    }

    // Extract only the NEW portion of the transcript
    const newText = transcript.slice(previousTranscript.length);

    // Check debounce
    const now = Date.now();
    if (now - lastCommandTimeRef.current < COMMAND_DEBOUNCE_MS) {
      lastTranscriptRef.current = transcript;
      return;
    }

    // Check if the NEW text contains command keywords
    const command = detectCommand(newText);

    // Execute command if detected
    if (command === "next") {
      lastCommandTimeRef.current = now;
      lastTranscriptRef.current = transcript;
      onNextStepRef.current();
    } else if (command === "previous" && onPreviousStepRef.current) {
      lastCommandTimeRef.current = now;
      lastTranscriptRef.current = transcript;
      onPreviousStepRef.current();
    } else {
      // Update transcript cache even if no command detected
      lastTranscriptRef.current = transcript;
    }
  });

  // Handle speech recognition errors
  useSpeechRecognitionEvent("error", (event: any) => {
    // Don't log or show error for common non-critical errors
    if (
      event.error === "no-speech" ||
      event.error === "aborted" ||
      event.error === "network" ||
      event.error === "client"
    ) {
      // These are expected:
      // - network: errors happen on timeout
      // - no-speech: when silent
      // - aborted: when manually stopped
      // - client: other client-side errors when stopping
      // The "end" event handler will auto-restart listening if still enabled
      return;
    }

    console.error("Speech recognition error:", event.error, event.message);
    setError(event.message || "Voice recognition error");
    setIsListening(false);
  });

  // Handle speech recognition end - restart if still enabled
  useSpeechRecognitionEvent("end", () => {
    setIsListening(false);
    // Reset transcript cache when session ends
    lastTranscriptRef.current = "";

    // Auto-restart if still enabled (for continuous listening)
    // Use ref to get current enabled value, not stale closure value
    if (enabledRef.current && !isStartingRef.current) {
      setTimeout(() => {
        if (enabledRef.current && !isStartingRef.current) {
          // Call the module directly to avoid stale startListening reference
          if (!ExpoSpeechRecognitionModule) return;
          isStartingRef.current = true;

          try {
            ExpoSpeechRecognitionModule.start({
              lang: "en-US",
              interimResults: true,
              continuous: true,
              contextualStrings: [
                "next step",
                "next",
                "previous step",
                "previous",
              ],
              // iOS-specific: helps with voice command recognition
              iosTaskHint: "dictation",
            });
            setIsListening(true);
          } catch (err) {
            console.error("Failed to restart speech recognition:", err);
            setIsListening(false);
          } finally {
            isStartingRef.current = false;
          }
        }
      }, 300);
    }
  });

  // Start/stop based on enabled prop - only depend on enabled
  useEffect(() => {
    if (enabled) {
      startListening();
    } else {
      stopListening();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (ExpoSpeechRecognitionModule) {
        try {
          ExpoSpeechRecognitionModule.stop();
        } catch {
          // Ignore errors during cleanup
        }
      }
    };
  }, []);

  // Stop listening when app goes to background
  useEffect(() => {
    const subscription = AppState.addEventListener("change", (nextAppState) => {
      if (nextAppState !== "active") {
        if (ExpoSpeechRecognitionModule) {
          try {
            ExpoSpeechRecognitionModule.stop();
          } catch {
            // Ignore
          }
        }
        setIsListening(false);
      } else if (nextAppState === "active" && enabledRef.current) {
        // Restart when coming back to foreground
        setTimeout(() => {
          if (enabledRef.current && !isStartingRef.current) {
            startListening();
          }
        }, 500);
      }
    });

    return () => {
      subscription.remove();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return {
    isListening,
    isSupported: isVoiceControlSupported,
    permissionGranted,
    lastRecognizedText,
    error,
    startListening,
    stopListening,
    requestPermission,
  };
}
