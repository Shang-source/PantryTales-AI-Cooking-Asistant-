import renderer, { act } from "react-test-renderer";
import { ScrollView, Text, View } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import { ScrollArea, ScrollBar } from "../scroll-area";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const findOuterContainer = (root: renderer.ReactTestInstance) =>
  root.findAll(
    (node) =>
      node.type === View &&
      typeof node.props.className === "string" &&
      node.props.className.includes("overflow-hidden")
  )[0];

describe("ScrollArea", () => {
  it("renders vertical orientation with default indicators", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <ScrollArea contentContainerClassName="p-2">
          <Text testID="child-text">Content</Text>
        </ScrollArea>
      );
    });

    const scrollView = tree.root.findByType(ScrollView);
    const container = findOuterContainer(tree.root);

    expect(container).toBeDefined();
    expect(container.props.className).toContain("overflow-hidden");

    expect(scrollView.props.horizontal).toBe(false);
    expect(scrollView.props.nestedScrollEnabled).toBe(true);
    expect(scrollView.props.showsVerticalScrollIndicator).toBe(true);
    expect(scrollView.props.showsHorizontalScrollIndicator).toBe(false);
    expect(scrollView.props.contentContainerClassName).toContain("flex-col");
    expect(scrollView.props.contentContainerClassName).toContain("p-2");
    expect(tree.root.findByProps({ testID: "child-text" }).props.children).toBe(
      "Content"
    );
  });

  it("switches to horizontal layout and merges class names", () => {
    const contentClass = "gap-4";
    const outerClass = "bg-neutral-50";

    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <ScrollArea
          orientation="horizontal"
          className={outerClass}
          contentContainerClassName={contentClass}
        >
          <Text>Item</Text>
        </ScrollArea>
      );
    });

    const scrollView = tree.root.findByType(ScrollView);
    const container = findOuterContainer(tree.root);

    expect(scrollView.props.horizontal).toBe(true);
    expect(scrollView.props.showsVerticalScrollIndicator).toBe(false);
    expect(scrollView.props.showsHorizontalScrollIndicator).toBe(true);
    expect(scrollView.props.contentContainerClassName).toContain("flex-row");
    expect(scrollView.props.contentContainerClassName).toContain(contentClass);
    expect(container.props.className).toContain("overflow-hidden");
    expect(container.props.className).toContain(outerClass);
  });

  it("respects explicit indicator props on both orientations", () => {
    let verticalTree!: renderer.ReactTestRenderer;
    act(() => {
      verticalTree = renderer.create(
        <ScrollArea showsVerticalScrollIndicator={false}>
          <Text>Vertical</Text>
        </ScrollArea>
      );
    });

    expect(
      verticalTree.root.findByType(ScrollView).props.showsVerticalScrollIndicator
    ).toBe(false);

    let horizontalTree!: renderer.ReactTestRenderer;
    act(() => {
      horizontalTree = renderer.create(
        <ScrollArea
          orientation="horizontal"
          showsHorizontalScrollIndicator={false}
        >
          <Text>Horizontal</Text>
        </ScrollArea>
      );
    });

    expect(
      horizontalTree.root.findByType(ScrollView).props.showsHorizontalScrollIndicator
    ).toBe(false);
  });
});

describe("ScrollBar", () => {
  it("renders null placeholder", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(<ScrollBar orientation="vertical" />);
    });

    expect(tree.toJSON()).toBeNull();
  });
});
