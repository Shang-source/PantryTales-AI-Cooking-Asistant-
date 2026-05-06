import { View, TouchableOpacity } from "react-native";
import { Calendar as RNDCalendar, DateData } from "react-native-calendars";
import { ChevronLeft, ChevronRight } from "lucide-react-native";
import { cn } from "@/utils/cn";

const THEME_COLORS = {
  primary: "#030712",
  primaryForeground: "#f9fafb",
  background: "#ffffff",
  foreground: "#030712",
  secondary: "#f3f4f6",
  accent: "#e5e7eb",
  accentForeground: "#1f2937",
  mutedForeground: "#6b7280",
  border: "#374151",
};

interface CalendarProps {
  selectedDate: string | undefined;
  onDayPress: (dateString: string) => void;
  className?: string;
}

const Calendar = ({ selectedDate, onDayPress, className }: CalendarProps) => {
  const markedDates = selectedDate
    ? {
        [selectedDate]: {
          selected: true,
          selectedColor: THEME_COLORS.primary,
          selectedTextColor: THEME_COLORS.primaryForeground,
        },
      }
    : {};

  return (
    <View
      className={cn(
        "p-3 rounded-lg bg-white shadow-sm border border-gray-100",
        className
      )}
    >
      <RNDCalendar
        className="w-full"
        initialDate={selectedDate || new Date().toISOString().split("T")[0]}
        markedDates={markedDates}
        onDayPress={(day: DateData) => onDayPress(day.dateString)}
        theme={{
          calendarBackground: THEME_COLORS.background,

          textMonthFontWeight: "500",
          textMonthFontSize: 14,

          arrowColor: THEME_COLORS.foreground,

          textDayHeaderFontWeight: "normal",
          textDayHeaderFontSize: 12,

          selectedDayBackgroundColor: THEME_COLORS.primary,
          selectedDayTextColor: THEME_COLORS.primaryForeground,

          todayBackgroundColor: THEME_COLORS.accent,
          todayTextColor: THEME_COLORS.accentForeground,

          textDisabledColor: THEME_COLORS.mutedForeground + "80",

          dotColor: "transparent",
          textSectionTitleColor: THEME_COLORS.mutedForeground,
        }}
        renderArrow={(direction: "left" | "right") => {
          const Icon = direction === "left" ? ChevronLeft : ChevronRight;
          return (
            <TouchableOpacity className="h-7 w-7 items-center justify-center rounded-md border border-[#374151]/20 opacity-50 active:opacity-100 active:bg-gray-50">
              <Icon size={16} color={THEME_COLORS.foreground} />
            </TouchableOpacity>
          );
        }}
      />
    </View>
  );
};

export { Calendar };
