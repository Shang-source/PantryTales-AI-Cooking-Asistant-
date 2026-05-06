import renderer, { act } from "react-test-renderer";
import { View } from "react-native";
import { Skeleton } from "../skeleton";
import { jest, describe, it, expect } from "@jest/globals";

// Mock NativeAnimatedHelper to avoid React Native internals failing in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Skeleton", () => {
  it("renders with default skeleton styles", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(<Skeleton />);
    });

    const skeleton = tree.root.findByType(View);

    // Component uses theme colors via style prop instead of className for background
    expect(skeleton.props.className).toContain("mb-2");
    expect(skeleton.props.className).toContain("opacity-70");
    expect(skeleton.props.style).toBeDefined();
  });

  it("merges custom className and forwards extra props", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Skeleton
          className="w-full h-4 rounded-md"
          testID="skeleton"
          accessibilityRole="progressbar"
        />
      );
    });

    const skeleton = tree.root.findByProps({ testID: "skeleton" });

    expect(skeleton.props.className).toContain("w-full");
    expect(skeleton.props.className).toContain("h-4");
    expect(skeleton.props.className).toContain("rounded-md");
    expect(skeleton.props.accessibilityRole).toBe("progressbar");
  });
});
