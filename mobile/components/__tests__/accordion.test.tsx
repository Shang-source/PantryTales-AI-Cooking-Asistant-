import type { ComponentProps } from "react";
import renderer, { act } from "react-test-renderer";
import { TouchableOpacity, Text, LayoutAnimation } from "react-native";
import { jest, describe, it, expect, beforeEach, afterAll } from "@jest/globals";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "../accordion";

// Silence warnings from React Native internals during tests.
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Stub out animations so they don't run in Jest.
const configureNextSpy = jest
  .spyOn(LayoutAnimation, "configureNext")
  .mockImplementation(() => {});

afterAll(() => {
  configureNextSpy.mockRestore();
});

const findHostByTestId = (
  root: renderer.ReactTestInstance,
  testID: string
) =>
  root.findAll(
    (node) => node.props?.testID === testID && typeof node.type === "string"
  );

describe("Accordion - single mode", () => {
  const renderAccordion = (props?: Partial<ComponentProps<typeof Accordion>>) => {
    let rendered!: renderer.ReactTestRenderer;
    act(() => {
      rendered = renderer.create(
        <Accordion type="single" {...props}>
          <AccordionItem value="one">
            <AccordionTrigger>First</AccordionTrigger>
            <AccordionContent>
              <Text testID="content-one">One</Text>
            </AccordionContent>
          </AccordionItem>
          <AccordionItem value="two">
            <AccordionTrigger>Second</AccordionTrigger>
            <AccordionContent>
              <Text testID="content-two">Two</Text>
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      );
    });
    return rendered;
  };

  beforeEach(() => {
    configureNextSpy.mockClear();
  });

  it("shows the default open item and switches to another item when pressed", () => {
    const onValueChange = jest.fn();
    const tree = renderAccordion({ defaultValue: "one", onValueChange });

    const contentOneNodes = findHostByTestId(tree.root, "content-one");
    const contentTwoNodes = findHostByTestId(tree.root, "content-two");

    expect(contentOneNodes.length).toBe(1);
    expect(contentTwoNodes.length).toBe(0);

    const triggers = tree.root.findAllByType(TouchableOpacity);

    act(() => {
      triggers[1].props.onPress();
    });

    expect(findHostByTestId(tree.root, "content-one").length).toBe(0);
    expect(findHostByTestId(tree.root, "content-two").length).toBe(1);
    expect(onValueChange).toHaveBeenLastCalledWith("two");
    expect(configureNextSpy).toHaveBeenCalled();

    act(() => {
      triggers[1].props.onPress();
    });

    expect(findHostByTestId(tree.root, "content-two").length).toBe(1);
    expect(onValueChange).toHaveBeenCalledTimes(1);
  });

  it("allows collapsing the open item when collapsible is true", () => {
    const onValueChange = jest.fn();
    const tree = renderAccordion({ defaultValue: "one", collapsible: true, onValueChange });
    const trigger = tree.root.findAllByType(TouchableOpacity)[0];

    act(() => {
      trigger.props.onPress();
    });

    expect(findHostByTestId(tree.root, "content-one").length).toBe(0);
    expect(onValueChange).toHaveBeenLastCalledWith("");
  });
});

describe("Accordion - multiple mode", () => {
  beforeEach(() => {
    configureNextSpy.mockClear();
  });

  it("toggles items independently and reports arrays via onValueChange", () => {
    const onValueChange = jest.fn();
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(
        <Accordion type="multiple" defaultValue={["one"]} onValueChange={onValueChange}>
          <AccordionItem value="one">
            <AccordionTrigger>First</AccordionTrigger>
            <AccordionContent>
              <Text testID="content-one">One</Text>
            </AccordionContent>
          </AccordionItem>
          <AccordionItem value="two">
            <AccordionTrigger>Second</AccordionTrigger>
            <AccordionContent>
              <Text testID="content-two">Two</Text>
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      );
    });

    const triggers = tree.root.findAllByType(TouchableOpacity);

    expect(findHostByTestId(tree.root, "content-one").length).toBe(1);
    expect(findHostByTestId(tree.root, "content-two").length).toBe(0);

    act(() => {
      triggers[1].props.onPress();
    });

    expect(findHostByTestId(tree.root, "content-two").length).toBe(1);
    expect(onValueChange).toHaveBeenLastCalledWith(["one", "two"]);

    act(() => {
      triggers[0].props.onPress();
    });

    expect(findHostByTestId(tree.root, "content-one").length).toBe(0);
    expect(findHostByTestId(tree.root, "content-two").length).toBe(1);
    expect(onValueChange).toHaveBeenLastCalledWith(["two"]);
    expect(configureNextSpy).toHaveBeenCalledTimes(2);
  });
});
