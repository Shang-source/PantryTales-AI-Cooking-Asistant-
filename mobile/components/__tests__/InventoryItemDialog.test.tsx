import renderer, { act } from "react-test-renderer";
import { Text, TextInput, TouchableOpacity } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import { InventoryItemDialog } from "../inventory/InventoryItemDialog";
import type { InventoryItemForm } from "@/types/Inventory";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("@/components/dialog", () => {
  const { View } = require("react-native");
  const PassThrough = ({ children, ...props }: any) => (
    <View {...props}>{children}</View>
  );
  return {
    Dialog: PassThrough,
    DialogContent: PassThrough,
    DialogHeader: PassThrough,
    DialogTitle: PassThrough,
    DialogFooter: PassThrough,
    DialogClose: PassThrough,
  };
});

jest.mock("@/components/dropdown-menu", () => {
  const { View, TouchableOpacity } = require("react-native");
  const PassThrough = ({ children, ...props }: any) => (
    <View {...props}>{children}</View>
  );
  const Trigger = ({ children, ...props }: any) => (
    <TouchableOpacity {...props}>{children}</TouchableOpacity>
  );
  const Item = ({ children, ...props }: any) => (
    <TouchableOpacity {...props}>{children}</TouchableOpacity>
  );
  return {
    DropdownMenu: PassThrough,
    DropdownMenuTrigger: Trigger,
    DropdownMenuContent: PassThrough,
    DropdownMenuItem: Item,
  };
});

const renderDialog = (
  overrides?: Partial<React.ComponentProps<typeof InventoryItemDialog>>
) => {
  const props = {
    open: true,
    onOpenChange: jest.fn(),
    onAdd: jest.fn(),
    onUpdate: jest.fn(),
    item: null as InventoryItemForm | null,
    ...overrides,
  };

  let tree!: renderer.ReactTestRenderer;
  act(() => {
    tree = renderer.create(<InventoryItemDialog {...props} />);
  });

  return { tree, props };
};

const getInput = (tree: renderer.ReactTestRenderer, placeholder: string) =>
  tree.root
    .findAllByType(TextInput)
    .find((input) => input.props.placeholder === placeholder);

const getSubmitButton = (
  tree: renderer.ReactTestRenderer,
  label: string
) => {
  return tree.root
    .findAllByType(TouchableOpacity)
    .find((button) =>
      button.findAllByType(Text).some((text) => text.props.children === label)
    );
};

describe("InventoryItemDialog", () => {
  it("submits a new item and closes the dialog", () => {
    const { tree, props } = renderDialog();
    const nameInput = getInput(tree, "Name");
    const qtyInput = getInput(tree, "Quantity");
    const expiryInput = getInput(tree, "Expiry (days)");

    act(() => {
      nameInput?.props.onChangeText?.("Milk ");
      qtyInput?.props.onChangeText?.("2");
      expiryInput?.props.onChangeText?.("7");
    });

    const submitButton = getSubmitButton(tree, "Add");
    act(() => {
      submitButton?.props.onPress?.();
    });

    expect(props.onAdd).toHaveBeenCalledTimes(1);
    expect(props.onAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        name: "Milk",
        amount: "2",
        expiryDays: "7",
        unit: "g",
        storage: "RoomTemp",
      })
    );
    expect(props.onOpenChange).toHaveBeenCalledWith(false);
  });

  it("submits updates in edit mode", () => {
    const item: InventoryItemForm = {
      id: "item-1",
      name: "Eggs",
      amount: "6",
      unit: "pcs",
      storage: "Refrigerated",
      addedDate: "2025-01-01T00:00:00Z",
      expiryDays: "3",
    };

    const { tree, props } = renderDialog({ item });
    const submitButton = getSubmitButton(tree, "Update");

    act(() => {
      submitButton?.props.onPress?.();
    });

    expect(props.onUpdate).toHaveBeenCalledTimes(1);
    expect(props.onUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ id: "item-1", name: "Eggs" })
    );
    expect(props.onOpenChange).toHaveBeenCalledWith(false);
  });

  it("does not submit when required fields are invalid", () => {
    const { tree, props } = renderDialog();
    const submitButton = getSubmitButton(tree, "Add");

    act(() => {
      submitButton?.props.onPress?.();
    });

    expect(props.onAdd).not.toHaveBeenCalled();
  });
});
