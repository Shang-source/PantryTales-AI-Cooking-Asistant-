import renderer, { act } from "react-test-renderer";
import { View, TouchableOpacity } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";

// Silence animation warnings
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock lucide-react-native icons
jest.mock("lucide-react-native", () => {
  const { Text } = require("react-native");
  return {
    ChevronLeft: (props: any) => (
      <Text testID="icon-chevron-left" {...props}>
        ChevronLeft
      </Text>
    ),
    ChevronRight: (props: any) => (
      <Text testID="icon-chevron-right" {...props}>
        ChevronRight
      </Text>
    ),
  };
});

// Mock react-native-calendars
jest.mock("react-native-calendars", () => {
  const { View, Text, TouchableOpacity } = require("react-native");
  return {
    Calendar: ({
      onDayPress,
      markedDates,
      initialDate,
      renderArrow,
      theme,
      ...props
    }: any) => (
      <View testID="rnd-calendar" {...props}>
        <Text testID="calendar-initial-date">{initialDate}</Text>
        <Text testID="calendar-marked-dates">
          {JSON.stringify(markedDates)}
        </Text>
        {renderArrow && (
          <View testID="calendar-arrows">
            {renderArrow("left")}
            {renderArrow("right")}
          </View>
        )}
        <TouchableOpacity
          testID="calendar-day"
          onPress={() => onDayPress?.({ dateString: "2026-01-15" })}
        >
          <Text>15</Text>
        </TouchableOpacity>
      </View>
    ),
  };
});

import { Calendar } from "../calendar";

afterEach(() => {
  jest.clearAllMocks();
});

describe("Calendar", () => {
  it("renders calendar with selected date", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate="2026-01-10" onDayPress={jest.fn()} />
      );
    });

    const calendar = tree.root.findByProps({ testID: "rnd-calendar" });
    expect(calendar).toBeTruthy();

    const initialDate = tree.root.findByProps({
      testID: "calendar-initial-date",
    });
    expect(initialDate.props.children).toBe("2026-01-10");
  });

  it("renders calendar with today's date when no selectedDate provided", () => {
    let tree!: renderer.ReactTestRenderer;
    const today = new Date().toISOString().split("T")[0];

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate={undefined} onDayPress={jest.fn()} />
      );
    });

    const initialDate = tree.root.findByProps({
      testID: "calendar-initial-date",
    });
    expect(initialDate.props.children).toBe(today);
  });

  it("marks selected date correctly", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate="2026-01-10" onDayPress={jest.fn()} />
      );
    });

    const markedDates = tree.root.findByProps({
      testID: "calendar-marked-dates",
    });
    const parsed = JSON.parse(markedDates.props.children);

    expect(parsed["2026-01-10"]).toBeDefined();
    expect(parsed["2026-01-10"].selected).toBe(true);
    expect(parsed["2026-01-10"].selectedColor).toBe("#030712");
    expect(parsed["2026-01-10"].selectedTextColor).toBe("#f9fafb");
  });

  it("has empty marked dates when no date is selected", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate={undefined} onDayPress={jest.fn()} />
      );
    });

    const markedDates = tree.root.findByProps({
      testID: "calendar-marked-dates",
    });
    const parsed = JSON.parse(markedDates.props.children);

    expect(Object.keys(parsed).length).toBe(0);
  });

  it("calls onDayPress when a day is pressed", () => {
    let tree!: renderer.ReactTestRenderer;
    const mockOnDayPress = jest.fn();

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate="2026-01-10" onDayPress={mockOnDayPress} />
      );
    });

    const dayButton = tree.root.findByProps({ testID: "calendar-day" });

    act(() => {
      dayButton.props.onPress();
    });

    expect(mockOnDayPress).toHaveBeenCalledWith("2026-01-15");
  });

  it("renders navigation arrows", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate="2026-01-10" onDayPress={jest.fn()} />
      );
    });

    const arrowsContainer = tree.root.findByProps({ testID: "calendar-arrows" });
    expect(arrowsContainer).toBeTruthy();

    // Find TouchableOpacity components (arrow buttons)
    const touchables = arrowsContainer.findAllByType(TouchableOpacity);
    expect(touchables.length).toBe(2);
  });

  it("renders chevron icons in arrows", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate="2026-01-10" onDayPress={jest.fn()} />
      );
    });

    const leftIcon = tree.root.findByProps({ testID: "icon-chevron-left" });
    const rightIcon = tree.root.findByProps({ testID: "icon-chevron-right" });

    expect(leftIcon).toBeTruthy();
    expect(rightIcon).toBeTruthy();
  });

  it("applies custom className", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Calendar
          selectedDate="2026-01-10"
          onDayPress={jest.fn()}
          className="my-custom-class"
        />
      );
    });

    // Find the outer View wrapper
    const views = tree.root.findAllByType(View);
    const outerView = views.find(
      (v) => v.props.className && v.props.className.includes("my-custom-class")
    );

    expect(outerView).toBeTruthy();
  });

  it("has correct wrapper styling", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Calendar selectedDate="2026-01-10" onDayPress={jest.fn()} />
      );
    });

    // Find the outer View wrapper with base styling
    const views = tree.root.findAllByType(View);
    const styledView = views.find(
      (v) =>
        v.props.className &&
        v.props.className.includes("rounded-lg") &&
        v.props.className.includes("bg-white")
    );

    expect(styledView).toBeTruthy();
  });
});
