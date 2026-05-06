import renderer, { act } from "react-test-renderer";
import { Text, TextInput, TouchableOpacity } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import { Textarea } from "../textarea";

// Silence React Native animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Textarea", () => {
  it("renders label, helper text, and applies base classes", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Textarea
          label="Notes"
          helperText="Helper"
          containerClassName="container-extra"
          className="input-extra"
          defaultValue="hello"
        />
      );
    });

    const label = tree.root.findAllByType(Text).find((node) => node.props.children === "Notes");
    expect(label).toBeDefined();

    const helper = tree.root.findAllByType(Text).find((node) => node.props.children === "Helper");
    expect(helper).toBeDefined();

    const input = tree.root.findByType(TextInput);
    const container = input.parent;
    const outer = tree.root.findAllByType(Text)[0]?.parent;

    expect(container?.props.className).toContain("flex w-full min-h-16");
    expect(input.props.className).toContain("flex-1 min-h-16 text-base leading-relaxed");
    expect(input.props.className).toContain("input-extra");
    expect(outer?.props.className).toContain("w-full");
    expect(outer?.props.className).toContain("container-extra");
  });

  it("shows clear button in uncontrolled mode and clears value/state", () => {
    const onChangeText = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Textarea
          showClearButton
          defaultValue="abc"
          onChangeText={onChangeText}
          accessibilityLabel="textarea"
        />
      );
    });

    const button = tree.root.findByType(TouchableOpacity);
    expect(button.props.accessibilityLabel).toBe("Clear text");

    act(() => {
      button.props.onPress();
    });

    // After clearing, button disappears (value now empty) and onChangeText is called.
    expect(tree.root.findAllByType(TouchableOpacity).length).toBe(0);
    expect(onChangeText).toHaveBeenLastCalledWith("");
    const input = tree.root.findByType(TextInput);
    expect(input.props.value).toBe("");
  });

  it("in controlled mode does not change value but still fires onChangeText on clear", () => {
    const onChangeText = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Textarea
          showClearButton
          value="controlled"
          onChangeText={onChangeText}
          editable
        />
      );
    });

    const button = tree.root.findByType(TouchableOpacity);
    const input = tree.root.findByType(TextInput);
    expect(input.props.value).toBe("controlled");

    act(() => {
      button.props.onPress();
    });

    // Controlled: value remains, but callback receives empty string.
    expect(onChangeText).toHaveBeenLastCalledWith("");
    expect(tree.root.findByType(TextInput).props.value).toBe("controlled");
    expect(tree.root.findAllByType(TouchableOpacity).length).toBe(1);
  });
});
