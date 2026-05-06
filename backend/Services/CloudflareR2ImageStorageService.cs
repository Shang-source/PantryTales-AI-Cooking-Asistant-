using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using backend.Interfaces;
using backend.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class CloudflareR2ImageStorageService : IImageStorageService
{
    private readonly IAmazonS3 s3Client;
    private readonly CloudflareR2Options options;
    private readonly ILogger<CloudflareR2ImageStorageService> logger;

    public CloudflareR2ImageStorageService(IAmazonS3 s3Client, IOptions<CloudflareR2Options> optionsAccessor,
        ILogger<CloudflareR2ImageStorageService> logger)
    {
        this.s3Client = s3Client;
        var r2Options = optionsAccessor.Value;
        ValidateOptions(r2Options);
        options = r2Options;
        this.logger = logger;
    }

    public async Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length == 0)
        {
            throw new ArgumentException("File is empty.", nameof(file));
        }

        const long maxFileSize = 10 * 1024 * 1024; // 10 MB
        if (file.Length > maxFileSize)
        {
            throw new ArgumentException($"File size exceeds maximum allowed size of {maxFileSize / 1024 / 1024}MB.",
                nameof(file));
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only image uploads are allowed.");
        }

        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Unsupported image type.")
        };
        var objectKey = $"uploads/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";

        await using var stream = file.OpenReadStream();
        var request = new PutObjectRequest
        {
            BucketName = options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = file.ContentType,
            UseChunkEncoding = false // R2 does not support streaming V4 chunked uploads
        };

        var response = await s3Client.PutObjectAsync(request, cancellationToken);

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Upload to R2 failed with status {StatusCode} for key {Key}", response.HttpStatusCode,
                objectKey);
            throw new InvalidOperationException("Uploading image failed.");
        }

        return $"{options.PublicBaseUrl.TrimEnd('/')}/{objectKey}";
    }

    private static void ValidateOptions(CloudflareR2Options options)
    {
        if (string.IsNullOrWhiteSpace(options.AccountId) ||
            string.IsNullOrWhiteSpace(options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(options.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(options.BucketName) ||
            string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            throw new InvalidOperationException("Cloudflare R2 configuration is missing or incomplete.");
        }
    }
}
