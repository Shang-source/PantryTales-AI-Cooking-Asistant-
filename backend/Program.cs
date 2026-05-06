using Amazon.S3;
using backend.Auth;
using backend.Data;
using backend.Interfaces;
using backend.Middleware;
using backend.Options;
using backend.Repository;
using backend.Services;
using backend.Services.Embedding;
using backend.Services.ImageGeneration;
using backend.Services.Vision;
using Clerk.Net.AspNetCore.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;
using Resend;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

// Add services to the container.

builder.Services.AddRazorPages();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// Job Monitoring
builder.Services.AddSingleton<JobStatusService>();

// Cookie Auth for Admin Panel
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "SmartAuth";
        options.DefaultChallengeScheme = "SmartAuth";
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Admin/Login";
        options.AccessDeniedPath = "/Admin/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddPolicyScheme("SmartAuth", "Bearer or Cookie", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers[Microsoft.Net.Http.Headers.HeaderNames.Authorization];
            if (!string.IsNullOrEmpty(authHeader) && authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                // API Request
                var isDev = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
                var useRealAuth = context.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("UseRealAuth");

                return isDev && !useRealAuth
                    ? DevelopmentAuthenticationDefaults.AuthenticationScheme
                    : ClerkAuthenticationDefaults.AuthenticationScheme;
            }

            // Browser Request
            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
        npgsqlOptions => npgsqlOptions.UseVector());
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IChecklistRepository, ChecklistRepository>();
builder.Services.AddScoped<IChecklistService, ChecklistService>();
builder.Services.AddScoped<IHouseholdMembershipRepository, HouseholdMembershipRepository>();
builder.Services.AddScoped<IHouseholdService, HouseholdService>();
builder.Services.AddScoped<IHouseholdInvitationRepository, HouseholdInvitationRepository>();

// Register email sender: Use Resend in production (when API key is configured), otherwise use logging stub
builder.Services.Configure<InvitationOptions>(builder.Configuration.GetSection("Invitation"));
var resendApiKey = builder.Configuration.GetValue<string>("Invitation:ResendApiKey");
if (!string.IsNullOrEmpty(resendApiKey))
{
    builder.Services.AddOptions<ResendClientOptions>()
        .Configure(o => o.ApiToken = resendApiKey);
    builder.Services.AddHttpClient<ResendClient>();
    builder.Services.AddTransient<IResend>(sp => sp.GetRequiredService<ResendClient>());
    builder.Services.AddScoped<IEmailSender, ResendEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
}

builder.Services.AddScoped<IHouseholdInvitationService, HouseholdInvitationService>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<IRecipeLikeRepository, RecipeLikeRepository>();
builder.Services.AddScoped<IRecipeLikeService, RecipeLikeService>();
builder.Services.AddScoped<IRecipeSaveRepository, RecipeSaveRepository>();
builder.Services.AddScoped<IRecipeSaveService, RecipeSaveService>();
builder.Services.AddScoped<IRecipeCookRepository, RecipeCookRepository>();
builder.Services.AddScoped<IRecipeCookService, RecipeCookService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IRecipeInteractionRepository, RecipeInteractionRepository>();
builder.Services.AddScoped<IRecipeInteractionService, RecipeInteractionService>();
builder.Services.AddScoped<IRecipeCommentRepository, RecipeCommentRepository>();
builder.Services.AddScoped<IRecipeCommentService, RecipeCommentService>();
builder.Services.AddScoped<IKnowledgebaseRepository, KnowledgebaseRepository>();
builder.Services.AddScoped<IKnowledgebaseService, KnowledgebaseService>();
builder.Services.AddScoped<INameNormalizationRepository, NameNormalizationRepository>();
builder.Services.AddScoped<INameNormalizationService, NameNormalizationService>();
builder.Services.Configure<NameNormalizationOptions>(
    builder.Configuration.GetSection(NameNormalizationOptions.SectionName));
builder.Services.AddHostedService<NameNormalizationBackgroundService>();

// Embedding services
builder.Services.Configure<EmbeddingOptions>(
    builder.Configuration.GetSection(EmbeddingOptions.SectionName));
builder.Services.AddHttpClient<IEmbeddingProvider, OpenAIEmbeddingProvider>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddHostedService<EmbeddingBackgroundService>();

// Vision services (AI image recognition)
builder.Services.Configure<VisionOptions>(
    builder.Configuration.GetSection(VisionOptions.SectionName));
builder.Services.AddHttpClient<IVisionProvider, OpenAIVisionProvider>();
builder.Services.AddHttpClient<IVisionService, VisionService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
        // Force HTTP/1.1 to avoid potential HTTP/2 ALPN issues on macOS
        client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
        client.DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Disable connection pooling to force fresh SSL handshakes
        PooledConnectionLifetime = TimeSpan.Zero,
        // Use SocketsHttpHandler for better TLS compatibility
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                                  System.Security.Authentication.SslProtocols.Tls13,
            // Explicitly set ALPN to HTTP/1.1 only
            ApplicationProtocols = new List<System.Net.Security.SslApplicationProtocol>
            {
                System.Net.Security.SslApplicationProtocol.Http11
            },
            // SECURITY: Only bypass certificate validation in development
            // This is needed for downloading images from servers with SSL issues on macOS
            RemoteCertificateValidationCallback = isDevelopment
                ? (sender, cert, chain, errors) =>
                {
                    Console.WriteLine("[WARNING] SSL certificate validation bypassed for VisionService (development mode only)");
                    return true;
                }
                : null
        }
    });

// Image generation services (AI recipe cover images - Gemini)
builder.Services.Configure<ImageGenerationOptions>(
    builder.Configuration.GetSection(ImageGenerationOptions.SectionName));
builder.Services.Configure<RecipeImageGenerationOptions>(
    builder.Configuration.GetSection(RecipeImageGenerationOptions.SectionName));
builder.Services.AddHttpClient<IImageGenerationProvider, GeminiImageGenerationProvider>();
builder.Services.AddScoped<IRecipeImageService, RecipeImageService>();
builder.Services.AddHostedService<RecipeImageBackgroundService>();

// Smart Recipe services (AI-powered recipe suggestions)
builder.Services.AddHttpClient<ISmartRecipeAIProvider, backend.Services.SmartRecipe.SmartRecipeAIProvider>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<ISmartRecipeService, backend.Services.SmartRecipe.SmartRecipeService>();

// Recommended Recipe services
builder.Services.AddScoped<IRecommendedRecipeService, backend.Services.RecommendedRecipe.RecommendedRecipeService>();

// Nutrition calculation service
builder.Services.AddScoped<INutritionService, NutritionService>();


builder.Services.Configure<CloudflareR2Options>(builder.Configuration.GetSection("CloudflareR2"));
builder.Services.Configure<DevelopmentUserOptions>(builder.Configuration.GetSection("DevelopmentUser"));
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var r2Options = sp.GetRequiredService<IOptions<CloudflareR2Options>>().Value;

    var config = new AmazonS3Config
    {
        ServiceURL = $"https://{r2Options.AccountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true,
        AuthenticationRegion = "auto"
    };

    return new AmazonS3Client(r2Options.AccessKeyId, r2Options.SecretAccessKey, config);
});
builder.Services.AddScoped<IImageStorageService, CloudflareR2ImageStorageService>();

var clerkAuthority = builder.Configuration["Clerk:Authority"]
                     ?? throw new InvalidOperationException("Clerk Authority configuration is missing.");
var clerkAuthorizedParty = builder.Configuration["Clerk:AuthorizedParty"]
                           ?? throw new InvalidOperationException("Clerk AuthorizedParty configuration is missing.");

// Allow using real Clerk auth in development by setting UseRealAuth to true
var useRealAuth = builder.Configuration.GetValue<bool>("UseRealAuth");

// Authentication is configured earlier with the "SmartAuth" policy scheme
var authenticationBuilder = builder.Services.AddAuthentication();

authenticationBuilder.AddClerkAuthentication(options =>
{
    options.Authority = clerkAuthority;
    options.AuthorizedParty = clerkAuthorizedParty;
});

if (isDevelopment)
{
    authenticationBuilder.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
        DevelopmentAuthenticationDefaults.AuthenticationScheme,
        _ => { });
}

// Add claims transformation to inject user roles from database
builder.Services.AddScoped<IClaimsTransformation, RoleClaimsTransformation>();

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!isDevelopment)
{
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseStaticFiles();

app.UseAuthentication();

app.UseMiddleware<LazyUserSyncMiddleware>();

app.UseAuthorization();

if (isDevelopment)
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapRazorPages();

// Health check endpoint for container orchestration (App Runner, ECS, etc.)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .AllowAnonymous();

app.Run();
