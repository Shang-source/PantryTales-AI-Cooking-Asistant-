import renderer, { act } from "react-test-renderer";
import { describe, it, expect, jest } from "@jest/globals";
import { Text, TouchableOpacity, View } from "react-native";
import {
  Pagination,
  PaginationContent,
  PaginationLink,
  PaginationPrevious,
  PaginationNext,
  PaginationEllipsis,
} from "../pagination";
import { ChevronLeft } from "lucide-react-native";

// Silence native animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock lucide icons to simple host components.
jest.mock("lucide-react-native", () => ({
  ChevronLeft: "ChevronLeft",
  ChevronRight: "ChevronRight",
  MoreHorizontal: "MoreHorizontal",
}));

const findButtonByText = (
  tree: renderer.ReactTestRenderer,
  text: string
): renderer.ReactTestInstance | undefined =>
  tree.root
    .findAllByType(TouchableOpacity)
    .find((btn) =>
      btn.findAllByType(Text).some((t) => t.props.children === text)
    );

describe("Pagination", () => {
  it("renders navigation container and numbered links", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Pagination>
          <PaginationContent>
            <PaginationPrevious />
            <PaginationLink>1</PaginationLink>
            <PaginationLink isActive>2</PaginationLink>
            <PaginationLink>3</PaginationLink>
            <PaginationNext />
          </PaginationContent>
        </Pagination>
      );
    });

    const navView = tree.root.findAllByType(View)[0];
    expect(navView.props.role || navView.props.accessibilityRole).toBe(
      "navigation"
    );
    expect(
      navView.props["aria-label"] || navView.props.accessibilityLabel
    ).toBe("pagination");

    expect(findButtonByText(tree, "1")).toBeDefined();
    expect(findButtonByText(tree, "2")).toBeDefined();
    expect(findButtonByText(tree, "3")).toBeDefined();
  });

  it("marks active page via accessibilityState and styles", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<PaginationLink isActive>5</PaginationLink>);
    });

    const btn = tree.root.findByType(TouchableOpacity);
    expect(btn.props.accessibilityState).toEqual({ selected: true });
    expect(btn.props.className).toContain("border");
  });

  it("renders Previous/Next buttons with labels and icons", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <PaginationContent>
          <PaginationPrevious />
          <PaginationNext />
        </PaginationContent>
      );
    });

    expect(findButtonByText(tree, "Previous")).toBeDefined();
    expect(findButtonByText(tree, "Next")).toBeDefined();
    const leftIcons = tree.root.findAllByType(ChevronLeft);
    expect(leftIcons.length).toBeGreaterThan(0);
  });

  it("renders ellipsis placeholder", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<PaginationEllipsis />);
    });

    const srOnlyText = tree.root.findByProps({ children: "More pages" });
    expect(srOnlyText).toBeDefined();
  });
});
