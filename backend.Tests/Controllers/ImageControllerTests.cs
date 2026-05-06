using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos.Images;
using backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class ImageControllerTests
{
    [Fact]
    public async Task UploadAsync_ReturnsOk_WhenUploadSucceeds()
    {
        const string expectedUrl = "https://example.com/uploads/test.jpg";
        var fakeService = new FakeImageStorageService { UrlToReturn = expectedUrl };
        var controller = CreateController(fakeService);
        using var file = CreateFormFile();

        var result = await controller.UploadAsync(file.File, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ImageUploadResponseDto>(ok.Value);
        Assert.Equal(expectedUrl, payload.Url);
        Assert.Same(file.File, fakeService.LastFile);
    }

    [Fact]
    public async Task UploadAsync_ReturnsBadRequest_WhenFileIsNull()
    {
        var controller = CreateController(new FakeImageStorageService());

        var result = await controller.UploadAsync(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("No file provided.", badRequest.Value);
    }

    [Fact]
    public async Task UploadAsync_ReturnsBadRequest_OnArgumentException()
    {
        var fakeService = new FakeImageStorageService
        {
            ExceptionToThrow = new ArgumentException("File too large.")
        };
        var controller = CreateController(fakeService);
        using var file = CreateFormFile();

        var result = await controller.UploadAsync(file.File, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("File too large.", badRequest.Value);
    }

    [Fact]
    public async Task UploadAsync_ReturnsBadRequest_OnInvalidOperationException()
    {
        var fakeService = new FakeImageStorageService
        {
            ExceptionToThrow = new InvalidOperationException("Unsupported image type.")
        };
        var controller = CreateController(fakeService);
        using var file = CreateFormFile();

        var result = await controller.UploadAsync(file.File, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Unsupported image type.", badRequest.Value);
    }

    [Fact]
    public async Task UploadAsync_ReturnsInternalServerError_OnUnexpectedException()
    {
        var fakeService = new FakeImageStorageService
        {
            ExceptionToThrow = new Exception("Unexpected failure")
        };
        var controller = CreateController(fakeService);
        using var file = CreateFormFile();

        var result = await controller.UploadAsync(file.File, CancellationToken.None);

        var errorResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, errorResult.StatusCode);
        Assert.Equal("Failed to upload image.", errorResult.Value);
    }

    private static ImageController CreateController(IImageStorageService storageService) =>
        new(storageService, NullLogger<ImageController>.Instance);

    private static DisposableFormFile CreateFormFile(string fileName = "photo.jpg", string contentType = "image/jpeg")
    {
        var bytes = Encoding.UTF8.GetBytes("test");
        var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return new DisposableFormFile(formFile, stream);
    }

    private sealed class DisposableFormFile : IDisposable
    {
        public DisposableFormFile(IFormFile file, Stream stream)
        {
            File = file;
            this.stream = stream;
        }

        public IFormFile File { get; }
        private readonly Stream stream;

        public void Dispose() => stream.Dispose();
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        public string? UrlToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public IFormFile? LastFile { get; private set; }

        public Task<string> UploadAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            LastFile = file;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(UrlToReturn ?? string.Empty);
        }
    }
}
