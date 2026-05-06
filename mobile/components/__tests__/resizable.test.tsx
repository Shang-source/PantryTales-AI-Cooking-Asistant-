import { Text } from "react-native";
import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect, afterEach } from "@jest/globals";
import {
  ResizablePanelGroup,
  ResizablePanel,
  ResizableHandle,
} from "../resizable";

jest.mock("lucide-react-native", () => ({
  GripVertical: () => "GripVertical",
  GripHorizontal: () => "GripHorizontal",
}));

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

  return {
    ...spreadRN,
    PanResponder: {
      create: jestGlobals.fn((config: any) => ({
        panHandlers: {
          onStartShouldSetResponder: () => true,
          onMoveShouldSetResponder: () => true,
          onResponderGrant: (evt: any, gestureState: any) => {
            config.onPanResponderGrant &&
              config.onPanResponderGrant(evt, gestureState);
          },
          onResponderMove: (evt: any, gestureState: any) => {
            config.onPanResponderMove &&
              config.onPanResponderMove(evt, gestureState);
          },
          onResponderRelease: (evt: any, gestureState: any) => {
            config.onPanResponderRelease &&
              config.onPanResponderRelease(evt, gestureState);
          },
        },
      })),
    },
  };
});

const getFlexStyle = (node: renderer.ReactTestInstance) => {
  const style = node.props.style;
  if (!style) return undefined;
  if (Array.isArray(style)) {
    const flexObj = style.find((s: any) => s && typeof s.flex === "number");
    return flexObj ? flexObj.flex : undefined;
  }
  return style.flex;
};

const findPanelNode = (root: renderer.ReactTestInstance, testID: string) => {
  const nodes = root.findAll((n) => n.props.testID === testID);
  const node = nodes.find((n) => n.props.style !== undefined);
  if (!node)
    throw new Error(`Could not find Panel View with testID: ${testID}`);
  return node;
};

const findHandleNode = (root: renderer.ReactTestInstance, testID: string) => {
  const nodes = root.findAll((n) => n.props.testID === testID);
  const node = nodes.find(
    (n) => typeof n.props.onResponderGrant === "function"
  );
  if (!node)
    throw new Error(`Could not find Handle View with testID: ${testID}`);
  return node;
};

const findContainerNode = (
  root: renderer.ReactTestInstance,
  testID: string
) => {
  const nodes = root.findAll((n) => n.props.testID === testID);
  const node = nodes.find((n) => typeof n.props.onLayout === "function");
  if (!node)
    throw new Error(`Could not find Container View with testID: ${testID}`);
  return node;
};

afterEach(() => {
  jest.clearAllMocks();
});

describe("Resizable Component", () => {
  it("renders correctly with children", () => {
    let tree;
    act(() => {
      tree = renderer.create(
        <ResizablePanelGroup>
          <ResizablePanel>
            <Text>Panel 1</Text>
          </ResizablePanel>
          <ResizableHandle />
          <ResizablePanel>
            <Text>Panel 2</Text>
          </ResizablePanel>
        </ResizablePanelGroup>
      );
    });
    expect(tree).toBeDefined();
  });

  it("updates panel sizes when dragged horizontally", () => {
    let tree: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <ResizablePanelGroup initialRatio={0.5} testID="group-container">
          <ResizablePanel order="first" testID="panel-1">
            <Text>Left</Text>
          </ResizablePanel>
          <ResizableHandle testID="resize-handle" />
          <ResizablePanel order="second" testID="panel-2">
            <Text>Right</Text>
          </ResizablePanel>
        </ResizablePanelGroup>
      );
    });

    const root = tree!.root;

    const container = findContainerNode(root, "group-container");
    act(() => {
      container.props.onLayout({
        nativeEvent: { layout: { width: 1000, height: 500 } },
      });
    });

    const panel1 = findPanelNode(root, "panel-1");
    expect(getFlexStyle(panel1)).toBe(0.5);

    const handle = findHandleNode(root, "resize-handle");
    act(() => {
      handle.props.onResponderGrant();
      handle.props.onResponderMove({}, { dx: 200, dy: 0 });
    });

    expect(getFlexStyle(panel1)).toBe(0.7);

    const panel2 = findPanelNode(root, "panel-2");
    expect(getFlexStyle(panel2)).toBeCloseTo(0.3);
  });

  it("respects min (0.1) and max (0.9) limits", () => {
    let tree: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <ResizablePanelGroup initialRatio={0.5} testID="group-container">
          <ResizablePanel order="first" testID="panel-1" />
          <ResizableHandle testID="resize-handle" />
          <ResizablePanel order="second" />
        </ResizablePanelGroup>
      );
    });

    const root = tree!.root;

    const container = findContainerNode(root, "group-container");
    act(() => {
      container.props.onLayout({
        nativeEvent: { layout: { width: 1000, height: 500 } },
      });
    });

    const handle = findHandleNode(root, "resize-handle");
    const panel1 = findPanelNode(root, "panel-1");

    act(() => {
      handle.props.onResponderGrant();
      handle.props.onResponderMove({}, { dx: 800, dy: 0 });
    });
    expect(getFlexStyle(panel1)).toBe(0.9);
    act(() => {
      handle.props.onResponderMove({}, { dx: -800, dy: 0 });
    });
    expect(getFlexStyle(panel1)).toBe(0.1);
  });
});
