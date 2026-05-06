import React, { useState } from "react";
import { View, Text, TouchableOpacity } from "react-native";
import { ChevronDown, ChevronUp } from "lucide-react-native";
import Svg, { Circle } from "react-native-svg";
import { useTheme } from "@/contexts/ThemeContext";

interface NutritionData {
  calories?: number | null;
  carbohydrates?: number | null;
  fat?: number | null;
  protein?: number | null;
  sugar?: number | null;
  sodium?: number | null;
  saturatedFat?: number | null;
  servings?: number | null;
}

interface NutritionSectionProps {
  nutrition: NutritionData;
}

// Colors for the nutrients - based on the theme
const COLORS = {
  calories: "#F5A623",
  carbs: "#5B9BD5",
  fat: "#FFD700",
  protein: "#D4A5A5",
  sugar: "#FF6B6B",
  sodium: "#A5D4A5",
  saturatedFat: "#FFB366",
};

// Daily values for calculation (approximate)
const DAILY_VALUES = {
  calories: 2000,
  carbs: 300, // grams
  fat: 65, // grams
  protein: 50, // grams
  sugar: 50, // grams
  sodium: 2300, // mg
  saturatedFat: 20, // grams
};

// Progress bar component
function NutritionRow({
  label,
  value,
  unit,
  percentage,
  color,
  isSubItem = false,
  colors,
}: {
  label: string;
  value: number | string;
  unit: string;
  percentage?: number;
  color: string;
  isSubItem?: boolean;
  colors: any;
}) {
  const displayPercentage =
    percentage !== undefined
      ? Math.min(100, Math.round(percentage))
      : undefined;

  return (
    <View className={`py-2 ${isSubItem ? "pl-4" : ""}`}>
      <View className="flex-row justify-between items-center mb-1">
        <Text className={isSubItem ? "text-sm" : "font-medium"} style={{ color: colors.textPrimary }}>
          {label}
        </Text>
        <Text className="text-sm" style={{ color: colors.textSecondary }}>
          {value} {unit}
          {displayPercentage !== undefined ? ` (${displayPercentage}%)` : ""}
        </Text>
      </View>
      {displayPercentage !== undefined && (
        <View className="h-1.5 rounded-full overflow-hidden" style={{ backgroundColor: colors.border }}>
          <View
            className="h-full rounded-full"
            style={{
              width: `${Math.min(100, displayPercentage)}%`,
              backgroundColor: color,
            }}
          />
        </View>
      )}
    </View>
  );
}

// Circular progress for calories
function CalorieCircle({ calories, colors }: { calories: number; colors: any }) {
  const size = 80;
  const strokeWidth = 8;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const percentage = Math.min(100, (calories / DAILY_VALUES.calories) * 100);
  const strokeDashoffset = circumference - (percentage / 100) * circumference;

  return (
    <View className="items-center justify-center">
      <Svg width={size} height={size}>
        {/* Background circle */}
        <Circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={colors.border}
          strokeWidth={strokeWidth}
          fill="transparent"
        />
        {/* Progress circle */}
        <Circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={COLORS.calories}
          strokeWidth={strokeWidth}
          fill="transparent"
          strokeDasharray={`${circumference}`}
          strokeDashoffset={strokeDashoffset}
          strokeLinecap="round"
          rotation="-90"
          origin={`${size / 2}, ${size / 2}`}
        />
      </Svg>
      <View className="absolute items-center">
        <Text className="text-xl font-bold" style={{ color: colors.textPrimary }}>
          {Math.round(calories)}
        </Text>
        <Text className="text-xs" style={{ color: colors.textMuted }}>{`cals`}</Text>
      </View>
    </View>
  );
}

// Macro summary item
function MacroItem({
  value,
  label,
  percentage,
  color,
  colors,
}: {
  value: number;
  label: string;
  percentage: number;
  color: string;
  colors: any;
}) {
  return (
    <View className="items-center flex-1">
      <Text className="font-bold text-lg" style={{ color: colors.textPrimary }}>{Math.round(value)}g</Text>
      <Text className="text-xs" style={{ color: colors.textMuted }}>{label}</Text>
      <Text style={{ color }} className="text-xs font-medium">
        {Math.round(percentage)}% cals
      </Text>
    </View>
  );
}

export function NutritionSection({ nutrition }: NutritionSectionProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const { colors } = useTheme();

  const hasNutrition =
    nutrition.calories !== null &&
    nutrition.calories !== undefined &&
    nutrition.calories > 0;

  if (!hasNutrition) {
    return null;
  }

  const calories = nutrition.calories ?? 0;
  const carbs = nutrition.carbohydrates ?? 0;
  const fat = nutrition.fat ?? 0;
  const protein = nutrition.protein ?? 0;
  const sugar = nutrition.sugar ?? 0;
  const sodium = nutrition.sodium ?? 0;
  const saturatedFat = nutrition.saturatedFat ?? 0;
  const servings = nutrition.servings ?? 1;

  // Calculate calorie percentages from macros
  // Note: The values in the database are % daily value, but for display we show them as-is
  // Carbs: 4 cal/g, Protein: 4 cal/g, Fat: 9 cal/g
  const carbCals = carbs * 4;
  const proteinCals = protein * 4;
  const fatCals = fat * 9;
  const totalMacroCals = carbCals + proteinCals + fatCals;

  const carbPercentage =
    totalMacroCals > 0 ? (carbCals / totalMacroCals) * 100 : 0;
  const proteinPercentage =
    totalMacroCals > 0 ? (proteinCals / totalMacroCals) * 100 : 0;
  const fatPercentage =
    totalMacroCals > 0 ? (fatCals / totalMacroCals) * 100 : 0;

  return (
    <View className="rounded-2xl p-4 mt-4" style={{ backgroundColor: colors.card }}>
      {/* Header with macros summary - always visible */}
      <View className="flex-row items-center">
        <CalorieCircle calories={calories} colors={colors} />
        <View className="flex-1 flex-row ml-4">
          <MacroItem
            value={carbs}
            label="Carbs"
            percentage={carbPercentage}
            color={COLORS.carbs}
            colors={colors}
          />
          <MacroItem
            value={fat}
            label="Total fat"
            percentage={fatPercentage}
            color={COLORS.fat}
            colors={colors}
          />
          <MacroItem
            value={protein}
            label="Protein"
            percentage={proteinPercentage}
            color={COLORS.protein}
            colors={colors}
          />
        </View>
      </View>

      {/* Collapsible daily RDA section */}
      <TouchableOpacity
        onPress={() => setIsExpanded(!isExpanded)}
        className="flex-row items-center justify-between py-2 mt-4 border-t"
        style={{ borderTopColor: colors.border }}
      >
        <Text className="font-medium" style={{ color: colors.textPrimary }}>Daily RDA Nutrition</Text>
        <View className="flex-row items-center">
          {isExpanded ? (
            <ChevronUp size={20} color={colors.textMuted} />
          ) : (
            <ChevronDown size={20} color={colors.textMuted} />
          )}
        </View>
      </TouchableOpacity>

      {isExpanded && (
        <View className="pt-2">
          <NutritionRow
            label="Servings"
            value={servings}
            unit=""
            color={COLORS.calories}
            colors={colors}
          />
          <NutritionRow
            label="Calories"
            value={Math.round(calories)}
            unit="kcal"
            percentage={(calories / DAILY_VALUES.calories) * 100}
            color={COLORS.calories}
            colors={colors}
          />
          <NutritionRow
            label="Carbs"
            value={Math.round(carbs)}
            unit="g"
            percentage={carbs}
            color={COLORS.carbs}
            colors={colors}
          />
          <NutritionRow
            label="Protein"
            value={Math.round(protein)}
            unit="g"
            percentage={protein}
            color={COLORS.protein}
            colors={colors}
          />
          <NutritionRow
            label="Total fat"
            value={Math.round(fat)}
            unit="g"
            percentage={fat}
            color={COLORS.fat}
            colors={colors}
          />
          <NutritionRow
            label="Saturated fat"
            value={Math.round(saturatedFat)}
            unit="g"
            percentage={saturatedFat}
            color={COLORS.saturatedFat}
            isSubItem
            colors={colors}
          />
          <NutritionRow
            label="Sugars"
            value={Math.round(sugar)}
            unit="g"
            percentage={sugar}
            color={COLORS.sugar}
            colors={colors}
          />
          <NutritionRow
            label="Sodium"
            value={Math.round(sodium)}
            unit="mg"
            percentage={sodium}
            color={COLORS.sodium}
            colors={colors}
          />
        </View>
      )}
    </View>
  );
}

export default NutritionSection;
