import { createContext, useContext, useState, ReactNode, useMemo } from "react";
import {
  CameraScanner,
  RecognizedIngredient,
} from "@/components/CameraScanner";

interface ScannerContextType {
  openScanner: () => void;
}

const ScannerContext = createContext<ScannerContextType | undefined>(undefined);

export const useCameraScanner = () => {
  const context = useContext(ScannerContext);
  if (!context) {
    throw new Error(
      "useCameraScanner must be used within a CameraScannerProvider"
    );
  }
  return context;
};

export function CameraScannerProvider({ children }: { children: ReactNode }) {
  const [isScannerOpen, setIsScannerOpen] = useState(false);

  const handleConfirm = (ingredients: RecognizedIngredient[]) => {
    setIsScannerOpen(false);
  };

  const contextValue = useMemo(
    () => ({
      openScanner: () => setIsScannerOpen(true),
    }),
    []
  );

  return (
    <ScannerContext.Provider value={contextValue}>
      {children}
      {isScannerOpen && (
        <CameraScanner
          onClose={() => setIsScannerOpen(false)}
          onConfirm={handleConfirm}
        />
      )}
    </ScannerContext.Provider>
  );
}

export default CameraScannerProvider;
