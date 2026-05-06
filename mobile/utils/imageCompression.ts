import * as FileSystem from "expo-file-system";
import * as ImageManipulator from "expo-image-manipulator";

const MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB

export type CompressionResult = {
  uri: string;
  wasCompressed: boolean;
};

/**
 * Compresses an image if it exceeds the maximum file size (5 MB).
 * Uses progressive quality reduction and resizing to achieve the target size.
 */
export async function compressImageIfNeeded(
  uri: string,
  fileSizeBytes?: number
): Promise<CompressionResult> {
  try {
    const knownSize = typeof fileSizeBytes === "number" && fileSizeBytes > 0 ? fileSizeBytes : null;
    const fileInfo = knownSize ? null : await FileSystem.getInfoAsync(uri);

    const size = knownSize ?? (fileInfo && "size" in fileInfo ? fileInfo.size : null);

    if (!size || size <= 0) {
      return { uri, wasCompressed: false };
    }

    if (size <= MAX_FILE_SIZE_BYTES) {
      return { uri, wasCompressed: false };
    }

    // Start with moderate compression
    let quality = 0.7;
    let resizeRatio = 1.0;
    let compressedUri = uri;
    let currentSize = size;

    // Progressive compression: try quality reduction first, then resize
    while (currentSize > MAX_FILE_SIZE_BYTES && quality >= 0.3) {
      const result = await ImageManipulator.manipulateAsync(
        uri,
        resizeRatio < 1.0 ? [{ resize: { width: resizeRatio * 2000 } }] : [],
        { compress: quality, format: ImageManipulator.SaveFormat.JPEG }
      );

      compressedUri = result.uri;
      const compressedInfo = await FileSystem.getInfoAsync(compressedUri);

      if (compressedInfo.exists && "size" in compressedInfo && compressedInfo.size) {
        currentSize = compressedInfo.size;
      }

      // If still too large, reduce quality or resize
      if (currentSize > MAX_FILE_SIZE_BYTES) {
        if (quality > 0.3) {
          quality -= 0.1;
        } else if (resizeRatio > 0.5) {
          resizeRatio -= 0.1;
          quality = 0.7; // Reset quality for new size
        } else {
          break; // Give up if we can't compress enough
        }
      }
    }

    return { uri: compressedUri, wasCompressed: true };
  } catch {
    // If compression fails, return original
    return { uri, wasCompressed: false };
  }
}

/**
 * Compresses multiple images and returns count of compressed images.
 */
export async function compressImagesIfNeeded(
  uris: string[]
): Promise<{ results: CompressionResult[]; compressedCount: number }> {
  const results: CompressionResult[] = [];
  let compressedCount = 0;

  for (const uri of uris) {
    const result = await compressImageIfNeeded(uri);
    results.push(result);
    if (result.wasCompressed) {
      compressedCount++;
    }
  }

  return { results, compressedCount };
}
