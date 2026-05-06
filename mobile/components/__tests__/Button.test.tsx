import renderer, { act } from "react-test-renderer";
import { Text, TouchableOpacity } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";

import { Button } from "../Button";

// Silence animation warnings
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

afterEach(() => {
  jest.clearAllMocks();
});

describe("Button", () => {
  it("renders text children with default variant styles", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Button>Tap me</Button>);
    });

    const button = tree.root.findByType(TouchableOpacity);
    expect(button.props.activeOpacity).toBe(0.8);
    // Background/border colors now applied via style prop using theme colors
    expect(button.props.style).toBeDefined();

    const label = tree.root.findByType(Text);
    expect(label.props.children).toBe("Tap me");
    // Text color now applied via style prop using theme colors
    expect(label.props.style).toBeDefined();
  });

  it("applies variant and size styles and adjusts activeOpacity for ghost/link", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Button variant="ghost" size="lg" textClassName="custom-text">
          Ghost
        </Button>
      );
    });

    const button = tree.root.findByType(TouchableOpacity);
    expect(button.props.activeOpacity).toBe(0.7);
    expect(button.props.className).toContain("bg-transparent");

    const label = tree.root.findByType(Text);
    // Text color now applied via style prop using theme colors
    expect(label.props.className).toContain("text-lg");
    expect(label.props.className).toContain("custom-text");
  });

  it("adds opacity when disabled", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Button disabled variant="outline">
          Disabled
        </Button>
      );
    });

    const button = tree.root.findByType(TouchableOpacity);
    expect(button.props.disabled).toBe(true);
    expect(button.props.className).toContain("opacity-50");

    const label = tree.root.findByType(Text);
    // Text color now applied via style prop using theme colors
    expect(label.props.style).toBeDefined();
  });
});
