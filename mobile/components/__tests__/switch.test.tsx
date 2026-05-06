import renderer, { act } from "react-test-renderer";
import { describe, it, expect, jest, afterEach } from "@jest/globals";
import { Animated, Pressable } from "react-native";
import { Switch } from "../switch";

// Silence React Native animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const mockTiming = () => {
  // const timingSpy = jest
  //   .spyOn(Animated, "timing")
  //   .mockReturnValue({ start } as unknown as Animated.CompositeAnimation);
  // return { timingSpy, start };
  const timingSpy = jest.spyOn(Animated, "timing").mockImplementation(
    (value: any, config: any) =>
      ({
        start: (cb?: (result?: { finished: boolean }) => void) => {
          if (
            typeof value?.setValue === "function" &&
            config?.toValue !== undefined
          ) {
            value.setValue(config.toValue);
          }
          cb?.({ finished: true });
        },
      }) as any
  );
  return { timingSpy };
};

afterEach(() => {
  jest.restoreAllMocks();
});

describe("Switch", () => {
  it("renders unchecked styles, merges className, and sets up thumb translation", () => {
    mockTiming();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(<Switch className="extra" />);
    });

    const pressable =
      tree.root.findAll(
        (node) =>
          typeof node.props?.className === "string" &&
          node.props.className.includes("inline-flex h-6 w-11")
      )[0] || tree.root.findAllByType(Pressable)[0];
    expect(pressable.props.className).toContain("inline-flex h-6 w-11");
    expect(pressable.props.className).toContain("bg-[#E4E4E7]");
    expect(pressable.props.className).toContain("extra");
    expect(pressable.props.disabled).toBe(false);

    const thumb = tree.root.findByType(Animated.View);
    const transform = (thumb.props.style as any).transform;
    expect(Array.isArray(transform)).toBe(true);
    expect(transform[0]).toHaveProperty("translateX");
  });

  it("uses checked styles and toggles value on press", () => {
    const { timingSpy } = mockTiming();
    const onCheckedChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(<Switch checked onCheckedChange={onCheckedChange} />);
    });

    const pressable =
      tree.root.findAll(
        (node) =>
          typeof node.props?.className === "string" &&
          node.props.className.includes("inline-flex h-6 w-11")
      )[0] || tree.root.findAllByType(Pressable)[0];
    expect(pressable.props.className).toContain("bg-[#18181B]");

    act(() => {
      pressable.props.onPress?.();
    });
    expect(onCheckedChange).toHaveBeenCalledWith(false);
    expect(timingSpy).toHaveBeenCalledWith(
      expect.any(Animated.Value),
      expect.objectContaining({ toValue: 1 })
    );
  });

  it("does nothing when disabled", () => {
    mockTiming();
    const onCheckedChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Switch disabled onCheckedChange={onCheckedChange} />
      );
    });

    const pressable =
      tree.root.findAll(
        (node) =>
          typeof node.props?.className === "string" &&
          node.props.className.includes("inline-flex h-6 w-11")
      )[0] || tree.root.findAllByType(Pressable)[0];
    expect(pressable.props.disabled).toBe(true);
    expect(pressable.props.className).toContain("opacity-50");

    act(() => {
      pressable.props.onPress?.();
    });
    expect(onCheckedChange).not.toHaveBeenCalled();
  });
});
