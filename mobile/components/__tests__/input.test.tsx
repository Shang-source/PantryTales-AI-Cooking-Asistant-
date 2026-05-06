import React from "react";
import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect } from "@jest/globals";

import { Input } from "../input";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Input", () => {
  it("merges className, style, and passes props through", () => {
    const onChangeText = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Input
          className="extra"
          placeholder="Your name"
          style={{ borderColor: "red" }}
          onChangeText={onChangeText}
          value=""
        />,
      );
    });

    const input = tree.root.findByProps({ "data-slot": "input" });

    // Colors are now applied via style prop using theme colors
    expect(input.props.className).toContain("extra");
    expect(input.props.placeholder).toBe("Your name");

    const [baseStyle, customStyle] = input.props.style;
    expect(baseStyle).toMatchObject({
      borderWidth: 1,
      paddingHorizontal: 12,
    });
    expect(customStyle).toMatchObject({ borderColor: "red" });

    act(() => {
      input.props.onChangeText("Alice");
    });
    expect(onChangeText).toHaveBeenCalledWith("Alice");
  });

  it("supports disabled state via editable prop", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Input editable={false} value="" />);
    });

    const input = tree.root.findByProps({ "data-slot": "input" });
    expect(input.props.editable).toBe(false);
    expect(input.props.className).toContain("disabled:opacity-50");
  });
});

