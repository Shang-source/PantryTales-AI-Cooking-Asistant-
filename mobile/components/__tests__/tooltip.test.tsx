import renderer, { act } from "react-test-renderer";
import { TextInput, Text } from "react-native";
import { jest, describe, it, expect, beforeEach, afterEach } from "@jest/globals";
import { Tooltip, TooltipTrigger, TooltipContent } from "../tooltip";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Tooltip", () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("renders root with base className and closed state", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Tooltip className="extra">
          <TooltipTrigger>Press</TooltipTrigger>
          <TooltipContent>tip</TooltipContent>
        </Tooltip>
      );
    });

    const root = tree.root.findByProps({ "data-slot": "tooltip-root" });
    expect(root.props.className).toContain("relative inline-flex");
    expect(root.props.className).toContain("extra");

    const input = tree.root.findByType(TextInput);
    expect(input.props.value).toBe("closed");
  });

  it("opens on press in and hides on press out without delay", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Tooltip>
          <TooltipTrigger>Hover</TooltipTrigger>
          <TooltipContent side="bottom" sideOffset={10} className="custom-content">
            tip text
          </TooltipContent>
        </Tooltip>
      );
    });

    const trigger = tree.root.findByProps({ "data-slot": "tooltip-trigger" });
    act(() => {
      trigger.props.onPressIn?.({} as any);
    });

    const content = tree.root.findByProps({ "data-slot": "tooltip-content" });
    expect(content.props.className).toContain("top-full");
    expect(content.props.className).toContain("mt-3");
    expect(content.props.className).toContain("custom-content");

    const input = tree.root.findByType(TextInput);
    expect(input.props.value).toBe("open");

    act(() => {
      trigger.props.onPressOut?.({} as any);
    });

    expect(tree.root.findAllByProps({ "data-slot": "tooltip-content" }).length).toBe(0);
    expect(tree.root.findByType(TextInput).props.value).toBe("closed");
  });

  it("respects delayDuration before showing content", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Tooltip delayDuration={100}>
          <TooltipTrigger>
            <Text>Hover</Text>
          </TooltipTrigger>
          <TooltipContent>tip delayed</TooltipContent>
        </Tooltip>
      );
    });

    const trigger = tree.root.findByProps({ "data-slot": "tooltip-trigger" });

    act(() => {
      trigger.props.onPressIn?.({} as any);
    });
    expect(tree.root.findAllByProps({ "data-slot": "tooltip-content" }).length).toBe(0);

    act(() => {
      jest.runAllTimers();
    });

    expect(
      tree.root.findAllByProps({ "data-slot": "tooltip-content" }).length
    ).toBeGreaterThanOrEqual(1);
  });
});
