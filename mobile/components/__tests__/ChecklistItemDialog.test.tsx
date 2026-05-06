import renderer, { act } from "react-test-renderer";
import { Text, TextInput, TouchableOpacity } from "react-native";
import { jest, describe, it, expect, beforeEach } from "@jest/globals";

import { ChecklistItemDialog } from "../checklist/ChecklistItemDialog";
import { useAddChecklistItem, useUpdateChecklistItem } from "@/hooks/useChecklist";
import type { ChecklistItemDto } from "@/hooks/useChecklist";

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

jest.mock("@/hooks/useChecklist", () => ({
  useAddChecklistItem: jest.fn(),
  useUpdateChecklistItem: jest.fn(),
}));

const mockUseAddChecklistItem = useAddChecklistItem as jest.Mock;
const mockUseUpdateChecklistItem = useUpdateChecklistItem as jest.Mock;

const renderDialog = (
  overrides?: Partial<React.ComponentProps<typeof ChecklistItemDialog>>
) => {
  const props = {
    open: true,
    onOpenChange: jest.fn(),
    item: null as ChecklistItemDto | null,
    ...overrides,
  };

  let tree!: renderer.ReactTestRenderer;
  act(() => {
    tree = renderer.create(<ChecklistItemDialog {...props} />);
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

describe("ChecklistItemDialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("submits a new item and closes the dialog", () => {
    const addMutate = jest.fn((_data: unknown, options?: { onSuccess?: () => void }) => {
      options?.onSuccess?.();
    });
    mockUseAddChecklistItem.mockReturnValue({
      mutate: addMutate,
      isPending: false,
    });
    mockUseUpdateChecklistItem.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
    });

    const { tree, props } = renderDialog();
    const nameInput = getInput(tree, "Name");
    const qtyInput = getInput(tree, "Quantity");

    act(() => {
      nameInput?.props.onChangeText?.("Milk");
      qtyInput?.props.onChangeText?.("2");
    });

    const submitButton = getSubmitButton(tree, "Add");
    act(() => {
      submitButton?.props.onPress?.();
    });

    expect(addMutate).toHaveBeenCalledTimes(1);
    expect(addMutate).toHaveBeenCalledWith(
      {
        name: "Milk",
        amount: 2,
        unit: "g",
        category: "Vegetables",
      },
      expect.any(Object)
    );
    expect(props.onOpenChange).toHaveBeenCalledWith(false);
  });

  it("submits updates in edit mode", () => {
    const updateMutate = jest.fn((_data: unknown, options?: { onSuccess?: () => void }) => {
      options?.onSuccess?.();
    });
    mockUseAddChecklistItem.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
    });
    mockUseUpdateChecklistItem.mockReturnValue({
      mutate: updateMutate,
      isPending: false,
    });

    const item: ChecklistItemDto = {
      id: "item-1",
      name: "Eggs",
      amount: 6,
      unit: "pcs",
      category: "Dairy",
      isChecked: false,
      createdAt: "2025-01-01T00:00:00Z",
      updatedAt: "2025-01-01T00:00:00Z",
    };
    const { tree, props } = renderDialog({ item });

    const submitButton = getSubmitButton(tree, "Update");
    act(() => {
      submitButton?.props.onPress?.();
    });

    expect(updateMutate).toHaveBeenCalledTimes(1);
    expect(updateMutate).toHaveBeenCalledWith(
      {
        id: "item-1",
        data: {
          name: "Eggs",
          amount: 6,
          unit: "pcs",
          category: "Dairy",
        },
      },
      expect.any(Object)
    );
    expect(props.onOpenChange).toHaveBeenCalledWith(false);
  });

  it("does not submit when required fields are invalid", () => {
    const addMutate = jest.fn();
    mockUseAddChecklistItem.mockReturnValue({
      mutate: addMutate,
      isPending: false,
    });
    mockUseUpdateChecklistItem.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
    });

    const { tree } = renderDialog();
    const submitButton = getSubmitButton(tree, "Add");
    act(() => {
      submitButton?.props.onPress?.();
    });

    expect(addMutate).not.toHaveBeenCalled();
  });
});
