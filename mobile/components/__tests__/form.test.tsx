import React from "react";
import renderer, { act } from "react-test-renderer";
import { TextInput } from "react-native";
import { useForm } from "react-hook-form";
import { jest, describe, it, expect } from "@jest/globals";

import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormDescription,
  FormMessage,
} from "../form";

jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

type TestValues = { name: string };

const FormHarness = ({
  showError = false,
  messageChildren,
}: {
  showError?: boolean;
  messageChildren?: React.ReactNode;
}) => {
  const form = useForm<TestValues>({ defaultValues: { name: "" } });

  React.useEffect(() => {
    if (showError) {
      form.setError("name", {
        type: "manual",
        message: "Name is required",
      });
    } else {
      form.clearErrors("name");
    }
  }, [showError, form]);

  return (
    <Form {...form}>
      <FormField
        control={form.control}
        name="name"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Name</FormLabel>
            <FormDescription>Helper description</FormDescription>
            <FormControl>
              <TextInput
                value={field.value}
                onBlur={field.onBlur}
                onChangeText={field.onChange}
              />
            </FormControl>
            <FormMessage>{messageChildren}</FormMessage>
          </FormItem>
        )}
      />
    </Form>
  );
};

describe("Form composite components", () => {
  it("wires generated ids between control/description/message", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<FormHarness messageChildren="Static helper" />);
    });

    const control = tree.root.findByProps({ "data-slot": "form-control" });
    expect(control.props.id).toEqual(expect.stringContaining("form-item"));

    const description = tree.root.findByProps({
      "data-slot": "form-description",
    });
    expect(description.props.nativeID).toEqual(
      expect.stringContaining("form-item-description")
    );

    const message = tree.root.findByProps({ "data-slot": "form-message" });
    expect(message.props.nativeID).toEqual(
      expect.stringContaining("form-item-message")
    );
    expect(message.props.children).toBe("Static helper");
  });

  it("renders nothing for FormMessage when no errors or children", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<FormHarness />);
    });

    expect(() =>
      tree.root.findByProps({ "data-slot": "form-message" })
    ).toThrow();
  });

  it("displays validation errors and flags the label", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<FormHarness showError />);
    });

    const label = tree.root.findByProps({ "data-slot": "form-label" });
    expect(label.props["data-error"]).toBe(true);

    const message = tree.root.findByProps({ "data-slot": "form-message" });
    expect(message.props.children).toBe("Name is required");
  });
});
