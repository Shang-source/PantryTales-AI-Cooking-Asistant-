namespace backend.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken = default);
}
