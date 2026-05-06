import renderer, { act } from "react-test-renderer";
import { describe, it, expect } from "@jest/globals";
import { View } from "react-native";
import { Progress } from "../progress";

describe("Progress", () => {
  it("renders track and indicator with clamped width", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Progress value={150} />);
    });

    const views = tree.root.findAllByType(View);
    expect(views.length).toBe(2);

    const track = views[0];
    const indicator = views[1];

    // Colors are now applied via style prop using theme colors
    expect(track.props.style).toBeDefined();
    expect(indicator.props.style).toBeDefined();
    // Check width is clamped (style is an array now)
    const indicatorStyles = Array.isArray(indicator.props.style)
      ? indicator.props.style[0]
      : indicator.props.style;
    expect(indicatorStyles.width).toBe("100%"); // clamped from 150 to 100
  });

  it("applies custom classNames to track and indicator", () => {
    const trackClass = "custom-track";
    const indicatorClass = "custom-indicator";

    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Progress
          value={40}
          className="outer"
          trackClassName={trackClass}
          indicatorClassName={indicatorClass}
        />
      );
    });

    const views = tree.root.findAllByType(View);
    expect(views[0].props.className).toContain(trackClass);
    expect(views[1].props.className).toContain(indicatorClass);
    // Style is now an array, check first element for width
    const indicatorStyles = Array.isArray(views[1].props.style)
      ? views[1].props.style[0]
      : views[1].props.style;
    expect(indicatorStyles.width).toBe("40%");
  });
});
