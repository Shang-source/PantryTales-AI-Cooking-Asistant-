import renderer, { act } from "react-test-renderer";
import { Text, TouchableOpacity, TouchableWithoutFeedback } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "../select";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("lucide-react-native", () => ({
  Check: "CheckIcon",
  ChevronDown: "ChevronDownIcon",
}));

jest.mock("react-native/Libraries/TurboModule/TurboModuleRegistry", () => {
  const { jest: jestGlobals } = require("@jest/globals");
  const actual = jestGlobals.requireActual(
    "react-native/Libraries/TurboModule/TurboModuleRegistry"
  ) as any;
  return {
    ...actual,
    getEnforcing: (name: string) => {
      if (name === "DevMenu" || name === "SettingsManager") {
        return {
          getConstants: () => ({ settings: {} }),
          addListener: jestGlobals.fn(),
          removeListener: jestGlobals.fn(),
          removeListeners: jestGlobals.fn(),
        };
      }
      try {
        return actual.getEnforcing(name);
      } catch {
        return {};
      }
    },
  };
});

jest.mock("react-native", () => {
  const { jest: jestGlobals } = require("@jest/globals");
  const RN = jestGlobals.requireActual("react-native") as any;

  const originalWarn = console.warn;

  console.warn = (...args) => {
    const msg = args[0];
    if (
      typeof msg === "string" &&
      (msg.includes("extracted") ||
        msg.includes("deprecated") ||
        msg.includes("NativeEventEmitter"))
    ) {
      return;
    }
    originalWarn(...args);
  };

  const spreadRN = { ...RN };
  console.warn = originalWarn;

  const MockModal = ({ visible, children, ...props }: any) =>
    visible ? <spreadRN.View {...props}>{children}</spreadRN.View> : null;

  return {
    ...spreadRN,
    Modal: MockModal,
  };
});

const findValueNode = (root: renderer.ReactTestInstance) =>
  root.findAllByType(Text).find((n) => n.props.numberOfLines === 1);

const findOverlay = (root: renderer.ReactTestInstance) =>
  root.findAll(
    (node) =>
      typeof node.props?.className === "string" &&
      node.props.className.includes("bg-black/50")
  )[0];

afterEach(() => {
  jest.clearAllMocks();
});

describe("Select", () => {
  it("opens content, selects an option, updates label, and closes", () => {
    const onValueChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Select onValueChange={onValueChange}>
          <SelectTrigger>
            <SelectValue placeholder="Pick one" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="one" label="Option One">
              Option One
            </SelectItem>
            <SelectItem value="two" label="Option Two">
              Option Two
            </SelectItem>
          </SelectContent>
        </Select>
      );
    });

    expect(findValueNode(tree.root)?.props.children).toBe("Pick one");

    const trigger = tree.root
      .findAllByType(TouchableOpacity)
      .find((node) => typeof node.props.onPress === "function");
    act(() => {
      trigger?.props.onPress();
    });

    expect(findOverlay(tree.root)).toBeDefined();

    const optionTwoButton = tree.root
      .findAllByType(TouchableOpacity)
      .find(
        (node) =>
          typeof node.props.onPress === "function" &&
          node
            .findAllByType(Text)
            .some((t) => t.props.children === "Option Two")
      );

    act(() => {
      optionTwoButton?.props.onPress();
    });

    expect(onValueChange).toHaveBeenLastCalledWith("two");
    expect(findValueNode(tree.root)?.props.children).toBe("Option Two");
    expect(findOverlay(tree.root)).toBeUndefined();
  });

  it("shows the label for the default selected value", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Select defaultValue="banana">
          <SelectTrigger>
            <SelectValue placeholder="Pick fruit" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="apple" label="Apple">
              Apple
            </SelectItem>
            <SelectItem value="banana" label="Banana">
              Banana
            </SelectItem>
          </SelectContent>
        </Select>
      );
    });

    expect(findValueNode(tree.root)?.props.children).toBe("banana");
  });

  it("invokes onOpenChange when toggling open state", () => {
    const onOpenChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Select onOpenChange={onOpenChange}>
          <SelectTrigger>
            <SelectValue placeholder="Toggle" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="one" label="One">
              One
            </SelectItem>
          </SelectContent>
        </Select>
      );
    });

    const trigger = tree.root
      .findAllByType(TouchableOpacity)
      .find((node) => typeof node.props.onPress === "function");
    act(() => {
      trigger?.props.onPress();
    });

    expect(onOpenChange).toHaveBeenLastCalledWith(true);
    const overlayPressable = tree.root
      .findAllByType(TouchableWithoutFeedback)
      .find((node) => typeof node.props.onPress === "function");

    act(() => {
      overlayPressable?.props.onPress?.();
    });

    expect(onOpenChange).toHaveBeenLastCalledWith(false);
    expect(findOverlay(tree.root)).toBeUndefined();
  });
});
