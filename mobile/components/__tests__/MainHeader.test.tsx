import { type ComponentProps } from "react";
import renderer, { act } from "react-test-renderer";
import { Text, TouchableOpacity, Modal} from "react-native";
import {
  jest,
  describe,
  it,
  expect,
  beforeEach,
  afterEach,
} from "@jest/globals";

// Use fake timers to avoid async warnings
jest.useFakeTimers();

// Mock react-native-safe-area-context
jest.mock("react-native-safe-area-context", () => ({
  useSafeAreaInsets: jest.fn(() => ({ top: 0, bottom: 0, left: 0, right: 0 })),
}));

// Mock NativeAnimatedHelper
jest.mock("react-native/Libraries/Animated/NativeAnimatedHelper", () => ({}), {
  virtual: true,
});

// Mock @expo/vector-icons
jest.mock("@expo/vector-icons", () => ({
  Feather: "FeatherIcon",
}));

// Mock lucide-react-native
jest.mock("lucide-react-native", () => ({
  X: "XIcon",
  Package: "PackageIcon",
  Sparkles: "SparklesIcon",
}));

// Mock expo-router
const mockPush = jest.fn();
jest.mock("expo-router", () => {
  const { useEffect } = require("react");
  return {
    router: {
      push: (route: string) => mockPush(route),
    },
    useFocusEffect: (effect: () => void | (() => void)) => {
      useEffect(() => effect?.(), [effect]);
    },
  };
});

// Mock vision recognition hooks
const mockIngredientMutate = jest.fn();
const mockRecipeMutate = jest.fn();
jest.mock("@/hooks/useVisionRecognition", () => ({
  useIngredientRecognition: (options?: any) => ({
    mutate: mockIngredientMutate.mockImplementation((uri: unknown) => {
      // Simulate success callback after mutation
      if (options?.onSuccess) {
        setTimeout(() => {
          options.onSuccess({
            data: {
              success: true,
              ingredients: [{ name: "tomato", quantity: 2, unit: "pcs" }],
            },
          });
        }, 0);
      }
    }),
    isLoading: false,
  }),
  useRecipeRecognition: (options?: any) => ({
    mutate: mockRecipeMutate.mockImplementation((uri: unknown) => {
      if (options?.onSuccess) {
        setTimeout(() => {
          options.onSuccess({
            data: {
              success: true,
              recipe: { name: "Test Recipe" },
            },
          });
        }, 0);
      }
    }),
    isLoading: false,
  }),
}));

// Mock Sheet components
jest.mock("../sheet", () => {
  const { Text, TouchableOpacity } = require("react-native");
  return {
    Sheet: ({ children }: any) => children,
    SheetTrigger: ({ children }: any) => children,
    SheetContent: ({ children }: any) => children,
    SheetHeader: ({ children }: any) => children,
    SheetTitle: ({ children }: any) => <Text>{children}</Text>,
    SheetClose: ({ children, onPress }: any) => (
      <TouchableOpacity onPress={onPress}>{children}</TouchableOpacity>
    ),
  };
});

// Mock RecognitionModeSheet
jest.mock("../RecognitionModeSheet", () => {
  const { Modal, TouchableOpacity } = require("react-native");
  return {
    RecognitionModeSheet: ({
      visible,
      onClose,
      onIngredientScan,
      onRecipeScan,
    }: any) =>
      visible ? (
        <Modal visible={visible} testID="recognition-mode-sheet">
          <TouchableOpacity testID="close-recognition" onPress={onClose} />
          <TouchableOpacity
            testID="ingredient-scan-btn"
            onPress={onIngredientScan}
          />
          <TouchableOpacity testID="recipe-scan-btn" onPress={onRecipeScan} />
        </Modal>
      ) : null,
  };
});

// Mock IngredientScannerSheet
jest.mock("../IngredientScannerSheet", () => {
  const { Modal, TouchableOpacity } = require("react-native");
  return {
    IngredientScannerSheet: ({
      visible,
      onClose,
      onTakePhoto,
      onSelectFromGallery,
    }: any) =>
      visible ? (
        <Modal visible={visible} testID="ingredient-scanner-sheet">
          <TouchableOpacity
            testID="close-ingredient-scanner"
            onPress={onClose}
          />
          <TouchableOpacity
            testID="ingredient-take-photo"
            onPress={() => onTakePhoto("file://test-image.jpg")}
          />
          <TouchableOpacity
            testID="ingredient-select-gallery"
            onPress={() => onSelectFromGallery("file://gallery-image.jpg")}
          />
        </Modal>
      ) : null,
  };
});

// Mock RecipeScannerSheet
jest.mock("../RecipeScannerSheet", () => {
  const { Modal, TouchableOpacity } = require("react-native");
  return {
    RecipeScannerSheet: ({
      visible,
      onClose,
      onTakePhoto,
      onSelectFromGallery,
    }: any) =>
      visible ? (
        <Modal visible={visible} testID="recipe-scanner-sheet">
          <TouchableOpacity testID="close-recipe-scanner" onPress={onClose} />
          <TouchableOpacity
            testID="recipe-take-photo"
            onPress={() => onTakePhoto("file://recipe-image.jpg")}
          />
          <TouchableOpacity
            testID="recipe-select-gallery"
            onPress={() => onSelectFromGallery("file://recipe-gallery.jpg")}
          />
        </Modal>
      ) : null,
  };
});

// Mock TurboModuleRegistry
jest.mock("react-native/Libraries/TurboModule/TurboModuleRegistry", () => {
  const { jest: jestGlobals } = require("@jest/globals");
  const actual = jestGlobals.requireActual(
    "react-native/Libraries/TurboModule/TurboModuleRegistry"
  ) as any;
  return {
    ...actual,
    getEnforcing: (name: string) => {
      if (name === "DevMenu" || name === "SettingsManager") {
        return {
          getConstants: () => ({ settings: {} }),
          addListener: jestGlobals.fn(),
          removeListener: jestGlobals.fn(),
          removeListeners: jestGlobals.fn(),
        };
      }
      try {
        return actual.getEnforcing(name);
      } catch {
        return {};
      }
    },
  };
});

// Import component after mocks
import MainHeader from "../MainHeader";

describe("MainHeader", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterEach(() => {
    // Flush timers inside act() to avoid React "not wrapped in act(...)" warnings.
    act(() => {
      jest.runOnlyPendingTimers();
    });
  });

  it("renders the header with PantryTales title", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    const textElements = tree.root.findAllByType(Text);
    const pantryText = textElements.find((t) => t.props.children === "Pantry");
    const talesText = textElements.find((t) => t.props.children === "Tales");

    expect(pantryText).toBeDefined();
    expect(talesText).toBeDefined();
  });

  it("renders menu icon and camera icon", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    const featherIcons = tree.root.findAll(
      (node) => (node.type as any) === "FeatherIcon"
    );

    // Should have menu icon, camera icon, and nav item icons
    expect(featherIcons.length).toBeGreaterThanOrEqual(2);

    const menuIcon = featherIcons.find((icon) => icon.props.name === "menu");
    const cameraIcon = featherIcons.find(
      (icon) => icon.props.name === "camera"
    );

    expect(menuIcon).toBeDefined();
    expect(cameraIcon).toBeDefined();
  });

  it("renders navigation items in the side menu", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    const textElements = tree.root.findAllByType(Text);
    const labels = textElements.map((t) => t.props.children);

    expect(labels).toContain("Home");
    expect(labels).toContain("My Inventory");
    expect(labels).toContain("Checklist");
    expect(labels).toContain("Post a Recipe");
    expect(labels).toContain("Community");
    expect(labels).toContain("Cooking Tips");
    expect(labels).toContain("Cooking History");
    expect(labels).toContain("Settings");
  });

  it("opens recognition mode sheet when camera button is pressed", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Find camera button (TouchableOpacity with camera icon as child)
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    expect(cameraButton).toBeDefined();

    act(() => {
      cameraButton?.props.onPress?.();
    });

    // Check if RecognitionModeSheet is now visible
    const recognitionSheet = tree.root.findAllByProps({
      testID: "recognition-mode-sheet",
    });
    expect(recognitionSheet.length).toBeGreaterThanOrEqual(1);
  });

  it("opens ingredient scanner when ingredient scan is selected", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open recognition sheet first
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    // Click ingredient scan button
    const ingredientScanBtn = tree.root.findByProps({
      testID: "ingredient-scan-btn",
    });
    act(() => {
      ingredientScanBtn.props.onPress?.();
    });

    // Check if IngredientScannerSheet is now visible
    const ingredientSheet = tree.root.findAllByProps({
      testID: "ingredient-scanner-sheet",
    });
    expect(ingredientSheet.length).toBeGreaterThanOrEqual(1);
  });

  it("opens recipe scanner when recipe scan is selected", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open recognition sheet first
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    // Click recipe scan button
    const recipeScanBtn = tree.root.findByProps({
      testID: "recipe-scan-btn",
    });
    act(() => {
      recipeScanBtn.props.onPress?.();
    });

    // Check if RecipeScannerSheet is now visible
    const recipeSheet = tree.root.findAllByProps({
      testID: "recipe-scanner-sheet",
    });
    expect(recipeSheet.length).toBeGreaterThanOrEqual(1);
  });

  it("calls ingredient recognition when photo is taken", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open recognition sheet, then ingredient scanner
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    const ingredientScanBtn = tree.root.findByProps({
      testID: "ingredient-scan-btn",
    });
    act(() => {
      ingredientScanBtn.props.onPress?.();
    });

    // Take photo
    const takePhotoBtn = tree.root.findByProps({
      testID: "ingredient-take-photo",
    });
    act(() => {
      takePhotoBtn.props.onPress?.();
    });

    expect(mockIngredientMutate).toHaveBeenCalledWith("file://test-image.jpg");
  });

  it("calls recipe recognition when photo is taken", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open recognition sheet, then recipe scanner
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    const recipeScanBtn = tree.root.findByProps({
      testID: "recipe-scan-btn",
    });
    act(() => {
      recipeScanBtn.props.onPress?.();
    });

    // Take photo
    const takePhotoBtn = tree.root.findByProps({
      testID: "recipe-take-photo",
    });
    act(() => {
      takePhotoBtn.props.onPress?.();
    });

    expect(mockRecipeMutate).toHaveBeenCalledWith("file://recipe-image.jpg");
  });

  it("shows processing modal when recognition is in progress", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open ingredient scanner and take photo
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    const ingredientScanBtn = tree.root.findByProps({
      testID: "ingredient-scan-btn",
    });
    act(() => {
      ingredientScanBtn.props.onPress?.();
    });

    const takePhotoBtn = tree.root.findByProps({
      testID: "ingredient-take-photo",
    });
    act(() => {
      takePhotoBtn.props.onPress?.();
    });

    // Check processing modal is visible
    const modals = tree.root.findAllByType(Modal);
    const processingModal = modals.find(
      (m) => m.props.visible && m.props.transparent
    );
    expect(processingModal).toBeDefined();

    // Check processing message
    const textElements = tree.root.findAllByType(Text);
    const processingText = textElements.find(
      (t) => t.props.children === "Analyzing ingredients..."
    );
    expect(processingText).toBeDefined();
  });

  it("navigates to correct routes when nav items are pressed", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Find parent TouchableOpacity/SheetClose for each nav item
    const touchables = tree.root.findAllByType(TouchableOpacity);

    // Find Home nav item
    const homeNavItem = touchables.find((t) => {
      try {
        const text = t.findByType(Text);
        return text.props.children === "Home";
      } catch {
        return false;
      }
    });

    // Click Home and verify navigation
    act(() => {
      homeNavItem?.props.onPress?.();
    });
    expect(mockPush).toHaveBeenCalledWith("/");

    // Reset mock
    mockPush.mockClear();

    // Find My Inventory nav item
    const inventoryNavItem = touchables.find((t) => {
      try {
        const text = t.findByType(Text);
        return text.props.children === "My Inventory";
      } catch {
        return false;
      }
    });

    act(() => {
      inventoryNavItem?.props.onPress?.();
    });
    expect(mockPush).toHaveBeenCalledWith("/MyInventory");
  });

  it("closes recognition sheet when close button is pressed", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open recognition sheet
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    // Verify sheet is visible
    let recognitionSheets = tree.root.findAllByProps({
      testID: "recognition-mode-sheet",
    });
    expect(recognitionSheets.length).toBeGreaterThanOrEqual(1);

    // Close the sheet
    const closeBtn = tree.root.findByProps({
      testID: "close-recognition",
    });
    act(() => {
      closeBtn.props.onPress?.();
    });

    // Verify sheet is closed
    recognitionSheets = tree.root.findAllByProps({
      testID: "recognition-mode-sheet",
    });
    expect(recognitionSheets.length).toBe(0);
  });

  it("handles gallery selection for ingredients", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open ingredient scanner
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    const ingredientScanBtn = tree.root.findByProps({
      testID: "ingredient-scan-btn",
    });
    act(() => {
      ingredientScanBtn.props.onPress?.();
    });

    // Select from gallery
    const galleryBtn = tree.root.findByProps({
      testID: "ingredient-select-gallery",
    });
    act(() => {
      galleryBtn.props.onPress?.();
    });

    expect(mockIngredientMutate).toHaveBeenCalledWith(
      "file://gallery-image.jpg"
    );
  });

  it("handles gallery selection for recipes", () => {
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Open recipe scanner
    const touchables = tree.root.findAllByType(TouchableOpacity);
    const cameraButton = touchables.find((t) => {
      try {
        const featherChild = t.findAll(
          (node) =>
            (node.type as any) === "FeatherIcon" && node.props.name === "camera"
        );
        return featherChild.length > 0;
      } catch {
        return false;
      }
    });

    act(() => {
      cameraButton?.props.onPress?.();
    });

    const recipeScanBtn = tree.root.findByProps({
      testID: "recipe-scan-btn",
    });
    act(() => {
      recipeScanBtn.props.onPress?.();
    });

    // Select from gallery
    const galleryBtn = tree.root.findByProps({
      testID: "recipe-select-gallery",
    });
    act(() => {
      galleryBtn.props.onPress?.();
    });

    expect(mockRecipeMutate).toHaveBeenCalledWith("file://recipe-gallery.jpg");
  });
});

describe("NavItem", () => {
  it("renders with icon and label", () => {
    // NavItem is an internal component, tested indirectly through MainHeader
    let tree!: renderer.ReactTestRenderer;
    act(() => {
      tree = renderer.create(<MainHeader />);
    });

    // Check that nav items have both icon and label
    const featherIcons = tree.root.findAll(
      (node) => (node.type as any) === "FeatherIcon"
    );
    const homeIcon = featherIcons.find((icon) => icon.props.name === "home");
    const packageIcon = featherIcons.find(
      (icon) => icon.props.name === "package"
    );
    const settingsIcon = featherIcons.find(
      (icon) => icon.props.name === "settings"
    );

    expect(homeIcon).toBeDefined();
    expect(packageIcon).toBeDefined();
    expect(settingsIcon).toBeDefined();
  });
});
