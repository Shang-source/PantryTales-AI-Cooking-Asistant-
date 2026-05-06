import { Slot, ErrorBoundary } from "expo-router";
import "./global.css";
import { ClerkProvider } from "@clerk/clerk-expo";
import { tokenCache } from "@clerk/clerk-expo/token-cache";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Toaster } from "@/components/sonner";
import { GestureHandlerRootView } from "react-native-gesture-handler";
import { CameraScannerProvider } from "@/hooks/useCameraScanner";
import { ThemeProvider } from "@/contexts/ThemeContext";

const queryClient = new QueryClient();

const publishableKey = process.env.EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY;

if (!publishableKey) {
  throw new Error(
    "Missing EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY. Add it to .env.local"
  );
}

export { ErrorBoundary };

export default function RootLayout() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <ThemeProvider>
        <ClerkProvider publishableKey={publishableKey} tokenCache={tokenCache}>
          <QueryClientProvider client={queryClient}>
            <CameraScannerProvider>
              <Slot />
            </CameraScannerProvider>
          </QueryClientProvider>
          <Toaster />
        </ClerkProvider>
      </ThemeProvider>
    </GestureHandlerRootView>
  );
}
