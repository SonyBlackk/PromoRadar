using Microsoft.AspNetCore.Http;

namespace PromoRadar.Web.Services;

public class ProductImageService : IProductImageService
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<ImageFileType, string[]> AllowedContentTypes = new()
    {
        [ImageFileType.Png] = ["image/png"],
        [ImageFileType.Jpeg] = ["image/jpeg", "image/pjpeg"],
        [ImageFileType.Webp] = ["image/webp"]
    };

    private readonly IWebHostEnvironment _environment;

    public ProductImageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<ProductImageSaveResult> SaveAsync(IFormFile? imageFile, CancellationToken cancellationToken = default)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            return new ProductImageSaveResult { IsValid = true };
        }

        if (imageFile.Length > MaxImageSizeBytes)
        {
            return Invalid("A imagem deve ter no máximo 5MB.");
        }

        var fileExtension = Path.GetExtension(imageFile.FileName);
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            return Invalid("Formato de arquivo inválido.");
        }

        var detectedType = await DetectFileTypeAsync(imageFile, cancellationToken);
        if (detectedType is null)
        {
            return Invalid("Não foi possível validar o conteúdo da imagem enviada.");
        }

        if (!IsExtensionCompatible(fileExtension, detectedType.Value))
        {
            return Invalid("A extensão do arquivo não corresponde ao conteúdo da imagem.");
        }

        if (!string.IsNullOrWhiteSpace(imageFile.ContentType))
        {
            var normalizedContentType = imageFile.ContentType.Trim().ToLowerInvariant();
            if (!AllowedContentTypes[detectedType.Value].Contains(normalizedContentType))
            {
                return Invalid("Tipo MIME da imagem não permitido.");
            }
        }

        var uploadsRoot = ResolveUploadsDirectory();
        Directory.CreateDirectory(uploadsRoot);

        var safeExtension = GetCanonicalExtension(detectedType.Value);
        var fileName = $"{Guid.NewGuid():N}{safeExtension}";
        var destinationPath = Path.GetFullPath(Path.Combine(uploadsRoot, fileName));
        var uploadsRootFullPath = Path.GetFullPath(uploadsRoot);

        if (!destinationPath.StartsWith(uploadsRootFullPath, StringComparison.Ordinal))
        {
            return Invalid("Não foi possível salvar a imagem.");
        }

        await using var sourceStream = imageFile.OpenReadStream();
        await using var destinationStream = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        return new ProductImageSaveResult
        {
            IsValid = true,
            ImageUrl = $"/images/products/uploads/{fileName}"
        };
    }

    private string ResolveUploadsDirectory()
    {
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        return Path.Combine(webRoot, "images", "products", "uploads");
    }

    private static ProductImageSaveResult Invalid(string errorMessage)
    {
        return new ProductImageSaveResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }

    private static string GetCanonicalExtension(ImageFileType fileType)
    {
        return fileType switch
        {
            ImageFileType.Png => ".png",
            ImageFileType.Jpeg => ".jpg",
            ImageFileType.Webp => ".webp",
            _ => string.Empty
        };
    }

    private static bool IsExtensionCompatible(string extension, ImageFileType detectedType)
    {
        var normalized = extension.Trim().ToLowerInvariant();
        return detectedType switch
        {
            ImageFileType.Png => normalized is ".png",
            ImageFileType.Jpeg => normalized is ".jpg" or ".jpeg",
            ImageFileType.Webp => normalized is ".webp",
            _ => false
        };
    }

    private static async Task<ImageFileType?> DetectFileTypeAsync(IFormFile imageFile, CancellationToken cancellationToken)
    {
        await using var stream = imageFile.OpenReadStream();
        var header = new byte[16];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (read < 12)
        {
            return null;
        }

        if (header[0] == 0x89 &&
            header[1] == 0x50 &&
            header[2] == 0x4E &&
            header[3] == 0x47 &&
            header[4] == 0x0D &&
            header[5] == 0x0A &&
            header[6] == 0x1A &&
            header[7] == 0x0A)
        {
            return ImageFileType.Png;
        }

        if (header[0] == 0xFF &&
            header[1] == 0xD8 &&
            header[2] == 0xFF)
        {
            return ImageFileType.Jpeg;
        }

        if (header[0] == 0x52 &&
            header[1] == 0x49 &&
            header[2] == 0x46 &&
            header[3] == 0x46 &&
            header[8] == 0x57 &&
            header[9] == 0x45 &&
            header[10] == 0x42 &&
            header[11] == 0x50)
        {
            return ImageFileType.Webp;
        }

        return null;
    }

    private enum ImageFileType
    {
        Png = 1,
        Jpeg = 2,
        Webp = 3
    }
}
