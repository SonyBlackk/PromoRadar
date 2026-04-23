using Microsoft.AspNetCore.Http;

namespace PromoRadar.Web.Services;

public interface IProductImageService
{
    Task<ProductImageSaveResult> SaveAsync(IFormFile? imageFile, CancellationToken cancellationToken = default);
}

public sealed class ProductImageSaveResult
{
    public bool IsValid { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ImageUrl { get; init; }
}
