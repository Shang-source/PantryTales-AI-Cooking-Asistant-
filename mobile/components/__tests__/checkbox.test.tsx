import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect } from "@jest/globals";
import { Checkbox } from "../checkbox";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("lucide-react-native", () => ({
  Check: "IconCheck",
}));

describe("Checkbox", () => {
  it("renders unchecked state with default styles and toggles to checked on press", () => {
    const onChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Checkbox checked={false} onChange={onChange} className="extra" />
      );
    });

    const pressable = tree.root.findByProps({ testID: "checkbox-button" });

    expect(pressable.props.className).toContain("w-5 h-5");
    // Background is now applied via style prop using theme colors
    expect(pressable.props.style).toBeDefined();

    const icons = tree.root.findAll(
      (node) => (node.type as any) === "IconCheck"
    );
    expect(icons.length).toBe(0);

    act(() => {
      pressable.props.onPress?.({} as any);
    });

    expect(onChange).toHaveBeenCalledWith(true);
  });

  it("renders checked state with icon and toggles off when pressed", () => {
    const onChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(<Checkbox checked onChange={onChange} />);
    });

    const pressable = tree.root.findByProps({ testID: "checkbox-button" });
    // Background is now applied via style prop using theme colors
    expect(pressable.props.style).toBeDefined();

    const icons = tree.root.findAll(
      (node) => (node.type as any) === "IconCheck"
    );
    expect(icons.length).toBe(1);

    act(() => {
      pressable.props.onPress?.({} as any);
    });

    expect(onChange).toHaveBeenCalledWith(false);
  });

  it("does not trigger onChange when disabled", () => {
    const onChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <Checkbox checked={false} onChange={onChange} disabled />
      );
    });

    const pressable = tree.root.findByProps({ testID: "checkbox-button" });
    expect(pressable.props.className).toContain("opacity-50");

    act(() => {
      pressable.props.onPress?.({} as any);
    });

    expect(onChange).not.toHaveBeenCalled();
  });
});
