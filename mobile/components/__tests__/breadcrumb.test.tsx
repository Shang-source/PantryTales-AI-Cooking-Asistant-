import renderer, { act } from "react-test-renderer";
import { TouchableOpacity } from "react-native";
import { jest, describe, it, expect, afterEach } from "@jest/globals";

import {
  Breadcrumb,
  BreadcrumbList,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbPage,
  BreadcrumbSeparator,
  BreadcrumbEllipsis,
} from "../breadcrumb";

// Silence animation warnings
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock Feather icon to a simple Text placeholder
jest.mock("@expo/vector-icons", () => {
  const { Text } = require("react-native");
  return {
    Feather: ({ name, ...props }: any) => (
      <Text testID={`Feather-${name}`} {...props}>
        {name}
      </Text>
    ),
  };
});

afterEach(() => {
  jest.clearAllMocks();
});

describe("Breadcrumb components", () => {
  it("renders a breadcrumb trail with default separators and current page styling", () => {
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Breadcrumb className="custom-breadcrumb">
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink onPress={jest.fn()}>Home</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbLink>Recipes</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage testID="current-page">Detail</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
      );
    });

    const breadcrumb = tree.root.findByProps({ "aria-label": "breadcrumb" });
    expect(breadcrumb.props.className).toContain("custom-breadcrumb");

    const separators = tree.root.findAllByProps({
      testID: "Feather-chevron-right",
    });
    expect(separators.length).toBeGreaterThanOrEqual(2);

    const current = tree.root.findByProps({ testID: "current-page" });
    expect(current.props.children).toBe("Detail");
  });

  it("renders ellipsis with more-horizontal icon", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<BreadcrumbEllipsis />);
    });

    expect(
      tree.root.findAllByProps({ testID: "Feather-more-horizontal" }).length
    ).toBeGreaterThanOrEqual(1);
  });

  it("invokes onPress for breadcrumb link", () => {
    const onPress = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <BreadcrumbLink onPress={onPress}>Tap me</BreadcrumbLink>
      );
    });

    const link = tree.root.findByType(TouchableOpacity);
    act(() => {
      link.props.onPress?.();
    });

    expect(onPress).toHaveBeenCalledTimes(1);
    expect(link.props.activeOpacity).toBe(0.7);
  });
});
