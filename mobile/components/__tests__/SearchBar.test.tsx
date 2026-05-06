import renderer, { act } from "react-test-renderer";
import { View } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import { SearchBar } from "../SearchBar";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("@expo/vector-icons", () => ({
  Feather: "FeatherIcon",
}));

describe("SearchBar", () => {
  it("renders wrapper, icon, and input with default props", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<SearchBar />);
    });

    const views = tree.root.findAllByType(View);
    // Wrapper now has different classes with rounded-full border on wrapper
    const wrapper = views.find((v) =>
      (v.props.className as string)?.includes("w-full") &&
      (v.props.className as string)?.includes("rounded-full")
    );
    expect(wrapper).toBeDefined();

    const icons = tree.root.findAll(
      (node) => (node.type as any) === "FeatherIcon"
    );
    expect(icons.length).toBe(1);

    const input = tree.root.findByProps({ "data-slot": "search" });
    expect(input.props.placeholder).toBe("Search");
    expect(input.props.autoCapitalize).toBe("none");
    expect(input.props.autoCorrect).toBe(false);
    // Input className no longer has border styles - they're on wrapper now
    expect(input.props.className).toContain("flex-1");
  });

  it("merges custom className and forwards props to input", () => {
    const onChangeText = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <SearchBar
          className="mt-4"
          inputClassName="bg-red-500"
          placeholder="Find items"
          onChangeText={onChangeText}
          value=""
        />
      );
    });

    const wrapper = tree.root
      .findAllByType(View)
      .find((v) => (v.props.className as string).includes("mt-4"));
    expect(wrapper).toBeDefined();

    const input = tree.root.findByProps({ "data-slot": "search" });
    expect(input.props.placeholder).toBe("Find items");
    expect(input.props.className).toContain("bg-red-500");

    act(() => {
      input.props.onChangeText?.("abc");
    });
    expect(onChangeText).toHaveBeenCalledWith("abc");
  });
});
