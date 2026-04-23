namespace PromoRadar.Web.Services;

public static class ProductTextNormalizer
{
    public static string NormalizeLookupValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return compact.ToUpperInvariant();
    }
}
