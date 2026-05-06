import renderer, { act } from "react-test-renderer";
import { Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import BadgeComponent, { badgeVariants, Badge as NamedBadge } from "../badge";

// Silence RN animation warnings in Jest.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

describe("badgeVariants", () => {
  it("returns combined classes per variant", () => {
    expect(badgeVariants("default")).toContain(
      "flex flex-row items-center justify-center rounded-md border px-2 py-0.5 text-xs font-medium w-fit whitespace-nowrap shrink-0 gap-1 overflow-hidden"
    );
    expect(badgeVariants("default")).toContain(
      "border-transparent bg-zinc-900 text-zinc-50"
    );
    expect(badgeVariants("secondary")).toContain(
      "border-transparent bg-zinc-100 text-zinc-900"
    );
    expect(badgeVariants("destructive")).toContain(
      "border-transparent bg-red-600 text-white"
    );
    expect(badgeVariants("outline")).toContain(
      "border-zinc-300 bg-transparent text-zinc-900"
    );
  });
});

describe("Badge component", () => {
  it("wraps plain text children into Text nodes with default styles and custom textClassName", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <BadgeComponent textClassName="custom-text">Hello badge</BadgeComponent>
      );
    });

    const badge = tree.root.findByProps({ "data-slot": "badge" });
    expect(badge.props.className).toContain("bg-zinc-900");

    const texts = badge.findAllByType(Text);
    expect(texts).toHaveLength(1);
    expect(texts[0].props.className).toContain("text-xs font-medium");
    expect(texts[0].props.className).toContain("custom-text");
    expect(texts[0].props.children).toBe("Hello badge");
  });

  it("keeps non-text children intact and preserves order alongside text segments", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <NamedBadge variant="outline" className="extra">
          Leading
          <Text testID="icon">★</Text>
          Trailing
        </NamedBadge>
      );
    });

    const badge = tree.root.findByProps({ "data-slot": "badge" });
    expect(badge.props.className).toContain("border-zinc-300");
    expect(badge.props.className).toContain("extra");

    const directChildren = badge.props.children;
    expect(directChildren).toHaveLength(3);
    expect(directChildren[0].props.children).toBe("Leading");
    expect(directChildren[1].props.children).toBe("★");
    expect(directChildren[2].props.children).toBe("Trailing");
  });
});
