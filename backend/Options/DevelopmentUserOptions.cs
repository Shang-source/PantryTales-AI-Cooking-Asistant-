namespace backend.Options;

public class DevelopmentUserOptions
{
    public string ClerkUserId { get; set; } = "dev_user";
    public string Email { get; set; } = "dev@example.com";
    public string Nickname { get; set; } = "Dev User";
    public string BearerToken { get; set; } = "dev-token";
    public string Role { get; set; } = "Admin";
}
