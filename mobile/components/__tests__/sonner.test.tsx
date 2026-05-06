import renderer, { act } from "react-test-renderer";
import { Text, TouchableOpacity, Animated } from "react-native";
import { Toaster, toast } from "../sonner";
import { jest, describe, it, expect, afterAll } from "@jest/globals";

jest.mock("lucide-react-native", () => {
  const MockIcon = () => null;
  return {
    __esModule: true,
    CheckCircle: MockIcon,
    XCircle: MockIcon,
    Info: MockIcon,
    X: MockIcon,
  };
});
jest.mock("react-native-css-interop", () => ({}));

// Silence NativeAnimatedHelper warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.useFakeTimers();

// Make Animated timing/spring synchronous so state updates apply immediately in tests.
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

const springSpy = jest.spyOn(Animated, "spring").mockImplementation(
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

afterAll(() => {
  timingSpy.mockRestore();
  springSpy.mockRestore();
});

describe("Toaster", () => {
  it("renders toast message and description when triggered", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(<Toaster />);
    });

    act(() => {
      toast.success("Saved", { description: "All good" });
    });

    const message = tree.root.findByProps({ children: "Saved" });
    const description = tree.root.findByProps({ children: "All good" });

    expect(message).toBeTruthy();
    expect(description).toBeTruthy();
  });

  it("invokes action callback and dismisses the toast on action press", () => {
    const actionSpy = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(<Toaster />);
    });

    act(() => {
      toast("Undo me", {
        action: { label: "Undo", onClick: actionSpy },
        duration: 1000,
      });
    });

    // Find the action button by its nested text label.
    const actionButton = tree.root
      .findAllByType(TouchableOpacity)
      .find((btn) => {
        try {
          return btn.findByType(Text).props.children === "Undo";
        } catch {
          return false;
        }
      });

    expect(actionButton).toBeDefined();

    act(() => {
      actionButton?.props.onPress?.();
    });

    expect(actionSpy).toHaveBeenCalled();

    // After pressing action, toast should be removed.
    expect(() => tree.root.findByProps({ children: "Undo me" })).toThrow();
  });
});
