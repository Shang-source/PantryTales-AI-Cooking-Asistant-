import renderer, { act } from "react-test-renderer";
import { View } from "react-native";
import StatCard from "../ui/StatCard";
import { jest, describe, it, expect } from "@jest/globals";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("react-native-vector-icons/Feather", () => {
  const { View } = require("react-native");
  return (props: any) => <View testID={`icon-${props.name}`} {...props} />;
});

describe("StatCard", () => {
  it("renders value and label with default styles", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<StatCard value={42} label="Calories" />);
    });

    const card = tree.root.findByType(View);
    const valueText = tree.root.findByProps({ children: 42 });
    const labelText = tree.root.findByProps({ children: "Calories" });

    expect(card.props.className).toContain(
      "rounded-xl p-3 items-center justify-center",
    );
    // Colors are now applied via style prop using theme colors
    expect(valueText.props.className).toContain("text-xl font-semibold");
    expect(valueText.props.style).toBeDefined();
    expect(labelText.props.className).toContain("text-sm");
    expect(labelText.props.style).toBeDefined();
  });

  it("applies custom className props", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <StatCard
          value={100}
          label="Protein"
          className="bg-red-500"
          valueClassName="text-green-500"
          labelClassName="text-blue-500"
        />,
      );
    });

    const card = tree.root.findByType(View);
    const valueText = tree.root.findByProps({ children: 100 });
    const labelText = tree.root.findByProps({ children: "Protein" });

    expect(card.props.className).toContain("bg-red-500");
    expect(valueText.props.className).toContain("text-green-500");
    expect(labelText.props.className).toContain("text-blue-500");
  });

  it("passes extra props to the container View", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <StatCard
          value={5}
          label="Fat"
          testID="stat-card"
          accessibilityLabel="stat-container"
        />,
      );
    });
    const card = tree.root.findByProps({ testID: "stat-card" });
    expect(card.props.accessibilityLabel).toBe("stat-container");
  });

  describe("compact mode", () => {
    it("renders horizontal layout with compact prop", () => {
      let tree!: renderer.ReactTestRenderer;
      act(() => {
        tree = renderer.create(<StatCard value={10} label="Total" compact />);
      });

      const card = tree.root.findByType(View);
      expect(card.props.className).toContain("flex-row items-center justify-between");
    });

    it("renders icon when icon prop is provided in compact mode", () => {
      let tree!: renderer.ReactTestRenderer;
      act(() => {
        tree = renderer.create(
          <StatCard value={5} label="Items" compact icon="package" iconColor="#D4A5A5" />,
        );
      });

      const icon = tree.root.findByProps({ testID: "icon-package" });
      expect(icon.props.name).toBe("package");
      expect(icon.props.color).toBe("#D4A5A5");
      expect(icon.props.size).toBe(14);
    });

    it("does not render icon when icon prop is not provided in compact mode", () => {
      let tree!: renderer.ReactTestRenderer;
      act(() => {
        tree = renderer.create(<StatCard value={3} label="Count" compact />);
      });

      // Check that no icon component exists by looking for any element with testID starting with "icon-"
      const allElements = tree.root.findAll((node) =>
        node.props.testID && node.props.testID.startsWith("icon-")
      );
      expect(allElements.length).toBe(0);
    });

    it("uses default icon color when iconColor is not provided", () => {
      let tree!: renderer.ReactTestRenderer;
      act(() => {
        tree = renderer.create(
          <StatCard value={7} label="Test" compact icon="star" />,
        );
      });

      const icon = tree.root.findByProps({ testID: "icon-star" });
      // Uses theme's textMuted color
      expect(icon.props.color).toBe("rgba(255,255,255,0.6)");
    });
  });
});
