import { type ComponentProps } from "react";
import renderer, { act } from "react-test-renderer";
import { Text } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import {
  Sheet,
  SheetTrigger,
  SheetContent,
  SheetHeader,
  SheetFooter,
  SheetTitle,
  SheetDescription,
  SheetClose,
} from "../sheet";

jest.mock("react-native-safe-area-context", () => {
  return {
    useSafeAreaInsets: jest.fn(() => ({ top: 0, bottom: 0, left: 0, right: 0 })),
  };
});

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("lucide-react-native", () => ({
  X: "XIcon",
}));

jest.mock("expo-router", () => {
  const { useEffect } = require("react");
  return {
    useFocusEffect: (effect: () => void | (() => void)) => {
      useEffect(() => effect?.(), [effect]);
    },
  };
});

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

  const MockModal = ({ visible, children, ...props }: any) =>
    visible ? <RN.View {...props}>{children}</RN.View> : null;

  const AnimatedValue = function (this: any, initial: number) {
    this._value = initial;
    this.setValue = (next: number) => {
      this._value = next;
    };
    this.interpolate = jestGlobals.fn(
      ({ outputRange }: { outputRange: number[] }) =>
        outputRange[outputRange.length - 1]
    );
    this.__getValue = () => this._value;
    return this;
  };

  const timing = (_value: any, _config: any) => ({
    start: (cb?: () => void) => cb?.(),
  });

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

  const mockedRN = {
    ...RN,
    Modal: MockModal,
    Animated: {
      ...RN.Animated,
      Value: AnimatedValue,
      timing,
      View: RN.View,
    },
    InteractionManager: {
      runAfterInteractions: (cb: () => void) => cb(),
    },
  };

  console.warn = originalWarn;

  return mockedRN;
});

const findByTestId = (root: renderer.ReactTestInstance, testID: string) =>
  root.findAll((node) => node.props?.testID === testID);

const findOverlayPressable = (root: renderer.ReactTestInstance) =>
  root.findAll(
    (node) =>
      typeof node.props?.className === "string" &&
      node.props.className.includes("absolute inset-0") &&
      typeof node.props.onPress === "function"
  )[0];

const triggerPress = (tree: renderer.ReactTestRenderer, testID: string) => {
  const nodes = tree.root.findAllByProps({ testID });
  const pressable = nodes.find((n) => typeof n.props.onPress === "function");

  if (!pressable) {
    throw new Error(`Could not find pressable with testID: ${testID}`);
  }

  act(() => {
    pressable.props.onPress?.({});
  });
};

const renderSheet = (
  sheetProps?: Partial<ComponentProps<typeof Sheet>>
): renderer.ReactTestRenderer => {
  let tree!: renderer.ReactTestRenderer;

  act(() => {
    tree = renderer.create(
      <Sheet {...sheetProps}>
        <SheetTrigger testID="sheet-trigger">
          <Text>Open sheet</Text>
        </SheetTrigger>
        <SheetContent testID="sheet-content">
          <SheetHeader>
            <SheetTitle>Sheet title</SheetTitle>
            <SheetDescription testID="sheet-body">
              Sheet description
            </SheetDescription>
          </SheetHeader>
          <SheetFooter>
            <SheetClose testID="sheet-close">
              <Text>Close</Text>
            </SheetClose>
          </SheetFooter>
        </SheetContent>
      </Sheet>
    );
  });

  return tree;
};

const flattenStyle = (style: any) => {
  if (!style) return [];
  return (Array.isArray(style) ? style : [style]).filter(Boolean);
};

const useSafeAreaInsetsMock = useSafeAreaInsets as unknown as jest.Mock;

afterEach(() => {
  jest.clearAllMocks();
});

describe("Sheet", () => {
  it("opens via trigger and closes via overlay in uncontrolled mode", () => {
    const tree = renderSheet();

    expect(findByTestId(tree.root, "sheet-body").length).toBe(0);

    triggerPress(tree, "sheet-trigger");

    expect(findByTestId(tree.root, "sheet-body").length).toBeGreaterThan(0);
    expect(findOverlayPressable(tree.root)).toBeDefined();

    const overlay = findOverlayPressable(tree.root);
    act(() => {
      overlay?.props.onPress?.();
    });

    expect(findByTestId(tree.root, "sheet-body").length).toBe(0);
    expect(findOverlayPressable(tree.root)).toBeUndefined();
  });

  it("invokes onOpenChange without unmounting content in controlled mode", () => {
    const onOpenChange = jest.fn();
    const tree = renderSheet({ open: true, onOpenChange });

    expect(findByTestId(tree.root, "sheet-body").length).toBeGreaterThan(0);

    triggerPress(tree, "sheet-close");

    expect(onOpenChange).toHaveBeenLastCalledWith(false);
    expect(findByTestId(tree.root, "sheet-body").length).toBeGreaterThan(0);
  });

  it("hides the close button when showCloseButton is false", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Sheet open>
          <SheetContent testID="sheet-content" showCloseButton={false}>
            <Text testID="sheet-body">Body</Text>
          </SheetContent>
        </Sheet>
      );
    });

    expect(findByTestId(tree.root, "sheet-body").length).toBeGreaterThan(0);
    expect(tree.root.findAllByType("XIcon" as any).length).toBe(0);
  });

  it("applies safe-area padding for left/right sheets only", () => {
    useSafeAreaInsetsMock.mockReturnValue({
      top: 12,
      bottom: 34,
      left: 0,
      right: 0,
    });

    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Sheet open>
          <SheetContent testID="sheet-content" side="left">
            <Text>Body</Text>
          </SheetContent>
        </Sheet>
      );
    });

    const sheetContent = tree.root
      .findAllByProps({ testID: "sheet-content" })
      .find((node) => typeof node.type === "string" && node.props?.style);

    if (!sheetContent) {
      throw new Error("Could not find host sheet content node.");
    }
    const styleObjects = flattenStyle(sheetContent.props.style);

    expect(styleObjects).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ paddingTop: 12, paddingBottom: 34 }),
      ])
    );

    act(() => {
      tree = renderer.create(
        <Sheet open>
          <SheetContent testID="sheet-content" side="bottom">
            <Text>Body</Text>
          </SheetContent>
        </Sheet>
      );
    });

    const bottomContent = tree.root
      .findAllByProps({ testID: "sheet-content" })
      .find((node) => typeof node.type === "string" && node.props?.style);

    if (!bottomContent) {
      throw new Error("Could not find host bottom sheet content node.");
    }
    const bottomStyleObjects = flattenStyle(bottomContent.props.style);
    const hasSafeAreaPadding = bottomStyleObjects.some(
      (value) =>
        value &&
        typeof value === "object" &&
        ("paddingTop" in value || "paddingBottom" in value)
    );

    expect(hasSafeAreaPadding).toBe(false);
  });
});
