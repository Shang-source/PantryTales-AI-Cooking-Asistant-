import React from "react";
import renderer, { act } from "react-test-renderer";
import { Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import {
  HoverCard,
  HoverCardTrigger,
  HoverCardContent,
} from "../hover-card";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("HoverCard", () => {
  it("throws when trigger is used outside of HoverCard root", () => {
    const renderOutside = () =>
      act(() => {
        renderer.create(
          <HoverCardTrigger>
            <Text>Standalone trigger</Text>
          </HoverCardTrigger>,
        );
      });

    expect(renderOutside).toThrowError(
      "HoverCard.* must be used inside <HoverCard>",
    );
  });

  it("toggles content visibility in response to trigger press events", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <HoverCard>
          <HoverCardTrigger>
            <Text>Open</Text>
          </HoverCardTrigger>
          <HoverCardContent>
            <Text testID="hover-card-body">Details</Text>
          </HoverCardContent>
        </HoverCard>,
      );
    });

    expect(() =>
      tree.root.findByProps({ "data-slot": "hover-card-content" }),
    ).toThrow();

    const trigger = tree.root.findByProps({
      "data-slot": "hover-card-trigger",
    });

    act(() => {
      trigger.props.onPressIn?.({} as any);
    });

    const content = tree.root.findByProps({
      "data-slot": "hover-card-content",
    });
    expect(content.findByProps({ testID: "hover-card-body" })).toBeTruthy();

    act(() => {
      trigger.props.onPressOut?.({} as any);
    });

    expect(() =>
      tree.root.findByProps({ "data-slot": "hover-card-content" }),
    ).toThrow();
  });

  it("applies alignment classes and side offset styles", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <HoverCard defaultOpen>
          <HoverCardContent
            align="left"
            sideOffset={12}
            className="extra"
            style={{ backgroundColor: "pink" }}
          >
            <Text>Aligned content</Text>
          </HoverCardContent>
        </HoverCard>,
      );
    });

    const content = tree.root.findByProps({
      "data-slot": "hover-card-content",
    });

    expect(content.props.className).toContain("self-start");
    expect(content.props.className).toContain("extra");

    const [offsetStyle, customStyle] = content.props.style;
    expect(offsetStyle).toMatchObject({ marginTop: 12 });
    expect(customStyle).toMatchObject({ backgroundColor: "pink" });
  });
});
