import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect } from "@jest/globals";
import { Text, TouchableOpacity } from "react-native";
import NavigationMenuComponent, { NavItem } from "../navigation-menu";

// Silence native animated warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock lucide icons to simple host components.
jest.mock("lucide-react-native", () => ({
  ChevronDown: "ChevronDown",
}));

const buildItems = (onDocsPress: () => void): NavItem[] => [
  {
    id: "products",
    label: "Products",
    children: [
      { id: "overview", label: "Overview", onPress: jest.fn() },
      { id: "pricing", label: "Pricing", onPress: jest.fn() },
    ],
  },
  {
    id: "docs",
    label: "Docs",
    onPress: onDocsPress,
  },
  {
    id: "disabled",
    label: "Disabled",
    disabled: true,
    children: [{ id: "hidden", label: "Hidden Child" }],
  },
];

const findButtonByLabel = (
  tree: renderer.ReactTestRenderer,
  label: string
): renderer.ReactTestInstance | undefined =>
  tree.root.findAllByType(TouchableOpacity).find((btn) =>
    btn.findAllByType(Text).some((t) => t.props.children === label)
  );

const hasLabel = (tree: renderer.ReactTestRenderer, label: string): boolean =>
  tree.root.findAllByType(Text).some((t) => t.props.children === label);

describe("NavigationMenu", () => {
  it("opens submenu when a top-level item with children is pressed", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <NavigationMenuComponent items={buildItems(jest.fn())} />
      );
    });

    const productsBtn = findButtonByLabel(tree, "Products");
    expect(productsBtn).toBeDefined();

    act(() => {
      productsBtn!.props.onPress();
    });

    expect(hasLabel(tree, "Overview")).toBe(true);
    expect(hasLabel(tree, "Pricing")).toBe(true);
  });

  it("calls onPress for a leaf item without submenu", () => {
    const onDocs = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <NavigationMenuComponent items={buildItems(onDocs)} />
      );
    });

    const docsBtn = findButtonByLabel(tree, "Docs");
    expect(docsBtn).toBeDefined();

    act(() => {
      docsBtn!.props.onPress();
    });

    expect(onDocs).toHaveBeenCalledTimes(1);
    expect(hasLabel(tree, "Overview")).toBe(false);
  });

  it("does not render submenu for a disabled item", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <NavigationMenuComponent items={buildItems(jest.fn())} />
      );
    });

    const disabledBtn = findButtonByLabel(tree, "Disabled");
    expect(disabledBtn).toBeDefined();
    expect(disabledBtn!.props.disabled).toBe(true);
    expect(hasLabel(tree, "Hidden Child")).toBe(false);
  });
});
