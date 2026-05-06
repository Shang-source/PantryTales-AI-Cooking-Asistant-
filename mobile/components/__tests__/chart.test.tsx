import renderer, { act } from "react-test-renderer";
import { Platform, Text } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";

import {
  ChartContainer,
  ChartTooltipContent,
  ChartLegendContent,
  type ChartConfig,
} from "../chart";

// Silence animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const baseConfig: ChartConfig = {
  protein: { label: "Protein", color: "#f87171" },
  carbs: { label: "Carbs", color: "#34d399" },
};

const renderWithContainer = (ui: React.ReactNode, config: ChartConfig = baseConfig) => {
  let tree!: renderer.ReactTestRenderer;
  act(() => {
    tree = renderer.create(<ChartContainer config={config}>{ui}</ChartContainer>);
  });
  return tree;
};

afterEach(() => {
  jest.restoreAllMocks();
});

describe("ChartTooltipContent", () => {
  it("renders null when no items are provided", () => {
    const tree = renderWithContainer(<ChartTooltipContent items={[]} />);
    const json = tree.toJSON() as any;
    expect(json?.children ?? []).toHaveLength(0);
  });

  it("renders labels, values, indicator styles, and respects Platform monospace on iOS", () => {
    const originalOS = Platform.OS;
    Object.defineProperty(Platform, "OS", { value: "ios" });

    const items = [{ label: "protein", value: 25 }];
    const tree = renderWithContainer(<ChartTooltipContent items={items} indicator="dot" />);

    const label = tree.root.findAllByType(Text).find((t) => t.props.children === "Protein");
    const value = tree.root.findAllByType(Text).find((t) => t.props.children === 25);
    const indicator = tree.root.findAll((node) => node.props.style?.backgroundColor === "#f87171")[0];

    expect(label).toBeDefined();
    expect(value?.props.className).toContain("font-[Courier]");
    expect(indicator).toBeDefined();

    Object.defineProperty(Platform, "OS", { value: originalOS });
  });

  it("omits label and indicator when hidden", () => {
    const items = [{ label: "carbs", value: 12, color: "#123456" }];
    const tree = renderWithContainer(
      <ChartTooltipContent
        items={items}
        hideLabel
        hideIndicator
        className="extra-class"
      />
    );

    const texts = tree.root.findAllByType(Text);
    expect(texts.some((t) => t.props.children === "Carbs")).toBe(false);
    const indicator = tree.root.findAll(
      (node) => node.props.style && "backgroundColor" in node.props.style
    )[0];
    expect(indicator).toBeUndefined();
    // Value text should still render
    expect(texts.some((t) => t.props.children === 12)).toBe(true);
  });
});

describe("ChartLegendContent", () => {
  it("renders legend entries for provided keys and skips missing config", () => {
    const tree = renderWithContainer(
      <ChartLegendContent keys={["protein", "missing"]} />
    );

    const labels = tree.root.findAllByType(Text).map((t) => t.props.children);
    expect(labels).toContain("Protein");
    expect(labels).not.toContain("missing");
  });
});
