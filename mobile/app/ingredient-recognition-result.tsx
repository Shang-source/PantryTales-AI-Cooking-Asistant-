import { useState } from "react";
import {
  View,
  Text,
  TouchableOpacity,
  ScrollView,
  TextInput,
  Alert,
  Pressable,
} from "react-native";
import { Stack, useLocalSearchParams, router } from "expo-router";
import { SafeAreaView } from "react-native-safe-area-context";
import {
  X,
  Check,
  Trash2,
  ChevronDown,
  Receipt,
  Camera,
  ChevronRight,
} from "lucide-react-native";
import { useAuthMutation } from "@/hooks/useApi";
import { RecognizedIngredient, FilteredItem } from "@/hooks/useVisionRecognition";
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
} from "@/components/dropdown-menu";
import { useTheme } from "@/contexts/ThemeContext";

// Storage method options
const STORAGE_METHODS = ["Fridge", "Freezer", "Pantry", "Counter"];

interface EditableIngredient extends RecognizedIngredient {
  id: string;
  selected: boolean;
  quantityStr: string;
  expirationDaysStr: string;
}

export default function IngredientRecognitionResultScreen() {
  const { colors } = useTheme();
  const {
    ingredients: ingredientsParam,
    imageType: imageTypeParam,
    storeName: storeNameParam,
    filteredItems: filteredItemsParam,
  } = useLocalSearchParams<{
    ingredients: string;
    imageType?: string;
    storeName?: string;
    filteredItems?: string;
  }>();

  // Parse ingredients from params
  const parsedIngredients: RecognizedIngredient[] = ingredientsParam
    ? JSON.parse(ingredientsParam)
    : [];

  // Parse image type and filtered items
  const imageType = (imageTypeParam || "ingredients") as
    | "receipt"
    | "ingredients"
    | "unknown";
  const storeName = storeNameParam || null;
  const filteredItems: FilteredItem[] = filteredItemsParam
    ? JSON.parse(filteredItemsParam)
    : [];

  // State for filtered items expansion
  const [showFilteredItems, setShowFilteredItems] = useState(false);

  // Convert to editable format with unique IDs
  const [editableIngredients, setEditableIngredients] = useState<
    EditableIngredient[]
  >(
    parsedIngredients.map((ing, idx) => ({
      ...ing,
      id: `ing-${idx}`,
      selected: true,
      quantityStr: ing.quantity.toString(),
      expirationDaysStr: String(ing.suggestedExpirationDays ?? 14),
    })),
  );

  // Mutation to add ingredients to inventory
  const addToInventory = useAuthMutation<unknown, unknown[]>(
    "/api/inventory/batch",
    "POST",
    {
      onSuccess: () => {
        Alert.alert("Success", "Ingredients added to inventory!", [
          { text: "OK", onPress: () => router.back() },
        ]);
      },
      onError: (error) => {
        Alert.alert("Error", error.message || "Failed to add ingredients.");
      },
    },
  );

  const toggleSelection = (id: string) => {
    setEditableIngredients((prev) =>
      prev.map((ing) =>
        ing.id === id ? { ...ing, selected: !ing.selected } : ing,
      ),
    );
  };

  const updateIngredient = (
    id: string,
    field: keyof EditableIngredient,
    value: string,
  ) => {
    setEditableIngredients((prev) =>
      prev.map((ing) => (ing.id === id ? { ...ing, [field]: value } : ing)),
    );
  };

  const removeIngredient = (id: string) => {
    setEditableIngredients((prev) => prev.filter((ing) => ing.id !== id));
  };

  const handleConfirm = () => {
    const selectedIngredients = editableIngredients
      .filter((ing) => ing.selected)
      .map((ing) => ({
        name: ing.name,
        amount: parseFloat(ing.quantityStr) || ing.quantity,
        unit: ing.unit,
        storageMethod: getStorageMethodValue(
          ing.suggestedStorageMethod || "Fridge",
        ),
        expirationDays: parseInt(ing.expirationDaysStr) || 14,
      }));

    if (selectedIngredients.length === 0) {
      Alert.alert("No Selection", "Please select at least one ingredient.");
      return;
    }

    addToInventory.mutate(selectedIngredients);
  };

  const selectedCount = editableIngredients.filter((i) => i.selected).length;

  return (
    <>
      <Stack.Screen
        options={{
          headerShown: false,
        }}
      />
      <SafeAreaView
        className="flex-1"
        style={{ backgroundColor: colors.bg }}
      >
        {/* Header */}
        <View className="flex-row items-center justify-between px-4 py-3 border-b" style={{ borderBottomColor: colors.border }}>
          <TouchableOpacity onPress={() => router.back()}>
            <X size={24} color={colors.textPrimary} />
          </TouchableOpacity>
          <Text className="text-lg font-semibold" style={{ color: colors.textPrimary }}>
            Ingredient Recognition
          </Text>
          <TouchableOpacity onPress={() => router.back()}>
            <X size={24} color="transparent" />
          </TouchableOpacity>
        </View>

        {/* Image Type Badge */}
        <View className="px-4 pt-3 pb-2">
          <View className="flex-row items-center gap-2">
            <View
              className={`flex-row items-center gap-1.5 px-3 py-1.5 rounded-full ${
                imageType === "receipt" ? "bg-blue-500/20" : "bg-green-500/20"
              }`}
            >
              {imageType === "receipt" ? (
                <Receipt size={14} color="#60A5FA" />
              ) : (
                <Camera size={14} color="#4ADE80" />
              )}
              <Text
                className={`text-sm font-medium ${
                  imageType === "receipt" ? "text-blue-400" : "text-green-400"
                }`}
              >
                {imageType === "receipt" ? "Scanned from receipt" : "Scanned ingredients"}
              </Text>
            </View>
            {storeName && (
              <Text className="text-sm" style={{ color: colors.textMuted }}>• {storeName}</Text>
            )}
          </View>
        </View>

        {/* Subtitle */}
        <View className="px-4 py-2 flex-row items-center justify-between">
          <View>
            <Text className="font-semibold" style={{ color: colors.textPrimary }}>
              Scan result ({selectedCount}/{editableIngredients.length})
            </Text>
          </View>
          <TouchableOpacity onPress={() => router.back()}>
            <Text className="font-medium" style={{ color: colors.accent }}>Scan again</Text>
          </TouchableOpacity>
        </View>

        {/* Filtered Items Section (for receipts) */}
        {filteredItems.length > 0 && (
          <View className="px-4 pb-2">
            <Pressable
              onPress={() => setShowFilteredItems(!showFilteredItems)}
              className="flex-row items-center justify-between rounded-lg px-3 py-2"
              style={{ backgroundColor: colors.card }}
            >
              <Text className="text-sm" style={{ color: colors.textMuted }}>
                {filteredItems.length} non-grocery item
                {filteredItems.length > 1 ? "s" : ""} filtered out
              </Text>
              <ChevronRight
                size={16}
                color={colors.textMuted}
                style={{
                  transform: [{ rotate: showFilteredItems ? "90deg" : "0deg" }],
                }}
              />
            </Pressable>
            {showFilteredItems && (
              <View className="mt-2 rounded-lg p-3" style={{ backgroundColor: colors.card }}>
                {filteredItems.map((item, idx) => (
                  <View
                    key={idx}
                    className="flex-row items-center justify-between py-1"
                  >
                    <Text className="text-sm flex-1" style={{ color: colors.textMuted }}>
                      {item.text}
                    </Text>
                    <Text className="text-xs ml-2 capitalize" style={{ color: colors.textMuted }}>
                      {item.reason}
                    </Text>
                  </View>
                ))}
              </View>
            )}
          </View>
        )}

        {/* Ingredients List */}
        <ScrollView
          className="flex-1 px-4"
          showsVerticalScrollIndicator={false}
        >
          {editableIngredients.map((ingredient) => (
            <View
              key={ingredient.id}
              className="rounded-2xl p-4 mb-4"
              style={{ backgroundColor: colors.card }}
            >
              {/* Header Row */}
              <View className="flex-row items-center justify-between mb-3">
                <View className="flex-row items-center gap-3 flex-1">
                  <TouchableOpacity
                    onPress={() => toggleSelection(ingredient.id)}
                    className="w-6 h-6 rounded border-2 items-center justify-center"
                    style={{
                      backgroundColor: ingredient.selected ? colors.accent : "transparent",
                      borderColor: ingredient.selected ? colors.accent : colors.border,
                    }}
                  >
                    {ingredient.selected && <Check size={14} color={colors.bg} />}
                  </TouchableOpacity>
                  <TextInput
                    value={ingredient.name}
                    onChangeText={(v) =>
                      updateIngredient(ingredient.id, "name", v)
                    }
                    placeholder="Ingredient name"
                    className="font-semibold text-base flex-1 rounded-lg px-2 py-1"
                    style={{ color: colors.textPrimary, backgroundColor: colors.bg }}
                    placeholderTextColor={colors.textMuted}
                  />
                </View>
                <View className="flex-row items-center gap-3 ml-2">
                  <Text className="text-sm" style={{ color: colors.textMuted }}>
                    {Math.round(ingredient.confidence * 100)}%
                  </Text>
                  <TouchableOpacity
                    onPress={() => removeIngredient(ingredient.id)}
                  >
                    <Trash2 size={18} color={colors.textMuted} />
                  </TouchableOpacity>
                </View>
              </View>

              {/* Quantity and Unit Row */}
              <View className="flex-row gap-3 mb-3">
                <View className="flex-1">
                  <Text className="text-xs mb-1 uppercase" style={{ color: colors.textMuted }}>
                    Quantity
                  </Text>
                  <TextInput
                    value={ingredient.quantityStr}
                    onChangeText={(v) =>
                      updateIngredient(ingredient.id, "quantityStr", v)
                    }
                    keyboardType="numeric"
                    className="rounded-lg px-3 py-2.5"
                    style={{ backgroundColor: colors.bg, color: colors.textPrimary }}
                    placeholderTextColor={colors.textMuted}
                  />
                </View>
                <View className="flex-1">
                  <Text className="text-xs mb-1 uppercase" style={{ color: colors.textMuted }}>
                    Unit
                  </Text>
                  <TextInput
                    value={ingredient.unit}
                    onChangeText={(v) =>
                      updateIngredient(ingredient.id, "unit", v)
                    }
                    className="rounded-lg px-3 py-2.5"
                    style={{ backgroundColor: colors.bg, color: colors.textPrimary }}
                    placeholderTextColor={colors.textMuted}
                  />
                </View>
              </View>

              {/* Location Row */}
              <View className="mb-3">
                <Text className="text-xs mb-1 uppercase" style={{ color: colors.textMuted }}>
                  Location
                </Text>
                <DropdownMenu>
                  <DropdownMenuTrigger
                    className="rounded-lg px-3 py-2.5 flex-row items-center justify-between"
                    style={{ backgroundColor: colors.bg }}
                  >
                    <Text style={{ color: colors.textPrimary }}>
                      {ingredient.suggestedStorageMethod || "Fridge"}
                    </Text>
                    <ChevronDown size={16} color={colors.textMuted} />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="left">
                    {STORAGE_METHODS.map((method) => (
                      <DropdownMenuItem
                        key={method}
                        onPress={() =>
                          updateIngredient(
                            ingredient.id,
                            "suggestedStorageMethod",
                            method,
                          )
                        }
                        className="flex-row items-center justify-between"
                      >
                        <Text style={{ color: colors.textPrimary }}>{method}</Text>
                        {(ingredient.suggestedStorageMethod || "Fridge") ===
                          method && <Check size={16} color={colors.textPrimary} />}
                      </DropdownMenuItem>
                    ))}
                  </DropdownMenuContent>
                </DropdownMenu>
              </View>

              {/* Expiry Row */}
              <View>
                <Text className="text-xs mb-1 uppercase" style={{ color: colors.textMuted }}>
                  Expiry (Days)
                </Text>
                  <TextInput
                    value={ingredient.expirationDaysStr}
                    onChangeText={(v) =>
                      updateIngredient(ingredient.id, "expirationDaysStr", v)
                    }
                    keyboardType="numeric"
                    className="rounded-lg px-3 py-2.5"
                    placeholderTextColor={colors.textMuted}
                    style={{ backgroundColor: colors.bg, color: colors.textPrimary }}
                  />
              </View>
            </View>
          ))}
        </ScrollView>

        {/* Confirm Button */}
        <View className="px-4 pb-4">
          <TouchableOpacity
            onPress={handleConfirm}
            disabled={addToInventory.isPending || selectedCount === 0}
            className="rounded-xl py-4 items-center"
            style={{
              backgroundColor: selectedCount === 0 ? `${colors.accent}80` : colors.accent,
            }}
          >
            <Text className="font-semibold text-base" style={{ color: colors.bg }}>
              {addToInventory.isPending
                ? "Adding..."
                : `Confirm (${selectedCount})`}
            </Text>
          </TouchableOpacity>
        </View>
      </SafeAreaView>
    </>
  );
}

// Convert storage method string to enum value (matches InventoryStorageMethod)
function getStorageMethodValue(method: string): number {
  switch (method.toLowerCase()) {
    case "fridge":
    case "refrigerated":
      return 2; // Refrigerated
    case "freezer":
    case "frozen":
      return 3; // Frozen
    case "pantry":
    case "counter":
    case "roomtemp":
    case "room temp":
      return 1; // RoomTemp
    default:
      return 1; // Default to RoomTemp
  }
}
