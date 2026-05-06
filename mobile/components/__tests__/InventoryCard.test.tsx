import renderer, { act } from "react-test-renderer";
import { Text, Pressable, View } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import { InventoryItemCard } from "../inventory/InventoryCard";
import { IconBadge } from "../IconBadge";
import type { InventoryItemForm } from "@/types/Inventory";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock vector icons to simple text placeholders
jest.mock("@expo/vector-icons", () => {
  const { Text } = require("react-native");
  const make = (displayName: string) => {
    const Comp = (props: any) => <Text {...props}>{displayName}</Text>;
    Comp.displayName = displayName;
    return Comp;
  };
  return {
    Ionicons: make("Ionicons"),
    MaterialCommunityIcons: make("MaterialCommunityIcons"),
  };
});

jest.mock("lucide-react-native", () => {
  const { Text } = require("react-native");
  const make = (name: string) => {
    const Comp = (props: any) => <Text testID={name} {...props} />;
    Comp.displayName = name;
    return Comp;
  };
  return {
    Pencil: make("Pencil"),
    Trash2: make("Trash2"),
    Refrigerator: make("Refrigerator"),
  };
});

// Simplify dialog primitives to avoid portal/Modal behavior in tests
jest.mock("@/components/dialog", () => {
  const { View } = require("react-native");
  const PassThrough = ({ children, ...props }: any) => (
    <View {...props}>{children}</View>
  );
  return {
    Dialog: PassThrough,
    DialogTrigger: PassThrough,
    DialogContent: PassThrough,
    DialogHeader: PassThrough,
    DialogTitle: PassThrough,
    DialogFooter: PassThrough,
    DialogClose: PassThrough,
  };
});

// Stub InventoryItemDialog to keep the test focused on InventoryItemCard layout/behavior
jest.mock("@/components/inventory/InventoryItemDialog", () => {
  const { View } = require("react-native");
  return {
    InventoryItemDialog: (props: any) => (
      <View testID="inventory-item-dialog" {...props} />
    ),
  };
});

const renderCard = (
  overrides?: Partial<React.ComponentProps<typeof InventoryItemCard>>
) => {
  const inventoryItem: InventoryItemForm = {
    id: "item-1",
    name: "Apples",
    amount: "3",
    unit: "kg",
    storage: "Refrigerated",
    addedDate: "2025-01-01T00:00:00Z",
    expiryDays: "7",
    ...(overrides?.inventoryItem ?? {}),
  };

  const props = {
    inventoryItem,
    onEdit: jest.fn(),
    onDelete: jest.fn(),
    ...overrides,
  };

  let tree!: renderer.ReactTestRenderer;
  act(() => {
    tree = renderer.create(<InventoryItemCard {...props} />);
  });

  return { tree, props };
};

describe("InventoryItemCard", () => {
  it("renders key details and storage badge icon", () => {
    const { tree } = renderCard();

    expect(tree.root.findByProps({ children: "Apples" })).toBeTruthy();
    const quantityText = tree.root.findAllByType(Text).find((t) => {
      const children = t.props.children;
      if (Array.isArray(children)) {
        return children.join("") === "3 kg";
      }
      return children === "3 kg";
    });
    expect(quantityText).toBeTruthy();
    expect(tree.root.findByProps({ children: "Refrigerated" })).toBeTruthy();
    const addedText = tree.root.findAllByType(Text).find((t) => {
      const children = t.props.children;
      if (Array.isArray(children)) {
        return children.join("").startsWith("Added");
      }
      return typeof children === "string" && children.startsWith("Added");
    });
    expect(addedText).toBeTruthy();
    const daysLeftText = tree.root.findAllByType(Text).find((t) => {
      const children = t.props.children;
      if (Array.isArray(children)) {
        return children.join("") === "7 days left";
      }
      return children === "7 days left";
    });
    expect(daysLeftText).toBeTruthy();

    const badge = tree.root.findByType(IconBadge);
    expect(badge.props.iconSet).toBe("MaterialCommunityIcons");
    expect(badge.props.iconName).toBe("fridge");
  });

  it("invokes onDelete when confirming delete", () => {
    const { tree, props } = renderCard();
    const trashButton = tree.root
      .findAll((node) => typeof node.props?.onPress === "function")
      .find((node) => node.findAllByProps({ testID: "Trash2" }).length > 0);

    expect(trashButton).toBeDefined();
    act(() => {
      trashButton?.props.onPress?.();
    });

    expect(props.onDelete).toHaveBeenCalledTimes(1);
    expect(props.onDelete).toHaveBeenCalledWith("item-1");
  });
});
