import { type ComponentProps } from "react";
import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect, afterEach } from "@jest/globals";
import { Text } from "react-native";
import {
  Drawer,
  DrawerTrigger,
  DrawerContent,
  DrawerHeader,
  DrawerFooter,
  DrawerTitle,
  DrawerDescription,
  DrawerClose,
} from "../drawer";


jest.mock(
  "react-native/Libraries/Components/ProgressBarAndroid/ProgressBarAndroid",
  () => "ProgressBarAndroid"
);
jest.mock(
  "react-native/Libraries/Components/Clipboard/Clipboard",
  () => "Clipboard"
);
jest.mock(
  "react-native/Libraries/PushNotificationIOS/PushNotificationIOS",
  () => ({})
);
jest.mock(
  "react-native/Libraries/Components/SafeAreaView/SafeAreaView",
  () => "SafeAreaView"
);
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("react-native/Libraries/TurboModule/TurboModuleRegistry", () => {
  const actual = jest.requireActual(
    "react-native/Libraries/TurboModule/TurboModuleRegistry"
  ) as any;
  return {
    ...actual,
    getEnforcing: (name: string) => {
      if (name === "DevMenu" || name === "SettingsManager") {
        return {
          getConstants: () => ({ settings: {} }),
          addListener: jest.fn(),
          removeListener: jest.fn(),
          removeListeners: jest.fn(),
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
  const RN = jest.requireActual("react-native") as any;

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

  if (!pressable) {
    throw new Error(`Could not find pressable trigger with testID: ${testID}`);
  }

  act(() => {
    pressable.props.onPress();
  });
};

const renderDrawer = (drawerProps?: Partial<ComponentProps<typeof Drawer>>) => {
  const onOpenChange = drawerProps?.onOpenChange ?? jest.fn();
  let tree!: renderer.ReactTestRenderer;

  act(() => {
    tree = renderer.create(
      <Drawer {...drawerProps} onOpenChange={onOpenChange}>
        <DrawerTrigger testID="drawer-trigger">
          <Text>Open drawer</Text>
        </DrawerTrigger>
        <DrawerContent testID="drawer-content">
          <DrawerHeader>
            <DrawerTitle>Drawer title</DrawerTitle>
            <DrawerDescription testID="drawer-body">
              Drawer description
            </DrawerDescription>
          </DrawerHeader>
          <DrawerFooter>
            <DrawerClose testID="drawer-close">
              <Text>Close</Text>
            </DrawerClose>
          </DrawerFooter>
        </DrawerContent>
      </Drawer>
    );
  });

  return { tree, onOpenChange };
};

afterEach(() => {
  jest.clearAllMocks();
});

describe("Drawer", () => {
  it("opens via trigger and closes via the overlay in uncontrolled mode", () => {
    const { tree, onOpenChange } = renderDrawer({ defaultOpen: false });

    expect(findByTestId(tree.root, "drawer-body").length).toBe(0);

    triggerPress(tree, "drawer-trigger");

    expect(findByTestId(tree.root, "drawer-body").length).toBeGreaterThan(0);
    expect(onOpenChange).toHaveBeenLastCalledWith(true);

    const overlay = findOverlay(tree);
    expect(overlay).toBeDefined();

    act(() => {
      overlay?.props.onPress?.();
    });

    expect(findByTestId(tree.root, "drawer-body").length).toBe(0);
    expect(onOpenChange).toHaveBeenLastCalledWith(false);
  });

  it("invokes onOpenChange without unmounting content in controlled mode", () => {
    const { tree, onOpenChange } = renderDrawer({ open: true });

    expect(findByTestId(tree.root, "drawer-body").length).toBeGreaterThan(0);

    triggerPress(tree, "drawer-close");

    expect(onOpenChange).toHaveBeenLastCalledWith(false);
    expect(findByTestId(tree.root, "drawer-body").length).toBeGreaterThan(0);
  });
});
