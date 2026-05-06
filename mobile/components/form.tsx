import React from "react";
import { View, Text, type ViewProps, type TextProps } from "react-native";
import {
  Controller,
  FormProvider,
  useFormContext,
  useFormState,
  type ControllerProps,
  type FieldPath,
  type FieldValues,
} from "react-hook-form";

import { cn } from "@/utils/cn";
import { Label } from "./label";

// ----------------- Form root -----------------

const Form = FormProvider;

// ----------------- Context -----------------

type FormFieldContextValue<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>,
> = {
  name: TName;
};

const FormFieldContext = React.createContext<FormFieldContextValue>(
  {} as FormFieldContextValue,
);

const FormField = <
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>,
>(
  props: ControllerProps<TFieldValues, TName>,
) => {
  return (
    <FormFieldContext.Provider value={{ name: props.name }}>
      <Controller {...props} />
    </FormFieldContext.Provider>
  );
};

const useFormField = () => {
  const fieldContext = React.useContext(FormFieldContext);
  const itemContext = React.useContext(FormItemContext);
  const { getFieldState } = useFormContext();
  const formState = useFormState({ name: fieldContext.name });
  const fieldState = getFieldState(fieldContext.name, formState);

  if (!fieldContext) {
    throw new Error("useFormField should be used within <FormField>");
  }

  const { id } = itemContext;

  return {
    id,
    name: fieldContext.name,
    formItemId: `${id}-form-item`,
    formDescriptionId: `${id}-form-item-description`,
    formMessageId: `${id}-form-item-message`,
    ...fieldState,
  };
};

// ----------------- FormItem -----------------

type FormItemContextValue = {
  id: string;
};

const FormItemContext = React.createContext<FormItemContextValue>(
  {} as FormItemContextValue,
);

type FormItemProps = ViewProps & {
  className?: string;
};

function FormItem({ className, children, ...props }: FormItemProps) {
  const id = React.useId();

  return (
    <FormItemContext.Provider value={{ id }}>
      <View
        // @ts-ignore
        data-slot="form-item"
        className={cn("grid gap-2", className)}
        {...props}
      >
        {children}
      </View>
    </FormItemContext.Provider>
  );
}

// ----------------- FormLabel -----------------

type FormLabelProps = React.ComponentProps<typeof Label>;

function FormLabel({ className, ...props }: FormLabelProps) {
  const { error } = useFormField();

  return (
    <Label
      // @ts-ignore
      data-slot="form-label"
      data-error={!!error}
      className={cn("data-[error=true]:text-destructive", className)}
      {...props}
    />
  );
}

// ----------------- FormControl -----------------

type FormControlProps = ViewProps & {
  className?: string;
  children: React.ReactNode;
};

function FormControl({ className, children, ...props }: FormControlProps) {
  // error just for style, you can add accessibility to mark
  const { /* error, */ formItemId } = useFormField();

  return (
    <View
      // @ts-ignore
      data-slot="form-control"
      // @ts-ignore RN use nativeID, just for align web structure
      id={formItemId}
      className={className}
      {...props}
    >
      {children}
    </View>
  );
}

// ----------------- FormDescription -----------------

type FormDescriptionProps = TextProps & {
  className?: string;
};

function FormDescription({
  className,
  children,
  ...props
}: FormDescriptionProps) {
  const { formDescriptionId } = useFormField();

  return (
    <Text
      // @ts-ignore
      data-slot="form-description"
      nativeID={formDescriptionId}
      className={cn("text-muted-foreground text-sm", className)}
      {...props}
    >
      {children}
    </Text>
  );
}

// ----------------- FormMessage -----------------

type FormMessageProps = TextProps & {
  className?: string;
};

function FormMessage({ className, children, ...props }: FormMessageProps) {
  const { error, formMessageId } = useFormField();
  const body = error ? String(error?.message ?? "") : children;

  if (!body) return null;

  return (
    <Text
      // @ts-ignore
      data-slot="form-message"
      nativeID={formMessageId}
      className={cn("text-destructive text-sm", className)}
      {...props}
    >
      {body}
    </Text>
  );
}

// ----------------- exports -----------------

export {
  useFormField,
  Form,
  FormItem,
  FormLabel,
  FormControl,
  FormDescription,
  FormMessage,
  FormField,
};
