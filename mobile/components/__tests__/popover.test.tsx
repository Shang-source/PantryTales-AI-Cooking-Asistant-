import renderer, { act } from "react-test-renderer";
import { describe, it, expect, jest } from "@jest/globals";
import { Text, TouchableOpacity } from "react-native";
import { Popover, PopoverTrigger, PopoverContent } from "../popover";

// Silence native animated warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const findText = (
  tree: renderer.ReactTestRenderer,
  value: string
): boolean => tree.root.findAllByType(Text).some((t) => t.props.children === value);

describe("Popover", () => {
  it("does not render content when closed, toggles open then closes via overlay", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Popover>
          <PopoverTrigger>
            <Text>Trigger</Text>
          </PopoverTrigger>
          <PopoverContent>Popover body</PopoverContent>
        </Popover>
      );
    });

    // Initially closed
    expect(findText(tree, "Popover body")).toBe(false);

    const trigger = tree.root.findAllByType(TouchableOpacity)[0];
    act(() => {
      trigger.props.onPress();
    });

    // Content should appear when open
    expect(findText(tree, "Popover body")).toBe(true);

    // Overlay should be the second TouchableOpacity when open
    const overlay = tree.root.findAllByType(TouchableOpacity)[1];
    act(() => {
      overlay.props.onPress();
    });

    expect(findText(tree, "Popover body")).toBe(false);
  });

  it("renders string content inside Text node", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Popover defaultOpen>
          <PopoverTrigger>
            <Text>Trigger</Text>
          </PopoverTrigger>
          <PopoverContent>Simple text</PopoverContent>
        </Popover>
      );
    });

    expect(findText(tree, "Simple text")).toBe(true);
  });

  it("calls onOpenChange in controlled mode without closing UI", () => {
    const onChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Popover open={true} onOpenChange={onChange}>
          <PopoverTrigger>
            <Text>Trigger</Text>
          </PopoverTrigger>
          <PopoverContent>Controlled</PopoverContent>
        </Popover>
      );
    });

    const trigger = tree.root.findAllByType(TouchableOpacity)[0];
    act(() => {
      trigger.props.onPress();
    });
    expect(onChange).toHaveBeenCalledWith(false);

    // UI stays open because `open` prop is controlled and true
    expect(findText(tree, "Controlled")).toBe(true);
  });
});
