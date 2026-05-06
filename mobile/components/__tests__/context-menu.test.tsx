import renderer, { act } from "react-test-renderer";
import {
  jest,
  describe,
  it,
  expect,
  beforeEach,
  afterEach,
} from "@jest/globals";
import { Dimensions, Text as RNText } from "react-native";
import {
  ContextMenu,
  ContextMenuTrigger,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuCheckboxItem,
  ContextMenuRadioItem,
} from "../context-menu";

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

jest.mock(
  "react-native/Libraries/Components/ProgressBarAndroid/ProgressBarAndroid",
  () => "ProgressBarAndroid"
);

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("lucide-react-native", () => ({
  Check: "CheckIcon",
  Circle: "CircleIcon",
}));

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

  const MockModal = ({ visible, children, ...props }: any) => {
    if (!visible) return null;
    return (
      <RN.View {...props} testID="mock-modal">
        {children}
      </RN.View>
    );
  };

  return {
    ...spreadRN,
    Modal: MockModal,
  };
});

const hasText = (tree: renderer.ReactTestRenderer, value: string): boolean => {
  return (
    tree.root.findAll((node) => {
      return (
        node.props.children === value ||
        (Array.isArray(node.props.children) &&
          node.props.children.includes(value))
      );
    }).length > 0
  );
};

const findByClassSubstring = (
  tree: renderer.ReactTestRenderer,
  token: string
) =>
  tree.root.findAll(
    (node) =>
      node.props &&
      typeof node.props.className === "string" &&
      node.props.className.includes(token)
  );

const triggerLongPress = (tree: renderer.ReactTestRenderer) => {
  const trigger = tree.root.findByProps({ delayLongPress: 200 });
  act(() => {
    trigger.props.onLongPress?.({
      nativeEvent: { pageX: 280, pageY: 450 },
    } as any);
  });
};

describe("ContextMenu", () => {
  let dimensionsSpy: any;

  beforeEach(() => {
    dimensionsSpy = jest.spyOn(Dimensions, "get").mockReturnValue({
      width: 375,
      height: 812,
      scale: 1,
      fontScale: 1,
    });
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  const renderMenu = (
    itemProps?: Partial<React.ComponentProps<typeof ContextMenuItem>>
  ) => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <ContextMenu>
          <ContextMenuTrigger>
            <RNText>Open menu</RNText>
          </ContextMenuTrigger>
          <ContextMenuContent>
            <ContextMenuItem {...itemProps}>Delete</ContextMenuItem>
            <ContextMenuCheckboxItem checked>Checkbox</ContextMenuCheckboxItem>
            <ContextMenuRadioItem checked>Radio</ContextMenuRadioItem>
          </ContextMenuContent>
        </ContextMenu>
      );
    });
    return tree;
  };

  it("opens at long-press position, clamps coordinates, and renders items/icons", () => {
    dimensionsSpy.mockReturnValue({
      width: 300,
      height: 500,
      scale: 1,
      fontScale: 1,
    });

    const tree = renderMenu();
    triggerLongPress(tree);

    const menuContainers = findByClassSubstring(tree, "absolute w-[200px]");
    expect(menuContainers.length).toBeGreaterThan(0);
    const menuContainer = menuContainers[0];

    expect(menuContainer.props.style).toMatchObject({ top: 250 });

    expect(hasText(tree, "Delete")).toBe(true);
    expect(hasText(tree, "Checkbox")).toBe(true);
    expect(hasText(tree, "Radio")).toBe(true);
  });

  it("closes when pressing the overlay", () => {
    const tree = renderMenu();
    triggerLongPress(tree);

    const overlays = findByClassSubstring(tree, "flex-1");
    expect(overlays.length).toBeGreaterThan(0);

    act(() => {
      overlays[0].props.onPress?.({} as any);
    });

    const modalContent = tree.root.findAllByProps({ testID: "mock-modal" });
    expect(modalContent.length).toBe(0);
  });

  it("invokes item handler and closes the menu", () => {
    const onSelect = jest.fn();
    const tree = renderMenu({ onPress: onSelect });
    triggerLongPress(tree);

    const items = findByClassSubstring(tree, "flex-row items-center");

    const deleteItem = items.find((item) => {
      try {
        const children = item.props.children;
        if (children === "Delete") return true;
        const textNodes = item.findAll((n) => n.props.children === "Delete");
        return textNodes.length > 0;
      } catch {
        return false;
      }
    });

    expect(deleteItem).toBeDefined();

    act(() => {
      deleteItem!.props.onPress?.({} as any);
    });

    expect(onSelect).toHaveBeenCalledTimes(1);

    const modalContent = tree.root.findAllByProps({ testID: "mock-modal" });
    expect(modalContent.length).toBe(0);
  });
});
