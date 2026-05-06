import React, { useEffect, useMemo, useState } from "react";
import { View, Text, TextInput, TouchableOpacity } from "react-native";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogClose,
} from "@/components/dialog";
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
} from "@/components/dropdown-menu";
import { cn } from "@/utils/cn";
import { InventoryItemForm, STORAGE_FILTERS } from "@/types/Inventory";
import { UNITS } from "@/constants/dropdownValue";
import { useTheme } from "@/contexts/ThemeContext";

const DEFAULT_FORM: InventoryItemForm = {
  id: "",
  name: "",
  unit: "g",
  amount: "",
  storage: "RoomTemp",
  addedDate: "",
  expiryDays: "",
};

interface InventoryItemDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  item?: InventoryItemForm | null;
  onAdd: (item: InventoryItemForm) => void;
  onUpdate: (item: InventoryItemForm) => void;
  isPending?: boolean;
  closeOnSubmit?: boolean;
}

export function InventoryItemDialog({
  open,
  onOpenChange,
  item,
  onAdd,
  onUpdate,
  isPending = false,
  closeOnSubmit = true,
}: InventoryItemDialogProps) {
  const isEditMode = !!item;
  const { colors } = useTheme();
  const [form, setForm] = useState<InventoryItemForm>({ ...DEFAULT_FORM });

  const normalizedItem = useMemo(() => {
    if (!item) return null;
    return item;
  }, [item]);

  useEffect(() => {
    if (open) {
      setForm(normalizedItem ?? { ...DEFAULT_FORM });
    }
  }, [open, normalizedItem]);

  const quantityValue = parseFloat(form.amount);
  const isQuantityValid =
    /^\d+(\.\d{1,2})?$/.test(form.amount.trim()) &&
    Number.isFinite(quantityValue) &&
    quantityValue > 0;
  const expiryValue = Number.parseInt(form.expiryDays, 10);
  const isExpiryProvided = form.expiryDays.trim() !== "";
  const isExpiryValid = isExpiryProvided
    ? /^-?\d+$/.test(form.expiryDays.trim()) && Number.isFinite(expiryValue)
    : true;
  const errors = {
    name: form.name.trim() === "" ? "Name is required" : "",
    quantity:
      form.amount.trim() === ""
        ? "Quantity is required"
        : isQuantityValid
          ? ""
          : "Quantity is invalid",
    expiry: isExpiryProvided && !isExpiryValid ? "Expiry days is invalid" : "",
  };

  const canSubmit =
    form.name.trim() !== "" && isQuantityValid && isExpiryValid && !isPending;

  const buttonText = isEditMode
    ? isPending
      ? "Updating..."
      : "Update"
    : isPending
      ? "Adding..."
      : "Add";

  const handleSubmit = () => {
    if (!canSubmit) return;
    if (isEditMode) {
      onUpdate({ ...form, name: form.name.trim() });
    } else {
      onAdd({ ...form, name: form.name.trim() });
    }
    if (closeOnSubmit) {
      onOpenChange(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent style={{ backgroundColor: colors.bg, borderWidth: 0 }}>
        <DialogHeader className="items-start">
          <DialogTitle style={{ color: colors.textPrimary }}>
            {isEditMode ? "Edit item" : "Add item"}
          </DialogTitle>
        </DialogHeader>

        <View>
          <View className="flex-row gap-3 mb-3">
            <View className="flex-1">
              {!isEditMode ? (
                <View>
                  <TextInput
                    className="rounded-xl px-4 py-3.5"
                    style={{
                      backgroundColor: colors.card,
                      color: colors.textPrimary,
                      borderWidth: 1,
                      borderColor: errors.name ? colors.error : colors.border,
                    }}
                    value={form.name}
                    onChangeText={(t) =>
                      setForm((prev) => ({ ...prev, name: t }))
                    }
                    placeholder="Name"
                    placeholderTextColor={colors.textMuted}
                  />
                  {errors.name ? (
                    <Text className="text-xs mt-1" style={{ color: colors.error }}>
                      {errors.name}
                    </Text>
                  ) : null}
                </View>
              ) : (
                <View
                  className="rounded-xl px-4 py-3.5 opacity-50"
                  style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
                >
                  <Text style={{ color: colors.textPrimary }}>{form.name}</Text>
                </View>
              )}
            </View>

            <View className="flex-1">
              <TextInput
                className="rounded-xl px-4 py-3.5"
                style={{
                  backgroundColor: colors.card,
                  color: colors.textPrimary,
                  borderWidth: 1,
                  borderColor: errors.quantity ? colors.error : colors.border,
                }}
                keyboardType="decimal-pad"
                value={form.amount}
                onChangeText={(text) => {
                  setForm((prev) => ({
                    ...prev,
                    amount: text,
                  }));
                }}
                placeholder="Quantity"
                placeholderTextColor={colors.textMuted}
              />

              {errors.quantity ? (
                <Text className="text-xs mt-1" style={{ color: colors.error }}>
                  {errors.quantity}
                </Text>
              ) : null}
            </View>
          </View>

          {/* Unit + Storage */}
          <View className="flex-row gap-3 mb-2">
            <View className="flex-1">
              <View className="relative">
                <DropdownMenu>
                  <DropdownMenuTrigger>
                    <View
                      className="flex-row items-center justify-between rounded-xl px-4 py-3.5"
                      style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
                    >
                      <Text style={{ color: colors.textPrimary }}>
                        {form.unit || "Select unit"}
                      </Text>
                      <Text style={{ color: colors.textSecondary }}>{"\u25BE"}</Text>
                    </View>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent
                    className="rounded-xl max-h-40 shadow-lg"
                    style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}
                  >
                    {UNITS.map((unit) => {
                      const isSelected = unit === form.unit;

                      return (
                        <DropdownMenuItem
                          key={unit}
                          onPress={() =>
                            setForm((prev) => ({
                              ...prev,
                              unit,
                            }))
                          }
                          className="px-3 py-2 rounded-sm"
                          style={{ backgroundColor: isSelected ? colors.card : "transparent" }}
                        >
                          <Text style={{ color: colors.textPrimary }}>{unit}</Text>
                        </DropdownMenuItem>
                      );
                    })}
                  </DropdownMenuContent>
                </DropdownMenu>
              </View>
            </View>

            <View className="flex-1">
              <DropdownMenu>
                <DropdownMenuTrigger>
                  <View
                    className="flex-row items-center justify-between rounded-xl px-4 py-3.5"
                    style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
                  >
                    <Text style={{ color: colors.textPrimary }}>
                      {form.storage
                        ? STORAGE_FILTERS.find(
                            (filter) => filter.value === form.storage,
                          )?.label
                        : "Select storage"}
                    </Text>
                    <Text style={{ color: colors.textSecondary }}>{"\u25BE"}</Text>
                  </View>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                  className="rounded-xl max-h-40 shadow-lg"
                  style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}
                >
                  {STORAGE_FILTERS.map((opt) => {
                    const isSelected = form.storage === opt.value;

                    return (
                      <DropdownMenuItem
                        key={opt.value}
                        onPress={() =>
                          setForm((prev) => ({
                            ...prev,
                            storage: opt.value,
                          }))
                        }
                        className="px-3 py-2 rounded-sm"
                        style={{ backgroundColor: isSelected ? colors.card : "transparent" }}
                      >
                        <Text style={{ color: colors.textPrimary }}>{opt.label}</Text>
                      </DropdownMenuItem>
                    );
                  })}
                </DropdownMenuContent>
              </DropdownMenu>
            </View>
          </View>

          {/* Expiry */}
          <TextInput
            className="rounded-xl px-4 py-3.5"
            style={{
              backgroundColor: colors.card,
              color: colors.textPrimary,
              borderWidth: 1,
              borderColor: errors.expiry ? colors.error : colors.border,
            }}
            keyboardType="numeric"
            value={form.expiryDays}
            onChangeText={(text) => {
              setForm((prev) => ({
                ...prev,
                expiryDays: text,
              }));
            }}
            placeholder="Expiry (days)"
            placeholderTextColor={colors.textMuted}
          />
          {errors.expiry ? (
            <Text className="text-xs mt-1" style={{ color: colors.error }}>{errors.expiry}</Text>
          ) : null}
        </View>

        <DialogFooter className="mt-4">
          <DialogClose
            className="flex-1 rounded-xl py-3 items-center"
            style={{ backgroundColor: colors.card }}
          >
            <Text style={{ color: colors.textPrimary }}>Cancel</Text>
          </DialogClose>
          <TouchableOpacity
            onPress={handleSubmit}
            disabled={!canSubmit}
            className="flex-1 py-3 rounded-xl items-center"
            style={{
              backgroundColor: colors.accent,
              opacity: canSubmit ? 1 : 0.5,
            }}
          >
            <Text className="font-semibold" style={{ color: colors.bg }}>
              {buttonText}
            </Text>
          </TouchableOpacity>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
