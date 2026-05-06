import type { ComponentProps } from "react";
import renderer, { act } from "react-test-renderer";
import { TouchableOpacity, Text } from "react-native";
import { jest, describe, it, expect } from "@jest/globals";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogFooter,
  AlertDialogTrigger,
} from "../alert-dialog";

// Silence React Native animation warnings.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

const findTextButton = (tree: renderer.ReactTestRenderer, label: string) =>
  tree
    .root
    .findAllByType(TouchableOpacity)
    .find((node) =>
      node.findAllByType(Text).some((textNode) => textNode.props.children === label)
    );

const findHostByTestId = (
  root: renderer.ReactTestInstance,
  testID: string
) =>
  root.findAll(
    (node) => node.props?.testID === testID && typeof node.type === "string"
  );

const renderDialog = (props?: Partial<ComponentProps<typeof AlertDialog>>) => {
  const onOpenChange = jest.fn();
  let tree!: renderer.ReactTestRenderer;

  act(() => {
    tree = renderer.create(
      <AlertDialog {...props} onOpenChange={onOpenChange}>
        <AlertDialogTrigger>
          <TouchableOpacity testID="trigger">
            <Text>Open dialog</Text>
          </TouchableOpacity>
        </AlertDialogTrigger>

        <AlertDialogContent>
          <Text testID="dialog-content">Dialog body</Text>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction>Confirm</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    );
  });

  return { tree, onOpenChange };
};

describe("AlertDialog", () => {
  it("opens via trigger and closes via actions in uncontrolled mode", () => {
    const { tree, onOpenChange } = renderDialog({ defaultOpen: false });

    expect(findHostByTestId(tree.root, "dialog-content").length).toBe(0);

    const trigger = tree.root.findByProps({ testID: "trigger" });
    act(() => {
      trigger.props.onPress();
    });

    expect(findHostByTestId(tree.root, "dialog-content").length).toBe(1);
    expect(onOpenChange).toHaveBeenLastCalledWith(true);

    const confirm = findTextButton(tree, "Confirm");
    expect(confirm).toBeDefined();

    act(() => {
      confirm?.props.onPress();
    });

    expect(findHostByTestId(tree.root, "dialog-content").length).toBe(0);
    expect(onOpenChange).toHaveBeenLastCalledWith(false);
  });

  it("fires onOpenChange in controlled mode without closing content", () => {
    const { tree, onOpenChange } = renderDialog({ open: true });

    expect(findHostByTestId(tree.root, "dialog-content").length).toBe(1);

    const cancel = findTextButton(tree, "Cancel");
    expect(cancel).toBeDefined();

    act(() => {
      cancel?.props.onPress();
    });

    expect(onOpenChange).toHaveBeenLastCalledWith(false);
    // Controlled: content remains visible until parent changes `open`.
    expect(findHostByTestId(tree.root, "dialog-content").length).toBe(1);
  });
});
