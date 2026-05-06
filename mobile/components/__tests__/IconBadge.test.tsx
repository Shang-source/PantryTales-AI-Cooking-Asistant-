import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect } from "@jest/globals";
import { Text } from "react-native";

import { IconBadge } from "../IconBadge";

// Silence animation warnings
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock vector icons to simple Text components so we can inspect props
jest.mock("@expo/vector-icons", () => {
  const { Text } = require("react-native");
  const make = (displayName: string) => {
    const Comp = ({ children, ...props }: any) => <Text {...props}>{children}</Text>;
    Comp.displayName = displayName;
    return Comp;
  };

  return {
    Ionicons: make("Ionicons"),
    MaterialCommunityIcons: make("MaterialCommunityIcons"),
  };
});

describe("IconBadge", () => {
  it("renders Ionicons with custom color/size and merges className", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <IconBadge iconSet="Ionicons" iconName="home" iconColor="red" iconSize={20} className="extra">
          Hello
        </IconBadge>,
      );
    });

    const texts = tree.root.findAllByType(Text);
    const icon = texts.find((t) => t.props.name === "home");
    const label = texts.find((t) => t.props["data-slot"] === "label");

    expect(icon?.props.color).toBe("red");
    expect(icon?.props.size).toBe(20);
    // Text color is now applied via style prop using theme colors
    expect(label?.props.style).toBeDefined();
    expect(label?.props.className).toContain("text-xs");
    expect(label?.props.className).toContain("ml-1.5");
    expect(label?.props.children).toBe("Hello");
  });

  it("renders MaterialCommunityIcons with defaults", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <IconBadge iconSet="MaterialCommunityIcons" iconName="bell">
          Beep
        </IconBadge>,
      );
    });

    const texts = tree.root.findAllByType(Text);
    const icon = texts.find((t) => t.props.name === "bell");
    const label = texts.find((t) => t.props["data-slot"] === "label");

    // Icon color now uses theme's textPrimary color
    expect(icon?.props.color).toBe("#ffffff");
    expect(icon?.props.size).toBe(16);
    expect(label?.props.children).toBe("Beep");
  });
});
