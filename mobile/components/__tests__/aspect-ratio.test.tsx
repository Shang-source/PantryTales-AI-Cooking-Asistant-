import { Text, View } from "react-native";
import renderer, { act } from "react-test-renderer";
import { describe, it, expect, jest } from "@jest/globals";

import { AspectRatio } from "../aspect-ratio";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("AspectRatio", () => {
  it("renders with default ratio, base className and forwarded props", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <AspectRatio className="extra" accessibilityLabel="aspect-root" />
      );
    });
    const element = tree.root.findByType(View);

    expect(element.type).toBe(View);
    expect(element.props.className).toContain("w-full");
    expect(element.props.className).toContain("extra");
    const [ratioStyle] = element.props.style as any[];
    expect(ratioStyle.aspectRatio).toBeCloseTo(16 / 9);
    expect(element.props.accessibilityLabel).toBe("aspect-root");
  });

  it("applies custom ratio, merges style, and renders children", () => {
    const childText = "content";
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <AspectRatio
          ratio={4 / 3}
          style={{ backgroundColor: "red" }}
          testID="custom-aspect"
        >
          <Text>{childText}</Text>
        </AspectRatio>
      );
    });
    const element = tree.root.findByType(View);

    const [ratioStyle, passedStyle] = element.props.style as any[];
    expect(ratioStyle.aspectRatio).toBeCloseTo(4 / 3);
    expect(passedStyle.backgroundColor).toBe("red");

    expect(element.props.testID).toBe("custom-aspect");
    const text = element.findByType(Text);
    expect(text.props.children).toBe(childText);
  });
});
