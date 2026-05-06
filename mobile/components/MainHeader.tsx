import { useState } from "react";
import {
  View,
  Text,
  TouchableOpacity,
  Image,
  ScrollView,
  TouchableOpacityProps,
  Alert,
  Modal,
  ActivityIndicator,
} from "react-native";
import { Feather } from "@expo/vector-icons";
import { router } from "expo-router";
import {
  Sheet,
  SheetTrigger,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetClose,
} from "./sheet";
import { RecognitionModeSheet } from "./RecognitionModeSheet";
import { IngredientScannerSheet } from "./IngredientScannerSheet";
import { RecipeScannerSheet } from "./RecipeScannerSheet";
import {
  useIngredientRecognition,
  useRecipeRecognition,
} from "@/hooks/useVisionRecognition";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";
const logoImage = require("../assets/images/logo.jpg");

interface NavItemProps extends TouchableOpacityProps {
  iconName: React.ComponentProps<typeof Feather>["name"];
  label: string;
  accentColor?: string;
}

function NavItem({ iconName, label, accentColor = "#D4A5A5", textColor, ...props }: NavItemProps & { textColor?: string }) {
  return (
    <TouchableOpacity
      {...props}
      className={cn(
        "px-4 py-3 flex-row items-center rounded-lg",
        props.className,
      )}
    >
      <Feather name={iconName} size={20} color={accentColor} />
      <Text className="ml-3 font-medium" style={{ color: textColor || "#fff" }}>{label}</Text>
    </TouchableOpacity>
  );
}

export default function MainHeader() {
  const { colors } = useTheme();
  const [isRecognitionSheetVisible, setIsRecognitionSheetVisible] =
    useState(false);
  const [isIngredientScannerVisible, setIsIngredientScannerVisible] =
    useState(false);
  const [isRecipeScannerVisible, setIsRecipeScannerVisible] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [processingMessage, setProcessingMessage] = useState("");
  const [currentRecipeImageUri, setCurrentRecipeImageUri] = useState<
    string | null
  >(null);
  const accent = colors.accent;

  const formatRecognitionMessage = (
    kind: "ingredients" | "recipe",
    rawMessage: string | undefined | null,
  ) => {
    const normalized = (rawMessage || "").trim();
    if (!normalized) {
      return kind === "recipe"
        ? "Could not identify the dish. Please try a clearer photo."
        : "Could not detect any ingredients in the image. Please try a clearer photo.";
    }

    if (
      kind === "recipe" &&
      normalized.toLowerCase() === "cannot identify dish"
    ) {
      return "Could not identify the dish in the image. Please try a clearer photo.";
    }

    return normalized;
  };

  // Vision recognition hooks
  const ingredientRecognition = useIngredientRecognition({
    onSuccess: (data) => {
      console.log("Ingredient recognition success:", data);
      // Keep loading visible for a moment then navigate
      setTimeout(() => {
        setIsProcessing(false);
        if (data.data?.success && data.data.ingredients.length > 0) {
          router.push({
            pathname: "/ingredient-recognition-result",
            params: {
              ingredients: JSON.stringify(data.data.ingredients),
              imageType: data.data.imageType || "ingredients",
              storeName: data.data.storeName || "",
              filteredItems: data.data.filteredItems
                ? JSON.stringify(data.data.filteredItems)
                : "",
            },
          });
        } else {
          const message = formatRecognitionMessage(
            "ingredients",
            data.data?.errorMessage || data.message,
          );
          Alert.alert("No Ingredients Found", message);
        }
      }, 300);
    },
    onError: (error) => {
      console.error("Ingredient recognition error:", error);
      setIsProcessing(false);
      Alert.alert("Recognition Failed", error.message || "Please try again.");
    },
  });

  const recipeRecognition = useRecipeRecognition({
    onSuccess: (data) => {
      console.log("Recipe recognition success:", data);
      setTimeout(() => {
        setIsProcessing(false);
        if (data.data?.success && data.data.recipe) {
          router.push({
            pathname: "/scanner-result",
            params: {
              recipe: JSON.stringify(data.data.recipe),
              imageUri: currentRecipeImageUri ?? undefined,
            },
          });
        } else {
          const message = formatRecognitionMessage(
            "recipe",
            data.data?.errorMessage || data.message,
          );
          Alert.alert("No Recipe Found", message);
        }
      }, 300);
    },
    onError: (error) => {
      console.error("Recipe recognition error:", error);
      setIsProcessing(false);
      Alert.alert("Recognition Failed", error.message || "Please try again.");
    },
  });

  const handleIngredientScan = () => {
    setIsRecognitionSheetVisible(false);
    setIsIngredientScannerVisible(true);
  };

  const handleRecipeScan = () => {
    setIsRecognitionSheetVisible(false);
    setIsRecipeScannerVisible(true);
  };

  const handleIngredientTakePhoto = (uri: string) => {
    console.log("Processing ingredient image:", uri);
    setIsProcessing(true);
    setProcessingMessage("Analyzing ingredients...");
    ingredientRecognition.mutate(uri);
  };

  const handleIngredientSelectFromGallery = (uri: string) => {
    console.log("Processing selected ingredient image:", uri);
    setIsProcessing(true);
    setProcessingMessage("Analyzing ingredients...");
    ingredientRecognition.mutate(uri);
  };

  const handleRecipeTakePhoto = (uri: string) => {
    console.log("Processing recipe image:", uri);
    setCurrentRecipeImageUri(uri);
    setIsProcessing(true);
    setProcessingMessage("Analyzing...");
    recipeRecognition.mutate(uri);
  };

  const handleRecipeSelectFromGallery = (uri: string) => {
    console.log("Processing selected recipe image:", uri);
    setCurrentRecipeImageUri(uri);
    setIsProcessing(true);
    setProcessingMessage("Analyzing...");
    recipeRecognition.mutate(uri);
  };

  return (
    <>
      <View style={{ backgroundColor: colors.bg, borderBottomWidth: 1, borderBottomColor: colors.border }} className="px-4 py-3">
        {/* Top Row */}
        <View className="flex-row items-center justify-between">
          <Sheet>
            <SheetTrigger asChild>
              <TouchableOpacity className="flex-row items-center justify-between">
                <Feather name="menu" size={24} color={accent} />
              </TouchableOpacity>
            </SheetTrigger>
            <SheetContent
              showCloseButton={false}
              className="w-[260px] max-w-[82vw] p-0 border-0 shadow-none"
              style={{ backgroundColor: colors.bg }}
            >
              <View className="flex-1" style={{ backgroundColor: colors.bg }}>
                <SheetHeader className="w-full" style={{ backgroundColor: colors.bg }}>
                  <View className="flex-row items-center px-4 pb-5" style={{ backgroundColor: colors.bg, borderBottomWidth: 1, borderBottomColor: colors.border }}>
                    <View className="mr-4 h-20 w-20 overflow-hidden rounded-[12px] border" style={{ borderColor: colors.border }}>
                      <Image source={logoImage} className="h-full w-full" />
                    </View>
                    <View className="flex-row items-baseline">
                      <Text className="text-[22px] font-extrabold tracking-tight" style={{ color: colors.textPrimary }}>
                        Pantry
                      </Text>
                      <Text style={{ color: accent }} className="text-[22px] font-extrabold tracking-tight">
                        Tales
                      </Text>
                    </View>
                  </View>
                </SheetHeader>
                <ScrollView
                  className="flex-1"
                  style={{ backgroundColor: colors.bg }}
                  contentContainerStyle={{ flexGrow: 1 }}
                >
                  <View className="flex-1 px-2 py-1">
                    <View className="gap-6">
                      <View className="gap-3">
                        <SheetClose onPress={() => router.push("/")} asChild>
                          <NavItem iconName="home" label="Home" accentColor={accent} textColor={colors.textPrimary} />
                        </SheetClose>
                      </View>

                      <View className="gap-3">
                        <SheetTitle className="px-4 text-xs font-semibold" style={{ color: colors.textMuted }}>
                          INVENTORY
                        </SheetTitle>
                        <View className="gap-3">
                          <SheetClose
                            onPress={() => router.push("/MyInventory")}
                            asChild
                          >
                            <NavItem iconName="package" label="My Inventory" accentColor={accent} textColor={colors.textPrimary} />
                          </SheetClose>
                          <SheetClose
                            onPress={() => router.push("/checklist")}
                            asChild
                          >
                            <NavItem
                              iconName="shopping-cart"
                              label="Checklist"
                              accentColor={accent}
                              textColor={colors.textPrimary}
                            />
                          </SheetClose>
                        </View>
                      </View>

                      <View className="gap-3 pt-1">
                        <SheetTitle className="px-4 text-xs font-semibold" style={{ color: colors.textMuted }}>
                          COMMUNITY
                        </SheetTitle>
                        <View className="gap-3">
                          <SheetClose
                            onPress={() => router.push("/add")}
                            asChild
                          >
                            <NavItem iconName="plus" label="Post a Recipe" accentColor={accent} textColor={colors.textPrimary} />
                          </SheetClose>
                          <SheetClose
                            onPress={() => router.push("/community")}
                            asChild
                          >
                            <NavItem iconName="users" label="Community" accentColor={accent} textColor={colors.textPrimary} />
                          </SheetClose>
                        </View>
                      </View>
                      <View className="gap-3 pt-1">
                        <SheetTitle className="px-4 text-xs font-semibold" style={{ color: colors.textMuted }}>
                          COOKING & SUPPORT
                        </SheetTitle>
                        <View className="gap-3">
                          <SheetClose
                            onPress={() => router.push("/cooking-history")}
                            asChild
                          >
                            <NavItem iconName="book" label="Cooking History" accentColor={accent} textColor={colors.textPrimary} />
                          </SheetClose>
                          <SheetClose
                            onPress={() => router.push("/KnowledgeBase")}
                            asChild
                          >
                            <NavItem
                              iconName="book-open"
                              label="Cooking Tips"
                              accentColor={accent}
                              textColor={colors.textPrimary}
                            />
                          </SheetClose>
                        </View>
                      </View>
                      <View className="gap-3 pt-1">
                        <SheetTitle className="px-4 text-xs font-semibold" style={{ color: colors.textMuted }}>
                          SETTINGS
                        </SheetTitle>
                        <View className="gap-3">
                          <SheetClose
                            onPress={() => router.push("/settings")}
                            asChild
                          >
                            <NavItem iconName="settings" label="Settings" accentColor={accent} textColor={colors.textPrimary} />
                          </SheetClose>
                        </View>
                      </View>
                    </View>
                  </View>
                </ScrollView>
              </View>
            </SheetContent>
          </Sheet>

          {/* Title */}
          <View className="flex-row items-baseline">
            <Text className="text-lg font-semibold tracking-wide" style={{ color: colors.textPrimary }}>
              Pantry
            </Text>
            <Text style={{ color: accent }} className="text-lg font-semibold tracking-wide">
              Tales
            </Text>
          </View>
          {/* Scan Button */}
          <TouchableOpacity onPress={() => setIsRecognitionSheetVisible(true)}>
            <Feather name="camera" size={24} color={accent} />
          </TouchableOpacity>
        </View>
      </View>
      <RecognitionModeSheet
        visible={isRecognitionSheetVisible}
        onClose={() => setIsRecognitionSheetVisible(false)}
        onIngredientScan={handleIngredientScan}
        onRecipeScan={handleRecipeScan}
      />
      <IngredientScannerSheet
        visible={isIngredientScannerVisible}
        onClose={() => setIsIngredientScannerVisible(false)}
        onTakePhoto={handleIngredientTakePhoto}
        onSelectFromGallery={handleIngredientSelectFromGallery}
      />
      <RecipeScannerSheet
        visible={isRecipeScannerVisible}
        onClose={() => setIsRecipeScannerVisible(false)}
        onTakePhoto={handleRecipeTakePhoto}
        onSelectFromGallery={handleRecipeSelectFromGallery}
      />

      {/* Processing Modal */}
      <Modal visible={isProcessing} transparent animationType="none">
        <View
          className="flex-1 items-center justify-center"
          style={{ backgroundColor: "rgba(0,0,0,0.7)" }}
        >
          <View
            className="rounded-2xl p-8 items-center mx-8 shadow-xl"
            style={{ elevation: 10, backgroundColor: colors.bg }}
          >
            <ActivityIndicator size="large" color={accent} />
            <Text className="font-semibold text-lg mt-4" style={{ color: colors.textPrimary }}>
              {processingMessage}
            </Text>
            <Text className="text-sm mt-2 text-center" style={{ color: colors.textSecondary }}>
              This may take a few seconds
            </Text>
          </View>
        </View>
      </Modal>
    </>
  );
}
