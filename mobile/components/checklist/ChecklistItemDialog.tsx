import { useState, useEffect } from "react";
import { View, Text, TextInput, TouchableOpacity } from "react-native";
import {
  useAddChecklistItem,
  useUpdateChecklistItem,
  ChecklistItemDto,
} from "@/hooks/useChecklist";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogClose,
} from "@/components/dialog";
import { UNITS, CATEGORIES } from "@/constants/dropdownValue";
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
} from "@/components/dropdown-menu";
import { cn } from "@/utils/cn";
import { useTheme } from "@/contexts/ThemeContext";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  item?: ChecklistItemDto | null;
}

export function ChecklistItemDialog({ open, onOpenChange, item }: Props) {
  const isEditMode = !!item;
  const { colors } = useTheme();

  const [name, setName] = useState("");
  const [quantity, setQuantity] = useState("");
  const [unit, setUnit] = useState("g");
  const [category, setCategory] = useState("Vegetables");

  const addItem = useAddChecklistItem();
  const updateItem = useUpdateChecklistItem();

  useEffect(() => {
    if (item) {
      setName(item.name);
      setQuantity(item.amount.toString());
      setUnit(item.unit);
      setCategory(item.category || "Other");
    }
  }, [item]);

  const resetForm = () => {
    setName("");
    setQuantity("");
    setUnit("g");
    setCategory("Vegetables");
  };

  const quantityValue = parseFloat(quantity);
  const isQuantityValid =
    /^\d+(\.\d{1,2})?$/.test(quantity.trim()) &&
    Number.isFinite(quantityValue) &&
    quantityValue > 0;
  const nameError = name.trim() === "" ? "Name is required" : "";
  const quantityError =
    quantity.trim() === ""
      ? "Quantity is required"
      : isQuantityValid
        ? ""
        : "Quantity is invalid";

  const handleSubmit = () => {
    if (!name.trim() || !isQuantityValid) return;

    const data = {
      name: name.trim(),
      amount: quantityValue,
      unit: unit,
      category: category,
    };

    if (isEditMode && item) {
      updateItem.mutate(
        { id: item.id, data },
        {
          onSuccess: () => {
            onOpenChange(false);
          },
        }
      );
    } else {
      addItem.mutate(data, {
        onSuccess: () => {
          resetForm();
          onOpenChange(false);
        },
      });
    }
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) {
      if (!isEditMode) {
        resetForm();
      }
    }
    onOpenChange(newOpen);
  };

  const selectUnit = (u: string) => {
    setUnit(u);
  };

  const selectCategory = (c: string) => {
    setCategory(c);
  };

  const isPending = isEditMode ? updateItem.isPending : addItem.isPending;
  const buttonText = isEditMode
    ? isPending
      ? "Updating..."
      : "Update"
    : isPending
      ? "Adding..."
      : "Add";

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent style={{ backgroundColor: colors.bg, borderWidth: 0 }}>
        <DialogHeader className="items-start">
          <DialogTitle style={{ color: colors.textPrimary }}>
            {isEditMode ? "Edit item" : "Add item"}
          </DialogTitle>
        </DialogHeader>

        <View className="flex-row gap-3 mb-3">
          <View className="flex-1">
            <TextInput
              value={name}
              onChangeText={setName}
              placeholder="Name"
              placeholderTextColor={colors.textMuted}
              className="rounded-xl px-4 py-3.5"
              style={{
                backgroundColor: colors.card,
                color: colors.textPrimary,
                borderWidth: 1,
                borderColor: nameError ? colors.error : colors.border,
              }}
            />
            {nameError ? (
              <Text className="text-xs mt-1" style={{ color: colors.error }}>{nameError}</Text>
            ) : null}
          </View>
          <View className="flex-1">
            <TextInput
              value={quantity}
              onChangeText={setQuantity}
              placeholder="Quantity"
              placeholderTextColor={colors.textMuted}
              keyboardType="decimal-pad"
              className="rounded-xl px-4 py-3.5"
              style={{
                backgroundColor: colors.card,
                color: colors.textPrimary,
                borderWidth: 1,
                borderColor: quantityError ? colors.error : colors.border,
              }}
            />
            {quantityError ? (
              <Text className="text-xs mt-1" style={{ color: colors.error }}>{quantityError}</Text>
            ) : null}
          </View>
        </View>

        <View className="flex-row gap-3 mb-2">
          {/* Unit Dropdown */}
          <View className="flex-1">
            <DropdownMenu>
              <DropdownMenuTrigger>
                <View
                  className="flex-row items-center justify-between rounded-xl px-4 py-3.5"
                  style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
                >
                  <Text style={{ color: colors.textPrimary }}>{unit}</Text>
                  <Text style={{ color: colors.textSecondary }}>{"\u25BE"}</Text>
                </View>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                className="rounded-xl max-h-40 shadow-lg"
                style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}
              >
                {UNITS.map((u) => (
                  <DropdownMenuItem
                    key={u}
                    onPress={() => selectUnit(u)}
                    className="px-3 py-2 rounded-sm"
                    style={{ backgroundColor: unit === u ? colors.card : "transparent" }}
                  >
                    <Text style={{ color: colors.textPrimary }}>{u}</Text>
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </View>

          {/* Category Dropdown */}
          <View className="flex-1">
            <DropdownMenu>
              <DropdownMenuTrigger>
                <View
                  className="flex-row items-center justify-between rounded-xl px-4 py-3.5"
                  style={{ backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }}
                >
                  <Text style={{ color: colors.textPrimary }}>{category}</Text>
                  <Text style={{ color: colors.textSecondary }}>{"\u25BE"}</Text>
                </View>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                className="rounded-xl max-h-40 shadow-lg"
                style={{ backgroundColor: colors.bg, borderWidth: 1, borderColor: colors.border }}
              >
                {CATEGORIES.map((c) => (
                  <DropdownMenuItem
                    key={c}
                    onPress={() => selectCategory(c)}
                    className="px-3 py-2 rounded-sm"
                    style={{ backgroundColor: category === c ? colors.card : "transparent" }}
                  >
                    <Text style={{ color: colors.textPrimary }}>{c}</Text>
                  </DropdownMenuItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          </View>
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
            disabled={!name.trim() || !isQuantityValid || isPending}
            className="flex-1 py-3 rounded-xl items-center"
            style={{
              backgroundColor: colors.accent,
              opacity: !name.trim() || !isQuantityValid || isPending ? 0.5 : 1,
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
