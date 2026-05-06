import renderer, { act } from "react-test-renderer";
import { Text, TouchableOpacity } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import { ChecklistCard } from "../checklist/ChecklistCard";
import type { ChecklistItemDto } from "@/hooks/useChecklist";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("lucide-react-native", () => {
  const { Text } = require("react-native");
  const make = (name: string) => {
    const Comp = (props: any) => <Text testID={name} {...props} />;
    Comp.displayName = name;
    return Comp;
  };
  return {
    Check: make("Check"),
    Pencil: make("Pencil"),
    Trash2: make("Trash2"),
  };
});

type RenderCardOverrides = Omit<Partial<React.ComponentProps<typeof ChecklistCard>>, 'item'> & {
  item?: Partial<ChecklistItemDto>;
};

const renderCard = (overrides?: RenderCardOverrides) => {
  const item: ChecklistItemDto = {
    id: "item-1",
    name: "Tomatoes",
    amount: 2,
    unit: "pcs",
    category: "Produce",
    isChecked: false,
    createdAt: "2025-01-01T00:00:00Z",
    updatedAt: "2025-01-01T00:00:00Z",
    ...(overrides?.item ?? {}),
  };

  const { item: _itemOverride, ...restOverrides } = overrides ?? {};
  const props = {
    item,
    onToggle: jest.fn(),
    onEdit: jest.fn(),
    onDelete: jest.fn(),
    ...restOverrides,
  };

  let tree!: renderer.ReactTestRenderer;
  act(() => {
    tree = renderer.create(<ChecklistCard {...props} />);
  });

  return { tree, props };
};

describe("ChecklistCard", () => {
  it("renders name and quantity", () => {
    const { tree } = renderCard();

    expect(tree.root.findByProps({ children: "Tomatoes" })).toBeTruthy();
    const quantityText = tree.root.findAllByType(Text).find((t) => {
      const children = t.props.children;
      if (Array.isArray(children)) {
        return children.join("") === "2 pcs";
      }
      return children === "2 pcs";
    });
    expect(quantityText).toBeTruthy();
  });

  it("calls onToggle when tapping the card", () => {
    const { tree, props } = renderCard();
    const touchable = tree.root.findByType(TouchableOpacity);

    act(() => {
      touchable.props.onPress?.();
    });

    expect(props.onToggle).toHaveBeenCalledTimes(1);
    expect(props.onToggle).toHaveBeenCalledWith(props.item);
  });

  it("calls onEdit and onDelete from action buttons", () => {
    const { tree, props } = renderCard();
    const editButton = tree.root.findByProps({ testID: "edit-button" });
    const deleteButton = tree.root.findByProps({ testID: "delete-button" });

    expect(editButton).toBeDefined();
    expect(deleteButton).toBeDefined();

    act(() => {
      editButton.props.onPress?.();
      deleteButton.props.onPress?.();
    });

    expect(props.onEdit).toHaveBeenCalledTimes(1);
    expect(props.onEdit).toHaveBeenCalledWith(props.item);
    expect(props.onDelete).toHaveBeenCalledTimes(1);
    expect(props.onDelete).toHaveBeenCalledWith("item-1");
  });

  it("shows check icon and disables edit when checked", () => {
    const { tree } = renderCard({ item: { isChecked: true } });
    const checkIcon = tree.root.findByProps({ testID: "Check" });
    const editButton = tree.root.findByProps({ testID: "edit-button" });

    expect(checkIcon).toBeTruthy();
    expect(editButton.props.disabled).toBe(true);
  });
});
