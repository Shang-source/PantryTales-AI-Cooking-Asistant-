using System.ComponentModel.DataAnnotations;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Pages.Admin.Tags;

public class IndexModel(ITagRepository repository) : AdminPageModel
{
    public List<Tag> Tags { get; set; } = [];
    public List<TagType> TagTypes { get; set; } = [];

    [BindProperty]
    public NewTagModel NewTag { get; set; } = new();

    [BindProperty]
    public int EditId { get; set; }

    [BindProperty]
    public EditTagModel EditTag { get; set; } = new();

    public async Task OnGetAsync()
    {
        Tags = await repository.GetAllAsync();
        TagTypes = await repository.GetTagTypesAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var tag = await repository.GetByIdAsync(id);
        if (tag != null)
        {
            await repository.DeleteAsync(tag);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            Tags = await repository.GetAllAsync();
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        var tag = new Tag
        {
            Name = NewTag.Name,
            DisplayName = NewTag.DisplayName,
            Type = NewTag.Type,
            Color = NewTag.Color,
            Icon = NewTag.Icon,
            IsActive = true
        };

        if (await repository.ExistsAsync(tag.Name, tag.Type))
        {
            ModelState.AddModelError("NewTag.Name", "Tag with this Name and Type already exists.");
            Tags = await repository.GetAllAsync();
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        await repository.AddAsync(tag);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (!ModelState.IsValid)
        {
            Tags = await repository.GetAllAsync();
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        var tag = await repository.GetByIdAsync(EditId);
        if (tag == null)
        {
            TempData["Error"] = "Tag not found.";
            return RedirectToPage();
        }

        // Check if name conflicts with another tag
        if (await repository.ExistsAsyncExcludingIdAsync(EditTag.Name, EditTag.Type, EditId))
        {
            ModelState.AddModelError("EditTag.Name", "Tag with this Name and Type already exists.");
            Tags = await repository.GetAllAsync();
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        tag.Name = EditTag.Name;
        tag.DisplayName = EditTag.DisplayName;
        tag.Type = EditTag.Type;
        tag.Color = EditTag.Color;
        tag.Icon = EditTag.Icon;

        await repository.UpdateAsync(tag);
        return RedirectToPage();
    }

    public class NewTagModel
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Type { get; set; } = "General";

        [MaxLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color (e.g., #FF0000)")]
        public string? Color { get; set; }

        [MaxLength(64)]
        public string? Icon { get; set; }
    }

    public class EditTagModel
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex color (e.g., #FF0000)")]
        public string? Color { get; set; }

        [MaxLength(64)]
        public string? Icon { get; set; }
    }
}
