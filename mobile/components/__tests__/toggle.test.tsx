import renderer, { act } from "react-test-renderer";
import { TextInput, Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import { Toggle } from "../toggle";

// Silence React Native animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Toggle", () => {
  it("renders with default styles and wraps string children in Text", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Toggle>Label</Toggle>);
    });

    const btn = tree.root.findByProps({ "data-slot": "toggle" });
    expect(btn.props.className).toContain("flex flex-row items-center justify-center");

    const text = btn.findByType(Text);
    expect(text.props.children).toBe("Label");
    expect(text.props.className).toContain("text-sm font-medium");
  });

  it("toggles pressed state in uncontrolled mode and updates hidden input", () => {
    const onPressedChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Toggle defaultPressed onPressedChange={onPressedChange}>
          A
        </Toggle>
      );
    });

    const btn = tree.root.findByProps({ "data-slot": "toggle" });
    expect(btn.props.className).toContain("bg-accent");

    const hiddenInput = tree.root.findByType(TextInput);
    expect(hiddenInput.props.value).toBe("on");

    act(() => {
      btn.props.onPress?.({} as any);
    });

    expect(onPressedChange).toHaveBeenLastCalledWith(false);
    const updated = tree.root.findByProps({ "data-slot": "toggle" });
    expect(updated.props.className).not.toContain("bg-accent");
    expect(tree.root.findByType(TextInput).props.value).toBe("off");
  });

  it("respects controlled pressed prop and does not change className internally", () => {
    const onPressedChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Toggle pressed={false} onPressedChange={onPressedChange}>
          A
        </Toggle>
      );
    });

    const btn = tree.root.findByProps({ "data-slot": "toggle" });
    expect(btn.props.className).not.toContain("bg-accent");

    act(() => {
      btn.props.onPress?.({} as any);
    });

    expect(onPressedChange).toHaveBeenLastCalledWith(true);
    // Still unpressed because controlled prop didn't change
    expect(tree.root.findByProps({ "data-slot": "toggle" }).props.className).not.toContain(
      "bg-accent"
    );
    expect(tree.root.findByType(TextInput).props.value).toBe("off");
  });

  it("does nothing when disabled", () => {
    const onPressedChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Toggle defaultPressed disabled onPressedChange={onPressedChange}>
          A
        </Toggle>
      );
    });

    const btn = tree.root.findByProps({ "data-slot": "toggle" });
    expect(btn.props.className).toContain("opacity-50");

    act(() => {
      btn.props.onPress?.({} as any);
    });

    expect(onPressedChange).not.toHaveBeenCalled();
    expect(tree.root.findByType(TextInput).props.value).toBe("on");
  });
});
