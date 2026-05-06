using backend.Dtos.Households;
using backend.Interfaces;
using backend.Options;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Pages;

[AllowAnonymous]
public class InviteModel : PageModel
{
     private readonly InvitationOptions options;
     private readonly IWebHostEnvironment environment;
     private readonly IHouseholdInvitationService invitationService;
     private readonly ILogger<InviteModel> logger;

     public InviteModel(IOptions<InvitationOptions> optionsAccessor,
          IWebHostEnvironment environment,
          IHouseholdInvitationService invitationService,
          ILogger<InviteModel> logger)
     {
          options = optionsAccessor.Value;
          this.environment = environment;
          this.invitationService = invitationService;
          this.logger = logger;
     }

     public Guid Id { get; private set; }
     public string DeepLink { get; private set; } = string.Empty;
     public string AppStoreUrl { get; private set; } = string.Empty;
     public string PlayStoreUrl { get; private set; } = string.Empty;
     public HouseholdInvitationAcceptStatus? AcceptStatus { get; private set; }
     public string? AcceptFailureMessage { get; private set; }
     public string? AcceptedHouseholdName { get; private set; }

     public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
     {
          Id = id;

          var deepLinkBase = environment.IsDevelopment()
               ? options.DevelopmentMobileDeepLinkBaseUrl ?? options.MobileDeepLinkBaseUrl
               : options.MobileDeepLinkBaseUrl;

          if (!string.IsNullOrWhiteSpace(deepLinkBase))
          {
               DeepLink = $"{deepLinkBase.TrimEnd('/')}/{id}";
          }

          AppStoreUrl = string.IsNullOrWhiteSpace(options.AppStoreUrl)
               ? "#"
               : options.AppStoreUrl;
          PlayStoreUrl = string.IsNullOrWhiteSpace(options.PlayStoreUrl)
               ? "#"
               : options.PlayStoreUrl;

          try
          {
               var acceptResult = await invitationService.AcceptInvitationByEmailAsync(id, cancellationToken);
               AcceptStatus = acceptResult.Status;
               AcceptFailureMessage = acceptResult.FailureReason;
               AcceptedHouseholdName = acceptResult.Membership?.HouseholdName;
               logger.LogInformation("Invitation {InvitationId} processed with status {Status}", id, AcceptStatus);
          }
          catch (Exception ex)
          {
               AcceptStatus = null;
               AcceptFailureMessage = "An error occurred while processing your invitation. Please try again later.";
               logger.LogError(ex, "Error processing invitation {InvitationId}", id);
          }
          return Page();
     }
}

