import renderer, { act } from "react-test-renderer";
import { Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";

import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuCheckboxItem,
} from "../dropdown-menu";
import { CheckIcon } from "lucide-react-native";

jest.mock(
  "react-native/Libraries/Animated/NativeAnimatedHelper",
  () => ({}),
  {
    virtual: true,
  }
);

describe("DropdownMenu", () => {
  it("toggles content visibility when trigger is pressed", () => {
    const onPress = jest.fn();
    let tree!: renderer.ReactTestRenderer;

    act(() => {
      tree = renderer.create(
        <DropdownMenu defaultOpen>
          <DropdownMenuTrigger onPress={onPress}>
            <Text>Toggle Menu</Text>
          </DropdownMenuTrigger>
          <DropdownMenuContent>
            <Text testID="menu-item">Item</Text>
          </DropdownMenuContent>
        </DropdownMenu>
      );
    });

    const trigger = tree.root.findByProps({
      "data-slot": "dropdown-menu-trigger",
    });

    act(() => {
      trigger.props.onPress?.({} as any);
    });

    const content = tree.root.findByProps({ testID: "menu-item" });
    expect(content).toBeTruthy();
    expect(onPress).toHaveBeenCalled();
  });

  it("sets inset and variant props on DropdownMenuItem", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <DropdownMenu>
          <DropdownMenuItem inset variant="destructive">
            <Text>Delete</Text>
          </DropdownMenuItem>
        </DropdownMenu>
      );
    });

    const item = tree.root.findByProps({
      "data-slot": "dropdown-menu-item",
    });
    expect(item.props["data-inset"]).toBe(true);
    expect(item.props["data-variant"]).toBe("destructive");
    expect(item.props.className).toContain("data-[variant=destructive]");
  });

  it("shows a check icon for checked checkbox items", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <DropdownMenuCheckboxItem checked>
          <Text>Airplane mode</Text>
        </DropdownMenuCheckboxItem>
      );
    });

    const icons = tree.root.findAllByType(CheckIcon);
    expect(icons.length).toBe(1);

    act(() => {
      tree.update(
        <DropdownMenuCheckboxItem>
          <Text>Airplane mode</Text>
        </DropdownMenuCheckboxItem>
      );
    });

    const iconsWhenUnchecked = tree.root.findAllByType(CheckIcon);
    expect(iconsWhenUnchecked.length).toBe(0);
  });
});
