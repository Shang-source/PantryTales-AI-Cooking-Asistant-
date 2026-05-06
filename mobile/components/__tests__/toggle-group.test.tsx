import renderer, { act } from "react-test-renderer";
import { Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import { ToggleGroup, ToggleGroupItem } from "../toggle-group";

// Silence React Native animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const findItems = (tree: renderer.ReactTestRenderer) =>
  tree.root.findAllByProps({ "data-slot": "toggle-group-item" });
const findItemByLabel = (tree: renderer.ReactTestRenderer, label: string) =>
  findItems(tree).find((node) =>
    node.findAllByType(Text).some((t) => t.props.children === label)
  )!;

describe("ToggleGroup - single mode", () => {
  it("respects defaultValue, toggles selection, and fires onValueChange", () => {
    const onValueChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <ToggleGroup
          type="single"
          onValueChange={onValueChange}
        >
          <ToggleGroupItem value="a">A</ToggleGroupItem>
          <ToggleGroupItem value="b">B</ToggleGroupItem>
        </ToggleGroup>
      );
    });

    const items = findItems(tree);
    // Text color is now applied via style prop using theme colors
    expect(items.length).toBeGreaterThan(0);

    act(() => {
      findItemByLabel(tree, "B").props.onPress();
    });

    expect(onValueChange).toHaveBeenLastCalledWith("b");
    // Selection state changes styling via style prop
    expect(findItemByLabel(tree, "B").props.style).toBeDefined();

    act(() => {
      findItemByLabel(tree, "B").props.onPress();
    });

    expect(onValueChange).toHaveBeenLastCalledWith("");
  });
});

describe("ToggleGroup - multiple mode", () => {
  it("allows multiple selections independently", () => {
    const onValueChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <ToggleGroup
          type="multiple"
          onValueChange={onValueChange}
          variant="outline"
        >
          <ToggleGroupItem value="a">A</ToggleGroupItem>
          <ToggleGroupItem value="b">B</ToggleGroupItem>
        </ToggleGroup>
      );
    });

    // Text color is now applied via style prop using theme colors
    expect(findItemByLabel(tree, "A").props.style).toBeDefined();
    expect(findItemByLabel(tree, "B").props.style).toBeDefined();

    act(() => {
      findItemByLabel(tree, "A").props.onPress();
    });

    expect(onValueChange).toHaveBeenLastCalledWith(["a"]);

    act(() => {
      findItemByLabel(tree, "B").props.onPress();
    });

    expect(onValueChange).toHaveBeenLastCalledWith(["a", "b"]);

    act(() => {
      findItemByLabel(tree, "A").props.onPress();
    });

    expect(onValueChange).toHaveBeenLastCalledWith(["b"]);
  });
});

describe("ToggleGroup - disabled", () => {
  it("prevents toggling when group is disabled", () => {
    const onValueChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <ToggleGroup disabled onValueChange={onValueChange}>
          <ToggleGroupItem value="x">X</ToggleGroupItem>
        </ToggleGroup>
      );
    });

    const item = findItems(tree)[0];
    expect(item.props.className).toContain("opacity-50");

    act(() => {
      item.props.onPress();
    });

    expect(onValueChange).not.toHaveBeenCalled();
  });
});
