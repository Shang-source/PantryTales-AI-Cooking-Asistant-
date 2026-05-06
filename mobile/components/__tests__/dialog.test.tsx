import type { ComponentProps } from "react";
import renderer, { act } from "react-test-renderer";
import { Text } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";
import {
  Dialog,
  DialogTrigger,
  DialogContent,
  DialogTitle,
  DialogDescription,
  DialogFooter,
  DialogClose,
} from "../dialog";

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

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
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
    visible ? <RN.View {...props}>{children}</RN.View> : null;

  const MockKeyboardAvoidingView = ({ children, ...props }: any) => (
    <RN.View {...props}>{children}</RN.View>
  );

  return {
    ...spreadRN,
    Modal: MockModal,
    KeyboardAvoidingView: MockKeyboardAvoidingView,
  };
});

jest.mock("lucide-react-native", () => ({
  X: "XIcon",
}));

const findByTestId = (root: renderer.ReactTestInstance, testID: string) =>
  root.findAll((node) => node.props?.testID === testID);

const findOverlay = (tree: renderer.ReactTestRenderer) => {
  const nodes = tree.root.findAll(
    (node) =>
      node.props.className &&
      typeof node.props.className === "string" &&
      node.props.className.includes("bg-black/50")
  );
  return nodes[0];
};

const triggerPress = (tree: renderer.ReactTestRenderer, testID: string) => {
  const nodes = tree.root.findAllByProps({ testID });
  const pressable = nodes.find((n) => n.props.onPress);
  if (!pressable)
    throw new Error(`Could not find pressable trigger with testID: ${testID}`);

  act(() => {
    pressable.props.onPress();
  });
};

const renderDialog = (
  dialogProps?: Partial<ComponentProps<typeof Dialog>>,
  contentProps?: Partial<ComponentProps<typeof DialogContent>>
) => {
  const onOpenChange = dialogProps?.onOpenChange ?? jest.fn();
  let tree!: renderer.ReactTestRenderer;

  act(() => {
    tree = renderer.create(
      <Dialog {...dialogProps} onOpenChange={onOpenChange}>
        <DialogTrigger testID="dialog-trigger">
          <Text>Open dialog</Text>
        </DialogTrigger>
        <DialogContent {...contentProps} testID="dialog-content">
          <DialogTitle>Dialog title</DialogTitle>
          <DialogDescription testID="dialog-body">
            Dialog description
          </DialogDescription>
          <DialogFooter>
            <DialogClose testID="dialog-close">
              <Text>Close</Text>
            </DialogClose>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    );
  });

  return { tree, onOpenChange };
};

afterEach(() => {
  jest.clearAllMocks();
});

describe("Dialog", () => {
  it("opens via trigger and closes via the overlay in uncontrolled mode", () => {
    const { tree, onOpenChange } = renderDialog({ defaultOpen: false });

    expect(findByTestId(tree.root, "dialog-body").length).toBe(0);

    triggerPress(tree, "dialog-trigger");

    expect(findByTestId(tree.root, "dialog-body").length).toBeGreaterThan(0);
    expect(onOpenChange).toHaveBeenLastCalledWith(true);

    const overlay = findOverlay(tree);
    expect(overlay).toBeDefined();

    act(() => {
      overlay.props.onPress?.();
    });

    expect(findByTestId(tree.root, "dialog-body").length).toBe(0);
    expect(onOpenChange).toHaveBeenLastCalledWith(false);
  });

  it("invokes onOpenChange without unmounting content in controlled mode", () => {
    const { tree, onOpenChange } = renderDialog({ open: true });

    expect(findByTestId(tree.root, "dialog-body").length).toBeGreaterThan(0);
    expect(tree.root.findAll((n) => (n.type as any) === "XIcon").length).toBe(
      1
    );

    triggerPress(tree, "dialog-close");

    expect(onOpenChange).toHaveBeenLastCalledWith(false);
    expect(findByTestId(tree.root, "dialog-body").length).toBeGreaterThan(0);
  });

  it("omits the built-in close button when hideCloseButton is true", () => {
    const { tree } = renderDialog({ open: true }, { hideCloseButton: true });

    expect(findByTestId(tree.root, "dialog-body").length).toBeGreaterThan(0);
    expect(tree.root.findAll((n) => (n.type as any) === "XIcon").length).toBe(
      0
    );
  });
});
