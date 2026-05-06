import renderer, { act } from "react-test-renderer";
import { Text, TouchableOpacity } from "react-native";
import { jest, describe, it, expect, beforeEach } from "@jest/globals";
import { UnitSelectorModal } from "../UnitSelectorModal";

// Mock the theme context
jest.mock("@/contexts/ThemeContext", () => ({
  useTheme: () => ({
    colors: {
      card: "#ffffff",
      border: "#e5e5e5",
      bg: "#f5f5f5",
      accent: "#3b82f6",
      textPrimary: "#171717",
      textSecondary: "#737373",
      textMuted: "#a3a3a3",
    },
  }),
}));

// Silence RN animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("UnitSelectorModal", () => {
  const mockOnSelect = jest.fn();
  const mockOnClose = jest.fn();

  beforeEach(() => {
    mockOnSelect.mockClear();
    mockOnClose.mockClear();
  });

  it("renders nothing when not visible", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <UnitSelectorModal
          visible={false}
          onSelect={mockOnSelect}
          onClose={mockOnClose}
        />
      );
    });

    // Modal should not render content when not visible
    const root = tree.root;
    expect(root.findAllByType(Text).length).toBe(0);
  });

  it("renders common units when visible", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <UnitSelectorModal
          visible={true}
          onSelect={mockOnSelect}
          onClose={mockOnClose}
        />
      );
    });

    const texts = tree.root.findAllByType(Text);
    const unitTexts = texts.map((t) => t.props.children);

    // Check that common units are rendered
    expect(unitTexts).toContain("cups");
    expect(unitTexts).toContain("tbsp");
    expect(unitTexts).toContain("tsp");
    expect(unitTexts).toContain("g");
    expect(unitTexts).toContain("kg");
    expect(unitTexts).toContain("ml");
    expect(unitTexts).toContain("pcs");
  });

  it("shows Select Unit title", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <UnitSelectorModal
          visible={true}
          onSelect={mockOnSelect}
          onClose={mockOnClose}
        />
      );
    });

    const texts = tree.root.findAllByType(Text);
    const hasTitle = texts.some((t) => t.props.children === "Select Unit");
    expect(hasTitle).toBe(true);
  });

  it("shows custom unit input section", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <UnitSelectorModal
          visible={true}
          onSelect={mockOnSelect}
          onClose={mockOnClose}
        />
      );
    });

    const texts = tree.root.findAllByType(Text);
    const hasCustomLabel = texts.some(
      (t) => t.props.children === "Or enter custom unit:"
    );
    expect(hasCustomLabel).toBe(true);

    const hasSetButton = texts.some((t) => t.props.children === "Set");
    expect(hasSetButton).toBe(true);
  });

  it("renders with currentUnit prop without crashing", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <UnitSelectorModal
          visible={true}
          currentUnit="cups"
          onSelect={mockOnSelect}
          onClose={mockOnClose}
        />
      );
    });

    // Verify component renders
    expect(tree.root).toBeDefined();

    // Verify "cups" is in the rendered content
    const texts = tree.root.findAllByType(Text);
    const hasCups = texts.some((t) => t.props.children === "cups");
    expect(hasCups).toBe(true);
  });

  it("has touchable elements for unit selection", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <UnitSelectorModal
          visible={true}
          onSelect={mockOnSelect}
          onClose={mockOnClose}
        />
      );
    });

    // Verify there are TouchableOpacity elements (for units and backdrop)
    const touchables = tree.root.findAllByType(TouchableOpacity);
    expect(touchables.length).toBeGreaterThan(1);

    // Verify onPress handlers exist
    const touchablesWithOnPress = touchables.filter((t) => t.props.onPress);
    expect(touchablesWithOnPress.length).toBeGreaterThan(0);
  });

  it("calls onClose when backdrop is pressed", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <UnitSelectorModal
          visible={true}
          onSelect={mockOnSelect}
          onClose={mockOnClose}
        />
      );
    });

    // Find the backdrop TouchableOpacity (the first one with flex-1 class)
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const backdrop = touchables.find(
      (t) => t.props.className?.includes("flex-1") && t.props.className?.includes("bg-black")
    );

    act(() => {
      backdrop?.props.onPress();
    });

    expect(mockOnClose).toHaveBeenCalled();
  });
});
