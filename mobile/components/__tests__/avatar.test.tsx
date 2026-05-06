import renderer, { act } from "react-test-renderer";
import { Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import { Avatar, AvatarFallback, AvatarImage } from "../avatar";

// Silence React Native animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("Avatar", () => {
  it("renders with base styles and merges className and props", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Avatar className="extra" accessibilityLabel="avatar-root" />
      );
    });

    const avatar = tree.root.findByProps({ "data-slot": "avatar" });
    expect(avatar.props.className).toContain(
      "relative h-10 w-10 shrink-0 overflow-hidden rounded-full"
    );
    expect(avatar.props.className).toContain("extra");
    expect(avatar.props.accessibilityLabel).toBe("avatar-root");
  });

  it("renders fallback text when children is string", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Avatar>
          <AvatarFallback className="bg-extra" textClassName="txt-extra">
            AB
          </AvatarFallback>
        </Avatar>
      );
    });

    const fallback = tree.root.findByProps({ "data-slot": "avatar-fallback" });
    expect(fallback.props.className).toContain(
      "h-full w-full items-center justify-center rounded-full"
    );
    expect(fallback.props.className).toContain("bg-extra");

    const textNode = fallback.findByType(Text);
    expect(textNode.props.children).toBe("AB");
  });
});

describe("AvatarImage", () => {
  it("hides image after error event", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Avatar>
          <AvatarImage source={{ uri: "https://example.com/avatar.png" }} />
        </Avatar>
      );
    });

    const image = tree.root.findByProps({ "data-slot": "avatar-image" });
    expect(image.props.className).toContain("h-full w-full");

    act(() => {
      image.props.onError?.({} as any);
    });

    expect(
      tree.root.findAllByProps({ "data-slot": "avatar-image" }).length
    ).toBe(0);
  });
});
