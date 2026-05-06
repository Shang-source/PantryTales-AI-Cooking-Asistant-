import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect } from "@jest/globals";
import Menubar, { MenuItem } from "../menubar";
import { Text, TouchableOpacity } from "react-native";

// Avoid React Native animated warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock lucide icons as simple host components to avoid native dependencies.
jest.mock("lucide-react-native", () => ({
  ChevronRight: "ChevronRight",
  Check: "Check",
  Circle: "Circle",
}));

const buildMenuItems = (onAction: () => void): MenuItem[] => [
  {
    id: "file",
    label: "File",
    type: "submenu",
    children: [
      { id: "new", label: "New", onPress: jest.fn() },
      { id: "open", label: "Open", onPress: jest.fn() },
    ],
  },
  {
    id: "help",
    label: "Help",
    onPress: onAction,
  },
  {
    id: "disabled",
    label: "Disabled Menu",
    disabled: true,
    type: "submenu",
    children: [{ id: "should-not-open", label: "Hidden Child" }],
  },
];

const findButtonByLabel = (
  tree: renderer.ReactTestRenderer,
  label: string
): renderer.ReactTestInstance | undefined =>
  tree.root.findAllByType(TouchableOpacity).find((btn) =>
    btn
      .findAllByType(Text)
      .some((text) => text.props.children === label)
  );

const hasLabel = (tree: renderer.ReactTestRenderer, label: string): boolean =>
  tree.root.findAllByType(Text).some((t) => t.props.children === label);

describe("Menubar", () => {
  it("opens a submenu when a top-level item with children is pressed", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Menubar items={buildMenuItems(jest.fn())} />);
    });

    const fileButton = findButtonByLabel(tree, "File");
    expect(fileButton).toBeDefined();

    act(() => {
      fileButton!.props.onPress();
    });

    expect(hasLabel(tree, "New")).toBe(true);
    expect(hasLabel(tree, "Open")).toBe(true);
  });

  it("calls onPress for a normal item without submenu", () => {
    const onHelp = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Menubar items={buildMenuItems(onHelp)} />);
    });

    const helpButton = findButtonByLabel(tree, "Help");
    expect(helpButton).toBeDefined();

    act(() => {
      helpButton!.props.onPress();
    });

    expect(onHelp).toHaveBeenCalledTimes(1);
    expect(hasLabel(tree, "New")).toBe(false); // no submenu opened for Help
  });

  it("does not open a submenu when the item is disabled", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Menubar items={buildMenuItems(jest.fn())} />);
    });

    const disabledButton = findButtonByLabel(tree, "Disabled Menu");
    expect(disabledButton).toBeDefined();

    act(() => {
      disabledButton!.props.onPress();
    });

    expect(hasLabel(tree, "Hidden Child")).toBe(false);
  });
});
