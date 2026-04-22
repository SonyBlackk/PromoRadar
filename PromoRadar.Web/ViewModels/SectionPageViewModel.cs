namespace PromoRadar.Web.ViewModels;

public class SectionPageViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PrimaryActionText { get; set; } = "Voltar para Home";

    public string PrimaryActionUrl { get; set; } = "/";
}

