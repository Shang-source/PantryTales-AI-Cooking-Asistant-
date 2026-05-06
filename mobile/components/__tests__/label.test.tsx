import React from "react";
import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect } from "@jest/globals";

import { Label } from "../label";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Label", () => {
  it("merges default styles with custom className and forwards props", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Label accessibilityRole="text" className="extra" testID="label">
          Hello
        </Label>,
      );
    });

    const label = tree.root.findByProps({ "data-slot": "label" });

    expect(label.props.className).toContain("flex items-center gap-2");
    expect(label.props.className).toContain("extra");
    expect(label.props.accessibilityRole).toBe("text");
    expect(label.props.children).toBe("Hello");
  });
});

