using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PromoRadar.Web.Models;
using PromoRadar.Web.Models.Enums;
using PromoRadar.Web.Services;
using PromoRadar.Web.ViewModels;

namespace PromoRadar.Web.Controllers;

[Authorize]
public class TrackedProductsController : Controller
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private const string CreateDraftTempDataKey = "TrackedProducts.CreateDraft";
    private const string PreferencesDraftTempDataKey = "TrackedProducts.PreferencesDraft";
    private const string StoresDraftTempDataKey = "TrackedProducts.StoresDraft";

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

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITrackedProductCreationService _trackedProductCreationService;
    private readonly IProductImageService _productImageService;

    public TrackedProductsController(
        UserManager<ApplicationUser> userManager,
        ITrackedProductCreationService trackedProductCreationService,
        IProductImageService productImageService)
    {
        _userManager = userManager;
        _trackedProductCreationService = trackedProductCreationService;
        _productImageService = productImageService;
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
        var uploadResult = await _productImageService.SaveAsync(model.ImageFile, cancellationToken);
        if (!uploadResult.IsValid)
        {
            ModelState.AddModelError(
                nameof(CreateTrackedProductViewModel.ImageFile),
                uploadResult.ErrorMessage ?? "Não foi possível validar a imagem enviada.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentDraft = GetDraftFromTempData<CreateTrackedProductDraft>(CreateDraftTempDataKey);

        SaveDraftToTempData(CreateDraftTempDataKey, new CreateTrackedProductDraft
        {
            Name = model.Name.Trim(),
            Category = model.Category.Trim(),
            Brand = string.IsNullOrWhiteSpace(model.Brand) ? null : model.Brand.Trim(),
            Url = string.IsNullOrWhiteSpace(model.Url) ? null : model.Url.Trim(),
            TargetPrice = model.TargetPrice,
            ImageUrl = uploadResult.ImageUrl ?? currentDraft?.ImageUrl ?? "/images/products/default.svg"
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

        if (!TryBuildPreferencesDraft(model, out var preferencesDraft))
        {
            return View(model);
        }

        SaveDraftToTempData(PreferencesDraftTempDataKey, preferencesDraft!);

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

        var templatesByKey = AvailableStores.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var selectedStores = storesDraft.SelectedStoreKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key =>
            {
                var normalizedKey = key.Trim().ToLowerInvariant();
                var template = templatesByKey.TryGetValue(normalizedKey, out var storeTemplate)
                    ? storeTemplate
                    : new StoreTemplate(normalizedKey, normalizedKey, "Marketplace", false, normalizedKey[..1], "neutral", "#5b57f3");

                return new TrackedStoreRequest
                {
                    Key = template.Key,
                    Name = template.Name,
                    AccentColor = template.AccentColor
                };
            })
            .ToList();

        var creationResult = await _trackedProductCreationService.CreateAsync(
            userId,
            new TrackedProductCreationRequest
            {
                ProductName = createDraft!.Name,
                ProductCategory = createDraft.Category,
                ProductImageUrl = createDraft.ImageUrl,
                TargetPrice = preferencesDraft!.TargetPrice,
                MaximumPrice = preferencesDraft.MaximumPrice,
                AlertTrigger = preferencesDraft.AlertTrigger,
                EmailAlerts = preferencesDraft.EmailAlerts,
                PushNotifications = preferencesDraft.PushNotifications,
                DailySummary = preferencesDraft.DailySummary,
                Stores = selectedStores
            },
            cancellationToken);

        RemoveDraftFromTempData(CreateDraftTempDataKey);
        RemoveDraftFromTempData(PreferencesDraftTempDataKey);
        RemoveDraftFromTempData(StoresDraftTempDataKey);

        TempData["SuccessMessage"] = creationResult.CreatedCount > 0
            ? $"Monitoramento criado com sucesso em {creationResult.CreatedCount} loja(s)."
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

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            ModelState.AddModelError(nameof(CreateTrackedProductViewModel.Url), "Informe uma URL válida, incluindo http:// ou https://.");
        }
    }

    private bool TryBuildPreferencesDraft(TrackedProductPreferencesViewModel model, out TrackedProductPreferencesDraft? draft)
    {
        draft = null;

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
            return false;
        }

        draft = new TrackedProductPreferencesDraft
        {
            TargetPrice = targetPrice,
            MaximumPrice = maximumPrice,
            AlertTrigger = model.AlertTrigger,
            EmailAlerts = model.EmailAlerts,
            PushNotifications = model.PushNotifications,
            DailySummary = model.DailySummary
        };

        return true;
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
