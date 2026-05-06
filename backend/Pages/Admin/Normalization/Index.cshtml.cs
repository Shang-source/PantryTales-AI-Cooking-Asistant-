using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Pages.Admin.Normalization;

public class IndexModel(INameNormalizationRepository repository, ILogger<IndexModel> logger) : AdminPageModel
{
    // Logger kept for potential future use
    private readonly ILogger<IndexModel> _ = logger;

    public List<NameNormalizationToken> Tokens { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public NameNormalizationTokenCategory? CategoryFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? ActiveFilter { get; set; }

    [BindProperty]
    public NewTokenModel NewToken { get; set; } = new();

    public async Task OnGetAsync()
    {
        Tokens = await repository.GetTokensAsync(CategoryFilter, ActiveFilter);
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        var token = await repository.GetTokenByIdAsync(id);
        if (token != null)
        {
            await repository.DeleteTokenAsync(token);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Tokens = await repository.GetTokensAsync(CategoryFilter, ActiveFilter);
            return Page();
        }

        var token = new NameNormalizationToken
        {
            Token = NewToken.Token,
            Category = NewToken.Category,
            IsRegex = NewToken.IsRegex,
            IsActive = true
        };

        if (await repository.TokenExistsAsync(token.Token))
        {
            ModelState.AddModelError("NewToken.Token", "Token already exists.");
            Tokens = await repository.GetTokensAsync(CategoryFilter, ActiveFilter);
            return Page();
        }

        await repository.AddTokenAsync(token);
        return RedirectToPage();
    }

    public class NewTokenModel
    {
        public string Token { get; set; } = string.Empty;
        public NameNormalizationTokenCategory Category { get; set; } = NameNormalizationTokenCategory.Brand;
        public bool IsRegex { get; set; }
    }
}
