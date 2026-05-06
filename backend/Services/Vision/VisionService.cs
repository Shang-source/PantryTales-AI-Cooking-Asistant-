using backend.Interfaces;

namespace backend.Services.Vision;

/// <summary>
/// Service for orchestrating AI vision recognition tasks.
/// </summary>
public class VisionService : IVisionService
{
    private readonly IVisionProvider _visionProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<VisionService> _logger;

    private static readonly string[] SupportedMimeTypes =
    [
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/heic",
        "image/heif"
    ];

    private const int MaxImageSizeBytes = 20 * 1024 * 1024; // 20 MB
    private const int MaxImagesForGeneration = 9;

    public VisionService(
        IVisionProvider visionProvider,
        HttpClient httpClient,
        ILogger<VisionService> logger)
    {
        _visionProvider = visionProvider;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IngredientRecognitionResult> RecognizeIngredientsAsync(
        Stream imageStream,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        var validationError = ValidateImage(imageStream, mimeType);
        if (validationError != null)
        {
            return new IngredientRecognitionResult(false, "unknown", [], ErrorMessage: validationError);
        }

        // Read image data
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        var imageData = memoryStream.ToArray();

        _logger.LogInformation(
            "Starting ingredient recognition. File: {FileName}, Size: {Size} bytes, Provider: {Provider}",
            fileName, imageData.Length, _visionProvider.ProviderName);

        // Call provider
        var result = await _visionProvider.RecognizeIngredientsAsync(
            imageData, mimeType, cancellationToken);

        _logger.LogInformation(
            "Ingredient recognition completed. Success: {Success}, Ingredients: {Count}",
            result.Success, result.Ingredients.Count);

        return result;
    }

    public async Task<RecipeRecognitionResult> RecognizeRecipeAsync(
        Stream imageStream,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        var validationError = ValidateImage(imageStream, mimeType);
        if (validationError != null)
        {
            return new RecipeRecognitionResult(false, null, validationError);
        }

        // Read image data
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        var imageData = memoryStream.ToArray();

        _logger.LogInformation(
            "Starting recipe recognition. File: {FileName}, Size: {Size} bytes, Provider: {Provider}",
            fileName, imageData.Length, _visionProvider.ProviderName);

        // Call provider
        var result = await _visionProvider.RecognizeRecipeAsync(
            imageData, mimeType, cancellationToken);

        _logger.LogInformation(
            "Recipe recognition completed. Success: {Success}, Recipe: {Title}",
            result.Success, result.Recipe?.Title ?? "None");

        return result;
    }

    public async Task<GenerateRecipeContentResult> GenerateRecipeContentAsync(
        List<string> imageUrls,
        string title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (imageUrls == null || imageUrls.Count == 0)
        {
            return new GenerateRecipeContentResult(false, null, null, null, null, null, "At least one image URL is required.");
        }

        if (imageUrls.Count > MaxImagesForGeneration)
        {
            return new GenerateRecipeContentResult(false, null, null, null, null, null,
                $"Maximum {MaxImagesForGeneration} images allowed.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return new GenerateRecipeContentResult(false, null, null, null, null, null, "Recipe title is required.");
        }

        _logger.LogInformation(
            "Starting recipe content generation. Title: {Title}, Images: {Count}, Provider: {Provider}",
            title, imageUrls.Count, _visionProvider.ProviderName);

        // Download all images
        var imagesData = new List<byte[]>();
        var mimeTypes = new List<string>();

        foreach (var url in imageUrls)
        {
            try
            {
                var (imageData, mimeType) = await DownloadImageAsync(url, cancellationToken);

                if (imageData.Length > MaxImageSizeBytes)
                {
                    _logger.LogWarning("Image from {Url} exceeds size limit, skipping.", url);
                    continue;
                }

                if (!SupportedMimeTypes.Contains(mimeType.ToLowerInvariant()))
                {
                    _logger.LogWarning("Image from {Url} has unsupported mime type {MimeType}, skipping.", url, mimeType);
                    continue;
                }

                imagesData.Add(imageData);
                mimeTypes.Add(mimeType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image from {Url}, skipping.", url);
            }
        }

        if (imagesData.Count == 0)
        {
            return new GenerateRecipeContentResult(false, null, null, null, null, null, "Could not download any valid images.");
        }

        // Call provider
        var result = await _visionProvider.GenerateRecipeContentAsync(
            imagesData, mimeTypes, title, description, cancellationToken);

        _logger.LogInformation(
            "Recipe content generation completed. Success: {Success}, Steps: {StepCount}, Tags: {TagCount}",
            result.Success, result.Steps?.Count ?? 0, result.Tags?.Count ?? 0);

        return result;
    }

    private async Task<(byte[] Data, string MimeType)> DownloadImageAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var mimeType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            return (data, mimeType);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            // Fallback to curl for SSL issues on macOS
            _logger.LogWarning("HttpClient SSL failed, falling back to curl for {Url}", url);
            return await DownloadImageWithCurlAsync(url, cancellationToken);
        }
    }

    private async Task<(byte[] Data, string MimeType)> DownloadImageWithCurlAsync(string url, CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "curl",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Use ArgumentList to avoid command injection vulnerabilities
            startInfo.ArgumentList.Add("-sS");
            startInfo.ArgumentList.Add("-L");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(tempFile);
            startInfo.ArgumentList.Add("-w");
            startInfo.ArgumentList.Add("%{content_type}");
            startInfo.ArgumentList.Add(url);

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start curl process.");
            }

            var contentType = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new HttpRequestException($"curl failed with exit code {process.ExitCode}: {errorOutput}");
            }

            var data = await File.ReadAllBytesAsync(tempFile, cancellationToken);

            // Parse mime type from content type header (e.g., "image/jpeg; charset=utf-8")
            var mimeType = contentType.Split(';')[0].Trim();
            if (string.IsNullOrEmpty(mimeType) || !mimeType.StartsWith("image/"))
            {
                mimeType = "image/jpeg";
            }

            _logger.LogInformation("Successfully downloaded image via curl: {Size} bytes, {MimeType}", data.Length, mimeType);

            return (data, mimeType);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static string? ValidateImage(Stream imageStream, string mimeType)
    {
        if (imageStream == null || imageStream.Length == 0)
        {
            return "No image provided.";
        }

        if (imageStream.Length > MaxImageSizeBytes)
        {
            return $"Image size exceeds maximum allowed size of {MaxImageSizeBytes / 1024 / 1024} MB.";
        }

        if (!SupportedMimeTypes.Contains(mimeType.ToLowerInvariant()))
        {
            return $"Unsupported image type. Supported types: {string.Join(", ", SupportedMimeTypes)}";
        }

        return null;
    }
}
