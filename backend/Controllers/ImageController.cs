using backend.Dtos.Images;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/images")]
[Authorize]
public class ImageController(IImageStorageService imageStorageService, ILogger<ImageController> logger) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImageUploadResponseDto>> UploadAsync([FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest("No file provided.");
        }

        try
        {
            var url = await imageStorageService.UploadAsync(file, cancellationToken);
            return Ok(new ImageUploadResponseDto { Url = url });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Rejected image upload for {FileName}", file.FileName);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Rejected image upload for {FileName}", file.FileName);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload image {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to upload image.");
        }
    }
}
