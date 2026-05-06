import renderer, { act } from "react-test-renderer";
import { Alert, AlertDescription, AlertTitle } from "../alert";
import { jest, describe, it, expect } from "@jest/globals";

// Mock NativeAnimatedHelper to avoid React Native internals failing in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Alert", () => {
  it("applies default variant classes and passes through props", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Alert className="extra" accessibilityRole="alert" />
      );
    });

    const alert = tree.root.findByProps({ "data-slot": "alert" });

    expect(alert.props.className).toContain(
      "relative w-full rounded-lg border px-4 py-3"
    );
    expect(alert.props.className).toContain(
      "bg-white border-zinc-200 text-zinc-900"
    );
    expect(alert.props.className).toContain("extra");
    expect(alert.props.accessibilityRole).toBe("alert");
  });

  it("uses destructive variant styles when requested", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<Alert variant="destructive" />);
    });

    const alert = tree.root.findByProps({ "data-slot": "alert" });

    expect(alert.props.className).toContain(
      "bg-red-50 border-red-200 text-red-700"
    );
    expect(alert.props.className).toContain(
      "relative w-full rounded-lg border px-4 py-3"
    );
  });
});

describe("Alert text helpers", () => {
  it("renders AlertTitle with default styles and custom className", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <AlertTitle className="title-extra">Title text</AlertTitle>
      );
    });

    const title = tree.root.findByProps({ "data-slot": "alert-title" });

    expect(title.props.className).toContain(
      "text-sm font-medium tracking-tight"
    );
    expect(title.props.className).toContain("title-extra");
    expect(title.props.children).toBe("Title text");
  });

  it("renders AlertDescription with default styles and custom className", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <AlertDescription className="desc-extra">
          Description text
        </AlertDescription>
      );
    });

    const description = tree.root.findByProps({
      "data-slot": "alert-description",
    });

    expect(description.props.className).toContain(
      "mt-1 text-xs leading-relaxed text-zinc-600"
    );
    expect(description.props.className).toContain("desc-extra");
    expect(description.props.children).toBe("Description text");
  });
});
