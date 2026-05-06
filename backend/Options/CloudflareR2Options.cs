namespace backend.Options;

public class CloudflareR2Options
{
    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    /// <summary>
    /// Publicly accessible base URL for the bucket (e.g. https://cdn.example.com).
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
