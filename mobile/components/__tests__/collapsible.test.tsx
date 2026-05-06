import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect, afterEach } from "@jest/globals";
import { Text, LayoutAnimation } from "react-native";
import {
  Collapsible,
  CollapsibleTrigger,
  CollapsibleContent,
} from "../collapsible";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const hasText = (
  tree: renderer.ReactTestRenderer,
  value: string
): boolean =>
  tree.root
    .findAllByType(Text)
    .some((node) => node.props.children === value);

const findTrigger = (tree: renderer.ReactTestRenderer) =>
  tree.root.findByProps({ accessibilityRole: "button" });

describe("Collapsible", () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("expands content when the trigger is pressed", () => {
    const onOpenChange = jest.fn();
    jest.spyOn(LayoutAnimation, "configureNext").mockImplementation(() => {});
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Collapsible onOpenChange={onOpenChange}>
          <CollapsibleTrigger>
            <Text>Toggle</Text>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <Text>Details</Text>
          </CollapsibleContent>
        </Collapsible>
      );
    });

    expect(hasText(tree, "Details")).toBe(false);

    const trigger = findTrigger(tree);

    act(() => {
      trigger.props.onPress?.({} as any);
    });

    expect(hasText(tree, "Details")).toBe(true);
    expect(onOpenChange).toHaveBeenCalledWith(true);
    expect(LayoutAnimation.configureNext).toHaveBeenCalled();
  });

  it("does not toggle when disabled", () => {
    const onOpenChange = jest.fn();
    jest.spyOn(LayoutAnimation, "configureNext").mockImplementation(() => {});
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Collapsible disabled onOpenChange={onOpenChange}>
          <CollapsibleTrigger>
            <Text>Toggle</Text>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <Text>Hidden</Text>
          </CollapsibleContent>
        </Collapsible>
      );
    });

    const trigger = findTrigger(tree);
    expect(trigger.props.disabled).toBe(true);

    act(() => {
      trigger.props.onPress?.({} as any);
    });

    expect(onOpenChange).not.toHaveBeenCalled();
    expect(LayoutAnimation.configureNext).not.toHaveBeenCalled();
    expect(hasText(tree, "Hidden")).toBe(false);
  });

  it("respects controlled open state and calls onOpenChange", () => {
    const onOpenChange = jest.fn();
    jest.spyOn(LayoutAnimation, "configureNext").mockImplementation(() => {});
    let tree!: renderer.ReactTestRenderer;

    const renderCollapsible = (open: boolean) => (
      <Collapsible open={open} onOpenChange={onOpenChange}>
        <CollapsibleTrigger>
          <Text>Trigger</Text>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <Text>Panel</Text>
        </CollapsibleContent>
      </Collapsible>
    );

    act(() => {
      tree = renderer.create(renderCollapsible(true));
    });

    const trigger = findTrigger(tree);
    expect(hasText(tree, "Panel")).toBe(true);

    act(() => {
      trigger.props.onPress?.({} as any);
    });

    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(LayoutAnimation.configureNext).toHaveBeenCalled();
    expect(hasText(tree, "Panel")).toBe(true);

    act(() => {
      tree.update(renderCollapsible(false));
    });

    expect(hasText(tree, "Panel")).toBe(false);
  });
});
