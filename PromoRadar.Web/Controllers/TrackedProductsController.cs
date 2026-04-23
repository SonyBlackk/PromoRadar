using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoRadar.Web.Data;
using PromoRadar.Web.Models;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class TrackedProductsController : Controller
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private const string CreateDraftTempDataKey = "TrackedProducts.CreateDraft";
    private const string PreferencesDraftTempDataKey = "TrackedProducts.PreferencesDraft";
    private const string StoresDraftTempDataKey = "TrackedProducts.StoresDraft";

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private static readonly IReadOnlyList<string> Categories =
    [
        "Placa de vídeo (GPU)",
        "Processador (CPU)",
        "Memória RAM",
        "SSD",
        "Placa-mãe",
        "Fonte",
        "Cooler e refrigeração",
        "Gabinete",
        "Periféricos"
    ];

    private static readonly IReadOnlyList<StoreTemplate> AvailableStores =
    [
        new("amazon-br", "Amazon Brasil", "Marketplace", true, "a", "amazon", "#111827"),
        new("mercado-livre", "Mercado Livre", "Marketplace", true, "ml", "mercado-livre", "#facc15"),
        new("magazine-luiza", "Magazine Luiza", "Varejo", false, "magalu", "magalu", "#2563eb"),
        new("kabum", "KaBuM!", "Especializada", false, "kabum", "kabum", "#0057ff"),
        new("pichau", "Pichau", "Especializada", false, "pichau", "pichau", "#ef233c"),
        new("casas-bahia", "Casas Bahia", "Varejo", false, "casas", "casas-bahia", "#1d4ed8"),
        new("shopee", "Shopee", "Marketplace", false, "s", "shopee", "#ea580c"),
        new("americanas", "Americanas", "Varejo", false, "americanas", "americanas", "#dc2626")
    ];

    private static readonly HashSet<string> DefaultSelectedStores = new(StringComparer.OrdinalIgnoreCase)
    {
        "amazon-br",
        "mercado-livre",
        "magazine-luiza"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public TrackedProductsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["ActiveNav"] = "my-products";

        var draft = GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey);
        var vm = new CreateTrackedProductViewModel
        {
            CategoryOptions = Categories,
            Name = draft?.Name ?? string.Empty,
            Category = draft?.Category ?? string.Empty,
            Brand = draft?.Brand,
            Url = draft?.Url,
            TargetPrice = draft?.TargetPrice
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTrackedProductViewModel model, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "my-products";
        model.CategoryOptions = Categories;

        ValidateUrl(model.Url);
        ValidateImageFile(model.ImageFile);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentDraft = GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey);
        var uploadedImageUrl = await SaveImageAsync(model.ImageFile, cancellationToken);

        SaveDraftToTempData(CreateDraftTempDataKey, new CreateTrackedProductDraft
        {
            Name = model.Name.Trim(),
            Category = model.Category.Trim(),
            Brand = string.IsNullOrWhiteSpace(model.Brand) ? null : model.Brand.Trim(),
            Url = string.IsNullOrWhiteSpace(model.Url) ? null : model.Url.Trim(),
            TargetPrice = model.TargetPrice,
            ImageUrl = uploadedImageUrl ?? currentDraft?.ImageUrl ?? "/images/products/default.svg"
        });

        RemoveDraftFromTempData(PreferencesDraftTempDataKey);
        RemoveDraftFromTempData(StoresDraftTempDataKey);

        return RedirectToAction(nameof(Preferences));
    }

    [HttpGet]
    public IActionResult Preferences()
    {
        ViewData["ActiveNav"] = "my-products";

        var createDraft = GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey);
        if (createDraft is null)
        {
            TempData["FlowWarning"] = "Preencha primeiro as informações do produto.";
            return RedirectToAction(nameof(Create));
        }

        var preferencesDraft = GetDraftFromTempData<TrackedProductPreferencesDraft>(PreferencesDraftTempDataKey);

        return View(new TrackedProductPreferencesViewModel
        {
            TargetPrice = preferencesDraft is not null ? FormatMoney(preferencesDraft.TargetPrice) : createDraft.TargetPrice.HasValue ? FormatMoney(createDraft.TargetPrice.Value) : string.Empty,
            MaximumPrice = preferencesDraft?.MaximumPrice is decimal maximumPrice ? FormatMoney(maximumPrice) : null,
            AlertTrigger = preferencesDraft?.AlertTrigger ?? PriceAlertTrigger.BelowTarget,
            EmailAlerts = preferencesDraft?.EmailAlerts ?? true,
            PushNotifications = preferencesDraft?.PushNotifications ?? true,
            DailySummary = preferencesDraft?.DailySummary ?? false
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Preferences(TrackedProductPreferencesViewModel model)
    {
        ViewData["ActiveNav"] = "my-products";

        if (GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey) is null)
        {
            TempData["FlowWarning"] = "Preencha primeiro as informações do produto.";
            return RedirectToAction(nameof(Create));
        }

        if (!TryParseMoney(model.TargetPrice, out var targetPrice) || targetPrice <= 0)
        {
            ModelState.AddModelError(nameof(TrackedProductPreferencesViewModel.TargetPrice), "Informe um preço alvo válido.");
        }

        decimal? maximumPrice = null;
        if (!string.IsNullOrWhiteSpace(model.MaximumPrice))
        {
            if (!TryParseMoney(model.MaximumPrice, out var parsedMaximum) || parsedMaximum <= 0)
            {
                ModelState.AddModelError(nameof(TrackedProductPreferencesViewModel.MaximumPrice), "Informe um preço máximo válido.");
            }
            else
            {
                maximumPrice = parsedMaximum;
            }
        }

        if (model.AlertTrigger == PriceAlertTrigger.BelowMaximum && maximumPrice is null)
        {
            ModelState.AddModelError(nameof(TrackedProductPreferencesViewModel.MaximumPrice), "Para este tipo de alerta, informe o preço máximo.");
        }

        if (maximumPrice is not null && targetPrice > maximumPrice)
        {
            ModelState.AddModelError(nameof(TrackedProductPreferencesViewModel.MaximumPrice), "O preço máximo deve ser maior ou igual ao preço alvo.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        SaveDraftToTempData(PreferencesDraftTempDataKey, new TrackedProductPreferencesDraft
        {
            TargetPrice = targetPrice,
            MaximumPrice = maximumPrice,
            AlertTrigger = model.AlertTrigger,
            EmailAlerts = model.EmailAlerts,
            PushNotifications = model.PushNotifications,
            DailySummary = model.DailySummary
        });

        return RedirectToAction(nameof(Stores));
    }

    [HttpGet]
    public IActionResult Stores()
    {
        ViewData["ActiveNav"] = "my-products";

        if (GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey) is null)
        {
            TempData["FlowWarning"] = "Preencha primeiro as informações do produto.";
            return RedirectToAction(nameof(Create));
        }

        if (GetDraftFromTempData<TrackedProductPreferencesDraft>(PreferencesDraftTempDataKey) is null)
        {
            return RedirectToAction(nameof(Preferences));
        }

        var storesDraft = GetDraftFromTempData<TrackedProductStoresDraft>(StoresDraftTempDataKey);
        IEnumerable<string> selectedKeys = storesDraft?.SelectedStoreKeys is { Count: > 0 } ? storesDraft.SelectedStoreKeys : DefaultSelectedStores;

        return View(new CreateTrackedProductStep3ViewModel
        {
            Stores = BuildStoreSelectionItems(selectedKeys)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Stores(CreateTrackedProductStep3ViewModel model)
    {
        ViewData["ActiveNav"] = "my-products";

        if (GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey) is null)
        {
            TempData["FlowWarning"] = "Preencha primeiro as informações do produto.";
            return RedirectToAction(nameof(Create));
        }

        if (GetDraftFromTempData<TrackedProductPreferencesDraft>(PreferencesDraftTempDataKey) is null)
        {
            return RedirectToAction(nameof(Preferences));
        }

        var selectedKeys = (model.Stores ?? []).Where(store => store.IsSelected).Select(store => store.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
        model.Stores = BuildStoreSelectionItems(selectedKeys);

        if (!model.Stores.Any(store => store.IsSelected))
        {
            ModelState.AddModelError(string.Empty, "Selecione ao menos uma loja para continuar.");
            return View(model);
        }

        SaveDraftToTempData(StoresDraftTempDataKey, new TrackedProductStoresDraft
        {
            SelectedStoreKeys = model.Stores
                .Where(store => store.IsSelected)
                .Select(store => store.Key)
                .ToList()
        });

        return RedirectToAction(nameof(Review));
    }

    [HttpGet]
    public IActionResult Review()
    {
        ViewData["ActiveNav"] = "my-products";

        if (!TryGetFullDraft(out var createDraft, out var preferencesDraft, out var storesDraft, out var redirectResult))
        {
            return redirectResult!;
        }

        var vm = BuildReviewViewModel(createDraft!, preferencesDraft!, storesDraft!);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "my-products";

        if (!TryGetFullDraft(out var createDraft, out var preferencesDraft, out var storesDraft, out var redirectResult))
        {
            return redirectResult!;
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (storesDraft!.SelectedStoreKeys.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Selecione ao menos uma loja para monitorar.");
            var invalidVm = BuildReviewViewModel(createDraft!, preferencesDraft!, storesDraft!);
            return View(invalidVm);
        }

        var normalizedName = createDraft!.Name.Trim();
        var normalizedCategory = createDraft.Category.Trim();
        var normalizedNameLower = normalizedName.ToLower();
        var normalizedCategoryLower = normalizedCategory.ToLower();
        var productImageUrl = string.IsNullOrWhiteSpace(createDraft.ImageUrl) ? "/images/products/default.svg" : createDraft.ImageUrl;

        var product = await _dbContext.Products.FirstOrDefaultAsync(
            productEntity =>
                productEntity.Name.ToLower() == normalizedNameLower &&
                productEntity.Category.ToLower() == normalizedCategoryLower,
            cancellationToken);

        if (product is null)
        {
            product = new Product
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                Category = normalizedCategory,
                ImageUrl = productImageUrl,
                BaselinePrice = preferencesDraft!.TargetPrice
            };

            _dbContext.Products.Add(product);
        }
        else
        {
            if (string.Equals(product.ImageUrl, "/images/products/default.svg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(productImageUrl, "/images/products/default.svg", StringComparison.OrdinalIgnoreCase))
            {
                product.ImageUrl = productImageUrl;
            }

            if (product.BaselinePrice <= 0)
            {
                product.BaselinePrice = preferencesDraft!.TargetPrice;
            }
        }

        var selectedKeys = storesDraft.SelectedStoreKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var templatesByKey = AvailableStores.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var stores = await _dbContext.Stores
            .Where(store => selectedKeys.Contains(store.Slug))
            .ToDictionaryAsync(store => store.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var key in selectedKeys)
        {
            if (stores.ContainsKey(key))
            {
                continue;
            }

            var template = templatesByKey.TryGetValue(key, out var storeTemplate)
                ? storeTemplate
                : new StoreTemplate(key, key, "Marketplace", false, key[..1], "neutral", "#5b57f3");

            var newStore = new Store
            {
                Id = Guid.NewGuid(),
                Name = template.Name,
                Slug = template.Key,
                AccentColor = template.AccentColor
            };

            _dbContext.Stores.Add(newStore);
            stores[key] = newStore;
        }

        var selectedStoreIds = stores.Values.Select(store => store.Id).ToList();
        var existingStoreIds = await _dbContext.UserTrackedProducts
            .Where(tracked =>
                tracked.ApplicationUserId == userId &&
                tracked.ProductId == product.Id &&
                tracked.IsActive &&
                selectedStoreIds.Contains(tracked.StoreId))
            .Select(tracked => tracked.StoreId)
            .ToHashSetAsync(cancellationToken);

        var createdCount = 0;
        foreach (var store in stores.Values)
        {
            if (existingStoreIds.Contains(store.Id))
            {
                continue;
            }

            var trackedProduct = new UserTrackedProduct
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId,
                ProductId = product.Id,
                StoreId = store.Id,
                TargetPrice = preferencesDraft!.TargetPrice,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.UserTrackedProducts.Add(trackedProduct);
            _dbContext.PriceSnapshots.Add(new PriceSnapshot
            {
                Id = Guid.NewGuid(),
                UserTrackedProductId = trackedProduct.Id,
                Price = preferencesDraft.TargetPrice,
                CapturedAtUtc = DateTime.UtcNow
            });

            createdCount += 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        RemoveDraftFromTempData(CreateDraftTempDataKey);
        RemoveDraftFromTempData(PreferencesDraftTempDataKey);
        RemoveDraftFromTempData(StoresDraftTempDataKey);

        TempData["SuccessMessage"] = createdCount > 0
            ? $"Monitoramento criado com sucesso em {createdCount} loja(s)."
            : "As lojas selecionadas já estavam sendo monitoradas para este produto.";

        return RedirectToAction("Index", "MyProducts");
    }

    private bool TryGetFullDraft(
        out CreateTrackedProductDraft? createDraft,
        out TrackedProductPreferencesDraft? preferencesDraft,
        out TrackedProductStoresDraft? storesDraft,
        out IActionResult? redirectResult)
    {
        createDraft = GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey);
        if (createDraft is null)
        {
            TempData["FlowWarning"] = "Preencha primeiro as informações do produto.";
            redirectResult = RedirectToAction(nameof(Create));
            preferencesDraft = null;
            storesDraft = null;
            return false;
        }

        preferencesDraft = GetDraftFromTempData<TrackedProductPreferencesDraft>(PreferencesDraftTempDataKey);
        if (preferencesDraft is null)
        {
            TempData["FlowWarning"] = "Configure suas preferências antes de revisar.";
            redirectResult = RedirectToAction(nameof(Preferences));
            storesDraft = null;
            return false;
        }

        storesDraft = GetDraftFromTempData<TrackedProductStoresDraft>(StoresDraftTempDataKey);
        if (storesDraft?.SelectedStoreKeys is not { Count: > 0 })
        {
            TempData["FlowWarning"] = "Selecione ao menos uma loja para continuar.";
            redirectResult = RedirectToAction(nameof(Stores));
            return false;
        }

        redirectResult = null;
        return true;
    }

    private CreateTrackedProductReviewViewModel BuildReviewViewModel(
        CreateTrackedProductDraft createDraft,
        TrackedProductPreferencesDraft preferencesDraft,
        TrackedProductStoresDraft storesDraft)
    {
        var selectedStores = BuildStoreSelectionItems(storesDraft.SelectedStoreKeys)
            .Where(store => store.IsSelected)
            .ToList();

        var configuredAlerts = new List<ReviewAlertItemViewModel>
        {
            new() { Label = preferencesDraft.AlertTrigger switch
            {
                PriceAlertTrigger.BelowMaximum => "Abaixo do preço máximo",
                PriceAlertTrigger.AnyReduction => "Qualquer redução",
                _ => "Abaixo do preço alvo"
            } },
            new() { Label = "E-mail", IsEnabled = preferencesDraft.EmailAlerts },
            new() { Label = "Notificações push", IsEnabled = preferencesDraft.PushNotifications },
            new() { Label = "Resumo diário", IsEnabled = preferencesDraft.DailySummary }
        };

        return new CreateTrackedProductReviewViewModel
        {
            ProductName = createDraft.Name,
            Category = createDraft.Category,
            Brand = string.IsNullOrWhiteSpace(createDraft.Brand) ? "NVIDIA" : createDraft.Brand,
            ProductUrl = string.IsNullOrWhiteSpace(createDraft.Url)
                ? "https://www.exemplo.com/rtx-4070-ti-super"
                : createDraft.Url,
            ProductImageUrl = string.IsNullOrWhiteSpace(createDraft.ImageUrl)
                ? "/images/products/default.svg"
                : createDraft.ImageUrl,
            TargetPrice = preferencesDraft.TargetPrice,
            MaximumPrice = preferencesDraft.MaximumPrice,
            ConfiguredAlerts = configuredAlerts,
            SelectedStores = selectedStores
        };
    }

    private List<TrackedStoreSelectionItemViewModel> BuildStoreSelectionItems(IEnumerable<string> selectedStoreKeys)
    {
        var selectedLookup = selectedStoreKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return AvailableStores
            .Select(store => new TrackedStoreSelectionItemViewModel
            {
                Key = store.Key,
                Name = store.Name,
                StoreType = store.StoreType,
                IsRecommended = store.IsRecommended,
                IsSelected = selectedLookup.Contains(store.Key),
                LogoText = store.LogoText,
                LogoVariant = store.LogoVariant
            })
            .ToList();
    }

    private TDraft? GetDraftFromTempData<TDraft>(string key)
    {
        if (TempData.Peek(key) is not string json || string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<TDraft>(json);
        }
        catch
        {
            TempData.Remove(key);
            return default;
        }
    }

    private void SaveDraftToTempData<TDraft>(string key, TDraft draft)
    {
        TempData[key] = JsonSerializer.Serialize(draft);
    }

    private void RemoveDraftFromTempData(string key)
    {
        TempData.Remove(key);
    }

    private void ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            ModelState.AddModelError(nameof(CreateTrackedProductViewModel.Url), "Informe uma URL válida, incluindo http:// ou https://.");
        }
    }

    private void ValidateImageFile(IFormFile? imageFile)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            return;
        }

        if (imageFile.Length > MaxImageSizeBytes)
        {
            ModelState.AddModelError(nameof(CreateTrackedProductViewModel.ImageFile), "A imagem deve ter no máximo 5MB.");
        }

        var extension = Path.GetExtension(imageFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            ModelState.AddModelError(nameof(CreateTrackedProductViewModel.ImageFile), "Formatos permitidos: PNG, JPG, JPEG e WEBP.");
        }
    }

    private async Task<string?> SaveImageAsync(IFormFile? imageFile, CancellationToken cancellationToken)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            return null;
        }

        var uploadsDirectory = Path.Combine(_environment.WebRootPath, "images", "products", "uploads");
        Directory.CreateDirectory(uploadsDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.Combine(uploadsDirectory, fileName);

        await using var stream = new FileStream(destinationPath, FileMode.Create);
        await imageFile.CopyToAsync(stream, cancellationToken);

        return $"/images/products/uploads/{fileName}";
    }

    private static bool TryParseMoney(string? input, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var sanitized = input.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(sanitized, NumberStyles.Number, PtBr, out value);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N2", PtBr);
    }

    private sealed class CreateTrackedProductDraft
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Url { get; set; }
        public decimal? TargetPrice { get; set; }
        public string ImageUrl { get; set; } = "/images/products/default.svg";
    }

    private sealed class TrackedProductPreferencesDraft
    {
        public decimal TargetPrice { get; set; }
        public decimal? MaximumPrice { get; set; }
        public PriceAlertTrigger AlertTrigger { get; set; } = PriceAlertTrigger.BelowTarget;
        public bool EmailAlerts { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool DailySummary { get; set; }
    }

    private sealed class TrackedProductStoresDraft
    {
        public List<string> SelectedStoreKeys { get; set; } = [];
    }

    private sealed class StoreTemplate(
        string key,
        string name,
        string storeType,
        bool isRecommended,
        string logoText,
        string logoVariant,
        string accentColor)
    {
        public string Key { get; } = key;
        public string Name { get; } = name;
        public string StoreType { get; } = storeType;
        public bool IsRecommended { get; } = isRecommended;
        public string LogoText { get; } = logoText;
        public string LogoVariant { get; } = logoVariant;
        public string AccentColor { get; } = accentColor;
    }
}
