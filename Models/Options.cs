namespace SmartOutfitAssistant.Models;

public sealed class ImageSearchOptions
{
    public bool Enabled { get; set; } = true;
    public bool CacheRemoteImages { get; set; } = true;
    public string[] ProviderOrder { get; set; } = ["DuckDuckGo", "Bing", "LoremFlickr"];
    public string UnsplashAccessKey { get; set; } = string.Empty;
}

public sealed class UploadOptions
{
    public int MaxImageMegabytes { get; set; } = 12;
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
}
