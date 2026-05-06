import React from "react";
import renderer, { act } from "react-test-renderer";
import { Text, TextInput, View } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import {
  InputOTP,
  InputOTPGroup,
  InputOTPSlot,
  InputOTPSeparator,
} from "../input-otp";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("InputOTP", () => {
  it("renders slots and separator based on the current value", () => {
    const onChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <InputOTP value="12" onChange={onChange} maxLength={4}>
          <InputOTPGroup>
            <InputOTPSlot index={0} />
            <InputOTPSlot index={1} />
          </InputOTPGroup>
          <InputOTPSeparator />
          <InputOTPGroup>
            <InputOTPSlot index={2} />
            <InputOTPSlot index={3} />
          </InputOTPGroup>
        </InputOTP>,
      );
    });

    const slotComponents = tree.root.findAll((node) => node.type === InputOTPSlot);
    expect(slotComponents).toHaveLength(4);

    const slotChars = slotComponents.map((slot) => slot.findByType(Text).props.children);
    expect(slotChars).toEqual(["1", "2", "", ""]);

    const separator = tree.root.findByType(InputOTPSeparator).findByType(Text);
    expect(separator.props.children).toBe("-");

    const thirdSlotView = slotComponents[2].findByType(View);
    const fourthSlotView = slotComponents[3].findByType(View);

    expect(thirdSlotView.props.style[1]).toBeTruthy();
    expect(fourthSlotView.props.style[1]).toBeFalsy();
  });

  it("clamps user input to maxLength before calling onChange", () => {
    const onChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <InputOTP value="" onChange={onChange} maxLength={4}>
          <InputOTPSlot index={0} />
          <InputOTPSlot index={1} />
          <InputOTPSlot index={2} />
          <InputOTPSlot index={3} />
        </InputOTP>,
      );
    });

    const hiddenInput = tree.root.findByType(TextInput);

    act(() => {
      hiddenInput.props.onChangeText("987654");
    });

    expect(onChange).toHaveBeenCalledWith("9876");
  });
});

