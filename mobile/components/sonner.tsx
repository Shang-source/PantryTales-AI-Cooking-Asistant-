import { useState, useEffect, useRef, useCallback } from "react";
import { View, Text, TouchableOpacity, Animated } from "react-native";
import { CheckCircle, XCircle, Info, X } from "lucide-react-native";

type ToastType = "default" | "success" | "error" | "info";

interface ToastData {
  id: string;
  message: string;
  description?: string;
  type: ToastType;
  duration?: number;
  action?: {
    label: string;
    onClick: () => void;
  };
}

class ToastManager {
  private listeners = new Set<(toast: ToastData) => void>();

  subscribe(listener: (toast: ToastData) => void) {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  show(
    message: string,
    options: Partial<Omit<ToastData, "id" | "message">> = {}
  ) {
    const id = Math.random().toString(36).substring(2, 9);
    const toast: ToastData = {
      id,
      message,
      type: options.type || "default",
      duration: options.duration || 3000,
      ...options,
    };
    console.log("Toast Triggered:", message);
    this.listeners.forEach((listener) => listener(toast));
    return id;
  }
}

const toastManager = new ToastManager();

export const toast = (message: string, options?: any) =>
  toastManager.show(message, { ...options, type: "default" });
toast.success = (message: string, options?: any) =>
  toastManager.show(message, { ...options, type: "success" });
toast.error = (message: string, options?: any) =>
  toastManager.show(message, { ...options, type: "error" });
toast.info = (message: string, options?: any) =>
  toastManager.show(message, { ...options, type: "info" });

const ToastItem = ({
  item,
  onDismiss,
}: {
  item: ToastData;
  onDismiss: (id: string) => void;
}) => {
  const opacity = useRef(new Animated.Value(0)).current;
  const translateY = useRef(new Animated.Value(-50)).current;

  const handleDismiss = useCallback(() => {
    Animated.parallel([
      Animated.timing(opacity, {
        toValue: 0,
        duration: 250,
        useNativeDriver: true,
      }),
      Animated.timing(translateY, {
        toValue: -50,
        duration: 250,
        useNativeDriver: true,
      }),
    ]).start(() => {
      onDismiss(item.id);
    });
  }, [onDismiss, item.id, opacity, translateY]);

  useEffect(() => {
    Animated.parallel([
      Animated.timing(opacity, {
        toValue: 1,
        duration: 300,
        useNativeDriver: true,
      }),
      Animated.spring(translateY, {
        toValue: 0,
        friction: 6,
        tension: 50,
        useNativeDriver: true,
      }),
    ]).start();

    const timer = setTimeout(() => handleDismiss(), item.duration);
    return () => clearTimeout(timer);
  }, [handleDismiss, item.duration, opacity, translateY]);

  const getIcon = () => {
    switch (item.type) {
      case "success":
        return <CheckCircle size={24} color="#10b981" />;
      case "error":
        return <XCircle size={24} color="#ef4444" />;
      case "info":
        return <Info size={24} color="#3b82f6" />;
      default:
        return null;
    }
  };

  return (
    <Animated.View
      style={{ opacity, transform: [{ translateY }] }}
      className="w-full max-w-[90%] px-4 mb-3 z-[100]"
    >
      <View className="flex-row items-center bg-white dark:bg-zinc-900 border border-gray-100 dark:border-zinc-800 rounded-2xl p-4 shadow-xl shadow-black/20">
        {item.type !== "default" && <View className="mr-3">{getIcon()}</View>}

        <View className="flex-1 justify-center">
          <Text className="text-sm font-bold text-black dark:text-white">
            {item.message}
          </Text>
          {item.description && (
            <Text className="text-xs text-gray-500 mt-0.5">
              {item.description}
            </Text>
          )}
        </View>

        {item.action && (
          <TouchableOpacity
            onPress={() => {
              item.action?.onClick();
              handleDismiss();
            }}
            className="ml-3 bg-black dark:bg-white px-3 py-1.5 rounded-lg"
          >
            <Text className="text-xs font-bold text-white dark:text-black">
              {item.action.label}
            </Text>
          </TouchableOpacity>
        )}
      </View>
    </Animated.View>
  );
};

export const Toaster = () => {
  const [toasts, setToasts] = useState<ToastData[]>([]);

  useEffect(() => {
    return toastManager.subscribe((newToast) =>
      setToasts((p) => [...p, newToast])
    );
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  if (toasts.length === 0) return null;

  return (
    <View
      className="absolute inset-0 z-[9999] pt-14 px-4 pointer-events-none items-center justify-start"
      pointerEvents="box-none"
    >
      {toasts.map((item) => (
        <ToastItem key={item.id} item={item} onDismiss={removeToast} />
      ))}
    </View>
  );
};
