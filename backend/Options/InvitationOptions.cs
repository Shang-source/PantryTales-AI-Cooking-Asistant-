namespace backend.Options;

public class InvitationOptions
{
    /// <summary>
    /// Base URL used to construct invitation acceptance links (e.g. https://app.pantrytales.com)
    /// </summary>
    public string AcceptBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Email address to send invitations from (e.g. noreply@pantrytales.com)
    /// </summary>
    public string FromEmail { get; set; } = "onboarding@resend.dev";

    /// <summary>
    /// Display name for the sender (e.g. PantryTales)
    /// </summary>
    public string FromName { get; set; } = "PantryTales";

    /// <summary>
    /// Base deep link used to open the mobile application in production (default pantrytales://invitations).
    /// </summary>
    public string MobileDeepLinkBaseUrl { get; set; } = "pantrytales://invitations";

    /// <summary>
    /// Optional deep link base to use while running locally with Expo/Metro in development (e.g. exp://127.0.0.1:8081/--/invitations).
    /// </summary>
    public string? DevelopmentMobileDeepLinkBaseUrl { get; set; } = "exp://127.0.0.1:8081/--/invitations";

    /// <summary>
    /// App Store URL for iOS app download
    /// </summary>
    public string AppStoreUrl { get; set; } = "https://apps.apple.com/app/pantrytales";

    /// <summary>
    /// Play Store URL for Android app download
    /// </summary>
    public string PlayStoreUrl { get; set; } = "https://play.google.com/store/apps/details?id=com.pantrytales";
}
