import renderer, { act } from "react-test-renderer";
import { Image, Text, TouchableOpacity } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import { CookedRecipeCard } from "../cookinghistory/CookedRecipeCard";
import type { MyCookedRecipeCardDto } from "@/types/recipes";

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
  return { Trash2: make("Trash2") };
});

jest.mock("react-native-vector-icons/Feather", () => {
  const { Text } = require("react-native");
  return (props: any) => (
    <Text testID={`feather-${props.name}`} {...props} />
  );
});

jest.mock("@expo/vector-icons/build/MaterialCommunityIcons", () => {
  const { Text } = require("react-native");
  return (props: any) => (
    <Text testID="material-community-icon" {...props} />
  );
});

type RenderCardOverrides = Omit<Partial<React.ComponentProps<typeof CookedRecipeCard>>, 'item'> & {
  item?: Partial<MyCookedRecipeCardDto>;
};

const renderCard = (overrides?: RenderCardOverrides) => {
  const item: MyCookedRecipeCardDto = {
    cookId: "cook-1",
    id: "recipe-1",
    title: "Chicken Soup",
    authorName: "Test Author",
    coverImageUrl: null,
    cookCount: 2,
    lastCookedAt: "2025-01-01T12:00:00Z",
    firstCookedAt: "2025-01-01T12:00:00Z",
    ...(overrides?.item ?? {}),
  };

  const { item: _itemOverride, ...restOverrides } = overrides ?? {};
  const props = {
    item,
    onPress: jest.fn(),
    onDelete: jest.fn(),
    ...restOverrides,
  };

  let tree!: renderer.ReactTestRenderer;
  act(() => {
    tree = renderer.create(<CookedRecipeCard {...props} />);
  });

  return { tree, props };
};

const findTouchableFromIcon = (
  tree: renderer.ReactTestRenderer,
  testID: string
) => {
  let node: renderer.ReactTestInstance | null =
    tree.root.findByProps({ testID });
  while (node && node.type !== TouchableOpacity) {
    node = node.parent;
  }
  return node;
};

describe("CookedRecipeCard", () => {
  it("renders title, cooked count, and formatted date", () => {
    const { tree } = renderCard();
    const expectedDate = new Date("2025-01-01T12:00:00Z").toLocaleDateString(
      "en-US",
      { month: "short", day: "numeric", year: "numeric" }
    );

    expect(tree.root.findByProps({ children: "Chicken Soup" })).toBeTruthy();
    const cookedText = tree.root.findAllByType(Text).find((t) => {
      const children = t.props.children;
      if (Array.isArray(children)) {
        return children.join("") === "Cooked 2x";
      }
      return children === "Cooked 2x";
    });
    expect(cookedText).toBeTruthy();
    expect(tree.root.findByProps({ children: expectedDate })).toBeTruthy();
  });

  it("calls onPress when tapping the card", () => {
    const { tree, props } = renderCard();
    const outer = tree.root
      .findAllByType(TouchableOpacity)
      .find((node) => node.props.activeOpacity === 0.7);

    expect(outer).toBeDefined();
    act(() => {
      outer?.props.onPress?.();
    });

    expect(props.onPress).toHaveBeenCalledTimes(1);
  });

  it("calls onDelete when tapping the trash button", () => {
    const { tree, props } = renderCard();
    const deleteButton = findTouchableFromIcon(tree, "Trash2");

    expect(deleteButton).toBeDefined();
    act(() => {
      deleteButton?.props.onPress?.();
    });

    expect(props.onDelete).toHaveBeenCalledTimes(1);
  });

  it("renders the image when coverImageUrl is provided", () => {
    const { tree } = renderCard({
      item: { coverImageUrl: "https://example.com/cover.jpg" },
    });

    expect(tree.root.findAllByType(Image).length).toBe(1);
    expect(
      tree.root.findAllByProps({ testID: "material-community-icon" }).length
    ).toBe(0);
  });

  it("renders the fallback icon when coverImageUrl is missing", () => {
    const { tree } = renderCard({ item: { coverImageUrl: "" } });

    expect(
      tree.root.findAllByProps({ testID: "material-community-icon" }).length
    ).toBeGreaterThan(0);
    expect(tree.root.findAllByType(Image).length).toBe(0);
  });
});
