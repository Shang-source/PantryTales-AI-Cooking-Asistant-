"use client";

import type { ReactNode } from "react";
import { useState } from "react";
import {
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { Camera, Check, Loader2, Plus, Trash2, X } from "lucide-react-native";

import { Input } from "@/components/input";
import { useTheme } from "@/contexts/ThemeContext";

export interface RecognizedIngredient {
  id: string;
  name: string;
  confidence: number;
  quantity: number;
  unit: string;
  location: string;
  expiryDays: number;
  selected: boolean;
}

export interface CameraScannerProps {
  onClose: () => void;
  onConfirm: (ingredients: RecognizedIngredient[]) => void;
}

const buildMockRecognition = (): RecognizedIngredient[] => [
  {
    id: "1",
    name: "Tomato",
    confidence: 0.95,
    quantity: 6,
    unit: "pcs",
    location: "Fridge",
    expiryDays: 14,
    selected: true,
  },
  {
    id: "2",
    name: "Egg",
    confidence: 0.92,
    quantity: 6,
    unit: "pcs",
    location: "Fridge",
    expiryDays: 14,
    selected: true,
  },
  {
    id: "3",
    name: "Green Vegetable",
    confidence: 0.88,
    quantity: 1,
    unit: "bunch",
    location: "Fridge",
    expiryDays: 3,
    selected: true,
  },
];

interface ActionButtonProps {
  label: string;
  onPress: () => void;
  variant?: "solid" | "outline";
  icon?: ReactNode;
  disabled?: boolean;
  accentColor: string;
  textColor: string;
  borderColor: string;
  cardColor: string;
}

const ActionButton = ({
  label,
  onPress,
  variant = "solid",
  icon,
  disabled,
  accentColor,
  textColor,
  borderColor,
  cardColor,
}: ActionButtonProps) => (
  <TouchableOpacity
    onPress={onPress}
    activeOpacity={0.8}
    disabled={disabled}
    className={`w-full flex-row items-center justify-center rounded-full px-5 py-3 ${
      disabled ? "opacity-50" : ""
    }`}
    style={
      variant === "solid"
        ? { backgroundColor: accentColor }
        : { backgroundColor: cardColor, borderWidth: 1, borderColor: borderColor }
    }
  >
    {icon}
    <Text className="ml-2 text-base font-semibold" style={{ color: variant === "solid" ? "#ffffff" : textColor }}>
      {label}
    </Text>
  </TouchableOpacity>
);

export function CameraScanner({ onClose, onConfirm }: CameraScannerProps) {
  const { colors } = useTheme();
  const [step, setStep] = useState<"prompt" | "results">("prompt");
  const [scanning, setScanning] = useState(false);
  const [recognizedItems, setRecognizedItems] = useState<RecognizedIngredient[]>(
    []
  );
  const [lastCaptureUri, setLastCaptureUri] = useState<string | null>(null);

  const selectedCount = recognizedItems.filter((item) => item.selected).length;

  const runRecognition = () => {
    setScanning(true);
    setStep("results");
    setTimeout(() => {
      setRecognizedItems(buildMockRecognition());
      setScanning(false);
    }, 900);
  };

  const handleTakePhoto = async () => {
    // If native camera integration is needed, inject it via props and call here.
    setLastCaptureUri("captured-image.jpg");
    runRecognition();
  };

  const handlePickFromGallery = async () => {
    setLastCaptureUri("gallery-image.jpg");
    runRecognition();
  };

  const toggleItem = (id: string) => {
    setRecognizedItems((items) =>
      items.map((item) =>
        item.id === id ? { ...item, selected: !item.selected } : item
      )
    );
  };

  const updateItem = (
    id: string,
    field: keyof RecognizedIngredient,
    value: string
  ) => {
    setRecognizedItems((items) =>
      items.map((item) => {
        if (item.id !== id) return item;
        if (field === "quantity" || field === "expiryDays") {
          const numeric = parseInt(value || "0", 10);
          return { ...item, [field]: Number.isNaN(numeric) ? 0 : numeric };
        }
        return { ...item, [field]: value };
      })
    );
  };

  const removeItem = (id: string) => {
    setRecognizedItems((items) => items.filter((item) => item.id !== id));
  };

  const handleAddManual = () => {
    const now = Date.now().toString();
    const newItem: RecognizedIngredient = {
      id: `manual-${now}`,
      name: "",
      confidence: 0.5,
      quantity: 1,
      unit: "",
      location: "",
      expiryDays: 1,
      selected: true,
    };
    setRecognizedItems((items) => [...items, newItem]);
    setStep("results");
  };

  const handleConfirm = () => {
    const selectedItems = recognizedItems.filter((item) => item.selected);
    onConfirm(selectedItems);
    onClose();
  };

  const handleReset = () => {
    setRecognizedItems([]);
    setLastCaptureUri(null);
    setStep("prompt");
  };

  const renderOverlay = () => {
    if (!scanning) return null;
    const message =
      step === "prompt" ? "Opening camera..." : "Analyzing photo...";
    return (
      <View className="absolute inset-0 z-50 items-center justify-center bg-black/40 px-6">
        <View
          className="flex-row items-center gap-3 rounded-full px-4 py-3"
          style={{ backgroundColor: colors.card }}
        >
          <Loader2 className="h-5 w-5 animate-spin" color={colors.textPrimary} />
          <Text className="text-sm font-semibold" style={{ color: colors.textPrimary }}>{message}</Text>
        </View>
      </View>
    );
  };

  const renderPrompt = () => (
    <SafeAreaView className="absolute inset-0 z-50 flex-1 bg-black/50">
      <KeyboardAvoidingView
        behavior={Platform.OS === "ios" ? "padding" : undefined}
        className="flex-1 items-center px-4"
      >
        <View
          className="mt-6 w-full max-w-2xl overflow-hidden rounded-2xl"
          style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}
        >
          <View
            className="flex-row items-center justify-between px-4 py-3"
            style={{ borderBottomWidth: 1, borderBottomColor: colors.border }}
          >
            <View className="flex-row items-center gap-3">
              <View
                className="h-10 w-10 items-center justify-center rounded-full"
                style={{ backgroundColor: `${colors.accent}30` }}
              >
                <Camera className="h-5 w-5" color={colors.accent} />
              </View>
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                Ingredient Scanner
              </Text>
            </View>
            <TouchableOpacity
              onPress={onClose}
              className="h-9 w-9 items-center justify-center rounded-full"
              style={{ backgroundColor: colors.card }}
              activeOpacity={0.8}
            >
              <X className="h-4 w-4" color={colors.textSecondary} />
            </TouchableOpacity>
          </View>

          <View className="space-y-6 px-6 py-7">
            <Text className="text-sm leading-6" style={{ color: colors.textSecondary }}>
              Take a photo of your fridge, ingredients or groceries, and we will
              automatically recognize and add them to your inventory for easy
              management.
            </Text>

            <ActionButton
              label="Take Photo"
              onPress={handleTakePhoto}
              icon={<Camera className="h-5 w-5" color={colors.bg} />}
              accentColor={colors.accent}
              textColor={colors.textPrimary}
              borderColor={colors.border}
              cardColor={colors.card}
            />

            <ActionButton
              label="Select from Gallery"
              onPress={handlePickFromGallery}
              variant="outline"
              icon={<Camera className="h-5 w-5" color={colors.textPrimary} />}
              accentColor={colors.accent}
              textColor={colors.textPrimary}
              borderColor={colors.border}
              cardColor={colors.card}
            />
          </View>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );

  const renderResults = () => (
    <SafeAreaView className="absolute inset-0 z-50 flex-1 bg-black/50">
      <KeyboardAvoidingView
        behavior={Platform.OS === "ios" ? "padding" : undefined}
        className="flex-1 items-center px-4"
      >
        <View
          className="mt-4 w-full max-w-4xl overflow-hidden rounded-2xl"
          style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}
        >
          <View
            className="flex-row items-center justify-between px-4 py-3"
            style={{ borderBottomWidth: 1, borderBottomColor: colors.border }}
          >
            <View className="flex-row items-center gap-3">
              <TouchableOpacity
                onPress={handleReset}
                className="h-8 w-8 items-center justify-center rounded-full"
                style={{ backgroundColor: colors.card }}
                activeOpacity={0.8}
              >
                <X className="h-4 w-4" color={colors.textSecondary} />
              </TouchableOpacity>
              <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
                Ingredient Recognition
              </Text>
            </View>
            <TouchableOpacity
              onPress={onClose}
              className="h-8 w-8 items-center justify-center rounded-full"
              style={{ backgroundColor: colors.card }}
              activeOpacity={0.8}
            >
              <X className="h-4 w-4" color={colors.textSecondary} />
            </TouchableOpacity>
          </View>

          <ScrollView
            className="max-h-[620px]"
            showsVerticalScrollIndicator
            keyboardShouldPersistTaps="handled"
          >
            <View className="space-y-4 px-5 pb-6 pt-4">
              <View className="flex-row items-center justify-between">
                <Text className="text-sm font-medium" style={{ color: colors.textPrimary }}>
                  Scan result ({selectedCount}/{recognizedItems.length || 0})
                </Text>
                <TouchableOpacity
                  onPress={handleReset}
                  className="rounded-full px-3 py-1"
                  activeOpacity={0.8}
                >
                  <Text className="text-xs underline" style={{ color: colors.textSecondary }}>
                    Scan again
                  </Text>
                </TouchableOpacity>
              </View>

              {lastCaptureUri && (
                <Text className="text-xs" style={{ color: colors.textMuted }}>
                  Last photo: {lastCaptureUri}
                </Text>
              )}

              <View className="space-y-3">
                {recognizedItems.map((item) => (
                  <View
                    key={item.id}
                    className="rounded-xl p-4"
                    style={{
                      backgroundColor: item.selected ? `${colors.accent}15` : colors.card,
                      borderWidth: 1,
                      borderColor: item.selected ? `${colors.accent}50` : colors.border,
                    }}
                  >
                    <View className="flex-row items-start gap-3">
                      <TouchableOpacity
                        onPress={() => toggleItem(item.id)}
                        activeOpacity={0.8}
                        className="mt-1 h-5 w-5 items-center justify-center rounded"
                        style={{
                          backgroundColor: item.selected ? colors.accent : "transparent",
                          borderWidth: 2,
                          borderColor: item.selected ? colors.accent : colors.border,
                        }}
                      >
                        {item.selected && (
                          <Check className="h-3.5 w-3.5" color={colors.bg} />
                        )}
                      </TouchableOpacity>

                      <View className="flex-1 gap-3">
                        <View className="flex-row items-center gap-2">
                          <Input
                            value={item.name}
                            onChangeText={(text) =>
                              updateItem(item.id, "name", text)
                            }
                            placeholder="Ingredient name"
                            placeholderTextColor={colors.textMuted}
                            className="flex-1 rounded-lg text-sm"
                            style={{
                              backgroundColor: colors.card,
                              borderWidth: 1,
                              borderColor: colors.border,
                              color: colors.textPrimary,
                            }}
                          />
                          <Text className="text-xs" style={{ color: colors.textMuted }}>
                            {Math.round(item.confidence * 100)}% confidence
                          </Text>
                          <TouchableOpacity
                            onPress={() => removeItem(item.id)}
                            className="ml-1 h-9 w-9 items-center justify-center rounded-full"
                            style={{ backgroundColor: colors.card }}
                            activeOpacity={0.8}
                            accessibilityLabel="Remove item"
                          >
                            <Trash2 className="h-4 w-4" color={colors.error} />
                          </TouchableOpacity>
                        </View>

                        <View className="gap-3">
                          <View className="flex-row gap-3">
                            <View className="flex-1">
                              <Text className="mb-1 text-[10px] uppercase" style={{ color: colors.textMuted }}>
                                Quantity
                              </Text>
                              <Input
                                value={String(item.quantity)}
                                onChangeText={(text) =>
                                  updateItem(item.id, "quantity", text)
                                }
                                keyboardType="numeric"
                                className="rounded-lg text-sm"
                                style={{
                                  backgroundColor: colors.card,
                                  borderWidth: 1,
                                  borderColor: colors.border,
                                  color: colors.textPrimary,
                                }}
                              />
                            </View>
                            <View className="flex-1">
                              <Text className="mb-1 text-[10px] uppercase" style={{ color: colors.textMuted }}>
                                Unit
                              </Text>
                              <Input
                                value={item.unit}
                                onChangeText={(text) =>
                                  updateItem(item.id, "unit", text)
                                }
                                placeholder="pcs / g / kg / bunch"
                                placeholderTextColor={colors.textMuted}
                                className="rounded-lg text-sm"
                                style={{
                                  backgroundColor: colors.card,
                                  borderWidth: 1,
                                  borderColor: colors.border,
                                  color: colors.textPrimary,
                                }}
                              />
                            </View>
                          </View>

                          <View>
                            <Text className="mb-1 text-[10px] uppercase" style={{ color: colors.textMuted }}>
                              Location
                            </Text>
                            <Input
                              value={item.location}
                              onChangeText={(text) =>
                                updateItem(item.id, "location", text)
                              }
                              placeholder="Fridge / Freezer / Pantry"
                              placeholderTextColor={colors.textMuted}
                              className="rounded-lg text-sm"
                              style={{
                                backgroundColor: colors.card,
                                borderWidth: 1,
                                borderColor: colors.border,
                                color: colors.textPrimary,
                              }}
                            />
                          </View>

                          <View>
                            <Text className="mb-1 text-[10px] uppercase" style={{ color: colors.textMuted }}>
                              Expiry (days)
                            </Text>
                            <Input
                              value={String(item.expiryDays)}
                              onChangeText={(text) =>
                                updateItem(item.id, "expiryDays", text)
                              }
                              keyboardType="numeric"
                              className="rounded-lg text-sm"
                              style={{
                                backgroundColor: colors.card,
                                borderWidth: 1,
                                borderColor: colors.border,
                                color: colors.textPrimary,
                              }}
                            />
                          </View>
                        </View>
                      </View>
                    </View>
                  </View>
                ))}
              </View>

              <TouchableOpacity
                onPress={handleAddManual}
                activeOpacity={0.85}
                className="mt-1 flex-row items-center justify-center rounded-full px-4 py-3"
                style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
              >
                <Plus className="mr-2 h-4 w-4" color={colors.textPrimary} />
                <Text className="text-xs font-semibold" style={{ color: colors.textPrimary }}>
                  Add manually
                </Text>
              </TouchableOpacity>
            </View>
          </ScrollView>

          <View className="px-4 py-3" style={{ borderTopWidth: 1, borderTopColor: colors.border }}>
            <TouchableOpacity
              onPress={handleConfirm}
              disabled={selectedCount === 0}
              activeOpacity={0.85}
              className={`h-12 w-full items-center justify-center rounded-full ${
                selectedCount === 0 ? "opacity-50" : ""
              }`}
              style={{ backgroundColor: colors.accent }}
            >
              <Text className="text-base font-semibold" style={{ color: colors.bg }}>
                Confirm ({selectedCount})
              </Text>
            </TouchableOpacity>
          </View>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );

  if (step === "prompt") {
    return (
      <>
        {renderPrompt()}
        {renderOverlay()}
      </>
    );
  }

  return (
    <>
      {renderResults()}
      {renderOverlay()}
    </>
  );
}
