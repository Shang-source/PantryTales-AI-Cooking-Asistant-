import renderer, { act } from "react-test-renderer";
import { View } from "react-native";
import { describe, it, expect } from "@jest/globals";
import { Separator } from "../separator";

describe("Separator", () => {
  it("renders a horizontal decorative separator by default", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Separator testID="separator" />);
    });

    const separator = tree.root.findByType(View);

    expect(separator.props.accessibilityRole).toBe("none");
    expect(separator.props.className).toContain("shrink-0");
    expect(separator.props.className).toContain("bg-border");
    expect(separator.props.className).toContain("h-[1px]");
    expect(separator.props.className).toContain("w-full");
  });

  it("renders a vertical non-decorative separator and merges className", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Separator
          orientation="vertical"
          decorative={false}
          className="my-2"
        />
      );
    });

    const separator = tree.root.findByType(View);

    expect(separator.props.accessibilityRole).toBe("summary");
    expect(separator.props.className).toContain("h-full");
    expect(separator.props.className).toContain("w-[1px]");
    expect(separator.props.className).toContain("my-2");
  });
});
