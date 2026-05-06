using System.ComponentModel.DataAnnotations;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Pages.Admin.TagTypes;

public class IndexModel(ITagRepository repository) : AdminPageModel
{
    public List<TagType> TagTypes { get; set; } = [];

    [BindProperty]
    public NewTagTypeModel NewTagType { get; set; } = new();

    [BindProperty]
    public int EditId { get; set; }

    [BindProperty]
    public EditTagTypeModel EditTagType { get; set; } = new();

    public async Task OnGetAsync()
    {
        TagTypes = await repository.GetTagTypesAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var tagType = await repository.GetTagTypeByIdAsync(id);
        if (tagType != null)
        {
            // Check if any tags use this type
            if (await repository.HasTagsOfTypeAsync(tagType.Name))
            {
                TempData["Error"] = $"Cannot delete tag type '{tagType.DisplayName}' because it has existing tags. Delete or reassign those tags first.";
                return RedirectToPage();
            }

            await repository.DeleteTagTypeAsync(tagType);
            TempData["Message"] = "Tag type deleted successfully.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        if (await repository.TagTypeExistsAsync(NewTagType.Name))
        {
            ModelState.AddModelError("NewTagType.Name", "Tag type with this name already exists.");
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        var tagType = new TagType
        {
            Name = NewTagType.Name,
            DisplayName = NewTagType.DisplayName,
            Description = NewTagType.Description
        };

        await repository.AddTagTypeAsync(tagType);
        TempData["Message"] = "Tag type created successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (!ModelState.IsValid)
        {
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        var tagType = await repository.GetTagTypeByIdAsync(EditId);
        if (tagType == null)
        {
            TempData["Error"] = "Tag type not found.";
            return RedirectToPage();
        }

        // Check if name conflicts with another tag type
        var existing = await repository.GetTagTypesAsync();
        var nameConflict = existing.FirstOrDefault(tt => tt.Name == EditTagType.Name && tt.Id != EditId);
        if (nameConflict != null)
        {
            ModelState.AddModelError("EditTagType.Name", "Tag type with this name already exists.");
            TagTypes = await repository.GetTagTypesAsync();
            return Page();
        }

        tagType.Name = EditTagType.Name;
        tagType.DisplayName = EditTagType.DisplayName;
        tagType.Description = EditTagType.Description;

        await repository.UpdateTagTypeAsync(tagType);
        TempData["Message"] = "Tag type updated successfully.";
        return RedirectToPage();
    }

    public class NewTagTypeModel
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? Description { get; set; }
    }

    public class EditTagTypeModel
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? Description { get; set; }
    }
}
