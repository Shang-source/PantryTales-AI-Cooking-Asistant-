import renderer, { act } from "react-test-renderer";
import { TouchableOpacity, View, Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import Card, {
  CardHeader,
  CardContent,
  CardFooter,
  CardTitle,
  CardDescription,
  CardAction,
} from "../card";

// Mock NativeAnimatedHelper to avoid React Native internals failing in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Card", () => {
  it("renders static container styles when not pressable", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Card testID="card-root">
          <CardHeader testID="header" />
          <CardContent />
          <CardFooter />
        </Card>
      );
    });

    const cardHost = tree.root
      .findAllByType(View)
      .find((node) => node.props.testID === "card-root");

    expect(cardHost).toBeTruthy();
    // Background/border colors now applied via style prop using theme colors
    expect(cardHost?.props.className).toContain("rounded-xl");
    expect(cardHost?.props.style).toBeDefined();
  });

  it("renders a pressable card when onPress is provided", () => {
    const onPress = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Card testID="pressable-card" onPress={onPress}>
          <Text>Tap me</Text>
        </Card>
      );
    });

    const cardHost = tree.root
      .findAllByType(TouchableOpacity)
      .find((node) => node.props.testID === "pressable-card");

    expect(cardHost).toBeTruthy();
    expect(cardHost?.props.activeOpacity).toBe(0.8);

    act(() => {
      cardHost?.props.onPress?.();
    });

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it("composes subcomponents and merges class names", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Card>
          <CardHeader testID="header" className="custom-header">
            <Text>Header</Text>
          </CardHeader>
          <CardContent testID="content" className="custom-content">
            <CardTitle testID="title" className="text-primary-500">
              Title
            </CardTitle>
            <CardDescription testID="description" className="text-gray-400">
              Description
            </CardDescription>
          </CardContent>
          <CardFooter testID="footer" className="items-center">
            <CardAction testID="action" className="custom-action">
              <Text>Action</Text>
            </CardAction>
          </CardFooter>
        </Card>
      );
    });

    const header = tree.root.findByProps({ testID: "header" });
    expect(header.props.className).toContain("custom-header");

    const content = tree.root.findByProps({ testID: "content" });
    expect(content.props.className).toContain("custom-content");

    expect(tree.root.findByProps({ testID: "title" }).props.children).toBe("Title");
    expect(tree.root.findByProps({ testID: "description" }).props.children).toBe("Description");

    const action = tree.root.findByProps({ testID: "action" });
    expect(action.props.className).toContain("custom-action");
  });
});
