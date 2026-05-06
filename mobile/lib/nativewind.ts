import { LinearGradient } from "expo-linear-gradient";
import { Image as ExpoImage } from "expo-image";
import { cssInterop } from "nativewind";

cssInterop(LinearGradient, {
  className: "style",
});

cssInterop(ExpoImage, {
  className: "style",
});

export { LinearGradient, ExpoImage as Image };
