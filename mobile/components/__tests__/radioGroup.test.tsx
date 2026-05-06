import renderer, { act } from "react-test-renderer";
import { describe, it, expect, jest } from "@jest/globals";
import { TouchableOpacity } from "react-native";
import { RadioGroup, RadioGroupItem } from "../radioGroup";

// Silence native animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock lucide icon
jest.mock("lucide-react-native", () => ({
  Circle: "Circle",
}));

describe("RadioGroup", () => {
  it("renders items with correct checked state", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <RadioGroup value="b" onValueChange={jest.fn()}>
          <RadioGroupItem value="a" />
          <RadioGroupItem value="b" />
        </RadioGroup>
      );
    });

    const radios = tree.root.findAllByType(TouchableOpacity);
    expect(radios.length).toBe(2);
    expect(radios[0].props.accessibilityState.checked).toBe(false);
    expect(radios[1].props.accessibilityState.checked).toBe(true);
  });

  it("invokes onValueChange when an item is pressed", () => {
    const onChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <RadioGroup value="a" onValueChange={onChange}>
          <RadioGroupItem value="a" />
          <RadioGroupItem value="b" />
        </RadioGroup>
      );
    });

    const radios = tree.root.findAllByType(TouchableOpacity);
    act(() => {
      radios[1].props.onPress();
    });

    expect(onChange).toHaveBeenCalledWith("b");
  });

  it("applies disabled state", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <RadioGroup value="x" onValueChange={jest.fn()}>
          <RadioGroupItem value="x" disabled />
        </RadioGroup>
      );
    });

    const radio = tree.root.findByType(TouchableOpacity);
    expect(radio.props.disabled).toBe(true);
    expect(radio.props.accessibilityState).toMatchObject({
      checked: true,
      disabled: true,
    });
  });
});
