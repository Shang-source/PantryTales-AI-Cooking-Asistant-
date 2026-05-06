import { RefObject } from "react";
import { View } from "react-native";
import { captureRef } from "react-native-view-shot";
import * as MediaLibrary from "expo-media-library";
import { UnavailabilityError } from "expo-modules-core";

/**
 * Request permission to access the media library.
 * @returns true if permission is granted, false otherwise.
 */
export async function requestMediaLibraryPermission(): Promise<boolean> {
  const { status } = await MediaLibrary.requestPermissionsAsync();
  return status === "granted";
}

/**
 * Capture a view as a high-resolution image.
 * @param viewRef - Reference to the View component to capture.
 * @returns The local URI of the captured image.
 */
export async function captureViewAsImage(
  viewRef: RefObject<View | null>,
): Promise<string> {
  if (!viewRef.current) {
    throw new Error("View reference is not available");
  }

  const uri = await captureRef(viewRef, {
    format: "png",
    quality: 1,
    result: "tmpfile",
  });

  return uri;
}

/**
 * Save an image to the device's photo album.
 * @param imageUri - The local URI of the image to save.
 * @returns The asset object from the media library.
 */
export async function saveImageToAlbum(
  imageUri: string,
): Promise<MediaLibrary.Asset> {
  const asset = await MediaLibrary.createAssetAsync(imageUri);
  return asset;
}

export interface SaveRecipePosterResult {
  success: boolean;
  error?: string;
  asset?: MediaLibrary.Asset;
}

/**
 * Complete flow to save a recipe poster to the photo album.
 * Handles permission request, image capture, and saving.
 *
 * @param viewRef - Reference to the poster View component.
 * @returns Result object with success status and optional error/asset.
 */
export async function saveRecipePosterToAlbum(
  viewRef: RefObject<View | null>,
): Promise<SaveRecipePosterResult> {
  try {
    // 1. Request permission
    const hasPermission = await requestMediaLibraryPermission();
    if (!hasPermission) {
      return {
        success: false,
        error:
          "Permission to access photo library was denied. Please enable it in your device settings.",
      };
    }

    // 2. Capture the view as an image
    const imageUri = await captureViewAsImage(viewRef);

    // 3. Save to photo album
    const asset = await saveImageToAlbum(imageUri);

    return {
      success: true,
      asset,
    };
  } catch (error) {
    // Check if the error is due to missing native module (common in Expo Go)
    if (error instanceof UnavailabilityError) {
      return {
        success: false,
        error:
          "This feature requires a development build. Please rebuild the app to enable saving posters.",
      };
    }

    const errorMessage =
      error instanceof Error ? error.message : "Unknown error occurred";

    return {
      success: false,
      error: errorMessage,
    };
  }
}
