import renderer, { act } from "react-test-renderer";
import { jest, describe, it, expect, afterEach } from "@jest/globals";
import { Text, TextInput } from "react-native";
import {
  Command,
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
} from "../command";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

jest.mock("lucide-react-native", () => ({
  Search: "SearchIcon",
}));

const hasText = (
  tree: renderer.ReactTestRenderer,
  value: string
): boolean =>
  tree.root
    .findAllByType(Text)
    .some((node) => node.props.children === value);

const findByClassSubstring = (
  tree: renderer.ReactTestRenderer,
  token: string
) =>
  tree.root.findAll(
    (node) =>
      typeof node.props?.className === "string" &&
      node.props.className.includes(token)
  );

describe("Command", () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("filters items based on search text from CommandInput", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Command>
          <CommandInput placeholder="Search commands" />
          <CommandList>
            <CommandItem value="apple">Apple</CommandItem>
            <CommandItem value="banana">Banana</CommandItem>
          </CommandList>
        </Command>
      );
    });

    expect(hasText(tree, "Apple")).toBe(true);
    expect(hasText(tree, "Banana")).toBe(true);

    const input = tree.root.findByType(TextInput);

    act(() => {
      input.props.onChangeText?.("ban");
    });

    expect(hasText(tree, "Apple")).toBe(false);
    expect(hasText(tree, "Banana")).toBe(true);
  });

  it("closes the dialog when pressing the overlay but not when pressing content", () => {
    const onOpenChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <CommandDialog open onOpenChange={onOpenChange}>
          <CommandInput placeholder="Filter" />
        </CommandDialog>
      );
    });

    const overlay = findByClassSubstring(tree, "bg-black/50")[0];
    expect(overlay).toBeDefined();

    act(() => {
      overlay.props.onPress?.();
    });

    expect(onOpenChange).toHaveBeenCalledWith(false);

    const panel = findByClassSubstring(tree, "max-h-[80%]")[0];
    act(() => {
      panel.props.onPress?.({ stopPropagation: jest.fn() });
    });

    expect(onOpenChange).toHaveBeenCalledTimes(1);
  });

  it("renders group headings and empty state text", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Command>
          <CommandGroup heading="Shortcuts">
            <CommandEmpty>No results</CommandEmpty>
          </CommandGroup>
        </Command>
      );
    });

    expect(hasText(tree, "Shortcuts")).toBe(true);
    expect(hasText(tree, "No results")).toBe(true);
  });
});
