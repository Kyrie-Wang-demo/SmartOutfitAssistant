using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SmartOutfitAssistant.Models;

namespace SmartOutfitAssistant.Services;

public sealed class ImageSearchService
{
    private readonly HttpClient _http;
    private readonly IWebHostEnvironment _env;
    private readonly ImageSearchOptions _options;
    private readonly ILogger<ImageSearchService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ImageSearchService(HttpClient http, IWebHostEnvironment env, IOptions<ImageSearchOptions> options, ILogger<ImageSearchService> logger)
    {
        _http = http;
        _env = env;
        _options = options.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 SmartOutfitAssistant/1.0 (+local-app)");
    }

    public async Task<OutfitResponse> EnrichRecommendedImagesAsync(OutfitResponse response, bool enabledByUser, CancellationToken ct = default)
    {
        if (!_options.Enabled || !enabledByUser) return response;
        var pieces = await Task.WhenAll(response.Pieces.Select(piece => EnrichPieceAsync(piece, response, ct)));
        response.Pieces = pieces.ToList();
        return response;
    }

    private async Task<OutfitPiece> EnrichPieceAsync(OutfitPiece piece, OutfitResponse response, CancellationToken ct)
    {
        if (piece.Source == "衣柜" || !string.IsNullOrWhiteSpace(piece.ImagePath)) return piece;
        var query = BuildQuery(piece, response.Mood, response.Occasion);
        var searchUrl = $"https://www.bing.com/images/search?q={Uri.EscapeDataString(query)}";
        var found = await SearchImageAsync(query, ct) ?? BuildFallback(query);
        var cached = _options.CacheRemoteImages ? await CacheImageAsync(found.Url, query, ct) : null;
        piece.ImagePath = cached ?? found.Url;
        piece.SearchUrl = searchUrl;
        piece.ImageCredit = cached is not null ? $"本地缓存 · {found.Credit}" : found.Credit;
        piece.Note += " 已自动联网搜索并呈现一张接近款式的参考图。";
        return piece;
    }

    private async Task<ImageSearchResult?> SearchImageAsync(string query, CancellationToken ct)
    {
        foreach (var provider in _options.ProviderOrder.Select(x => x.Trim().ToLowerInvariant()))
        {
            var result = provider switch
            {
                "duckduckgo" => await SearchDuckDuckGoAsync(query, ct),
                "bing" => await SearchBingAsync(query, ct),
                "unsplash" => await SearchUnsplashAsync(query, ct),
                "loremflickr" => BuildFallback(query),
                _ => null
            };
            if (result is not null) return result;
        }
        return null;
    }

    private async Task<ImageSearchResult?> SearchUnsplashAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.UnsplashAccessKey)) return null;
        try
        {
            var url = $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(query)}&per_page=1&orientation=portrait&client_id={Uri.EscapeDataString(_options.UnsplashAccessKey)}";
            await using var stream = await _http.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var raw = doc.RootElement.GetProperty("results").EnumerateArray().FirstOrDefault();
            if (raw.ValueKind == JsonValueKind.Undefined) return null;
            var image = raw.GetProperty("urls").GetProperty("regular").GetString();
            return string.IsNullOrWhiteSpace(image) ? null : new ImageSearchResult(image, "Unsplash");
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Unsplash failed"); return null; }
    }

    private async Task<ImageSearchResult?> SearchDuckDuckGoAsync(string query, CancellationToken ct)
    {
        try
        {
            var html = await _http.GetStringAsync($"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}&iax=images&ia=images", ct);
            var vqd = Regex.Match(html, "vqd=['\"](?<vqd>[^'\"]+)['\"]").Groups["vqd"].Value;
            if (string.IsNullOrWhiteSpace(vqd)) return null;
            await using var json = await _http.GetStreamAsync($"https://duckduckgo.com/i.js?l=us-en&o=json&q={Uri.EscapeDataString(query)}&vqd={Uri.EscapeDataString(vqd)}&f=,,,&p=1", ct);
            var payload = await JsonSerializer.DeserializeAsync<DuckPayload>(json, JsonOptions, ct);
            var first = payload?.Results?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Image));
            return first is null ? null : new ImageSearchResult(first.Image!, "DuckDuckGo 图片搜索");
        }
        catch (Exception ex) { _logger.LogDebug(ex, "DuckDuckGo failed"); return null; }
    }

    private async Task<ImageSearchResult?> SearchBingAsync(string query, CancellationToken ct)
    {
        try
        {
            var html = await _http.GetStringAsync($"https://www.bing.com/images/search?q={Uri.EscapeDataString(query)}&form=HDRSC2&first=1", ct);
            var match = Regex.Match(html, "murl&quot;:&quot;(?<url>.*?)&quot;");
            if (!match.Success) match = Regex.Match(html, "\\\"murl\\\"\\s*:\\s*\\\"(?<url>.*?)\\\"");
            if (!match.Success) return null;
            var imageUrl = WebUtility.HtmlDecode(match.Groups["url"].Value).Replace("\\/", "/").Replace("\\u0026", "&");
            return Uri.TryCreate(imageUrl, UriKind.Absolute, out _) ? new ImageSearchResult(imageUrl, "Bing 图片搜索") : null;
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Bing failed"); return null; }
    }

    private async Task<string?> CacheImageAsync(string url, string query, CancellationToken ct)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return null;
            var media = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!media.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
            var ext = media.Contains("png") ? ".png" : media.Contains("webp") ? ".webp" : media.Contains("gif") ? ".gif" : ".jpg";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url + query))).ToLowerInvariant()[..24];
            var relative = $"/generated/reference-images/{hash}{ext}";
            var path = Path.Combine(_env.WebRootPath, "generated", "reference-images", hash + ext);
            if (File.Exists(path)) return relative;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(path);
            await input.CopyToAsync(output, ct);
            return relative;
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Image cache failed for {Url}", url); return null; }
    }

    private static ImageSearchResult BuildFallback(string query)
    {
        var tags = Regex.Replace(query.ToLowerInvariant(), "[^a-z0-9]+", ",").Trim(',');
        if (string.IsNullOrWhiteSpace(tags)) tags = "fashion,outfit";
        return new ImageSearchResult($"https://loremflickr.com/720/960/{Uri.EscapeDataString(tags)}", "LoremFlickr 关键词图片");
    }

    private static string BuildQuery(OutfitPiece piece, string mood, string occasion)
    {
        return string.Join(' ', new[] { TranslateColor(piece.Color), TranslateMaterial(piece.Material), TranslateCategory(piece.Category, piece.Slot), TranslateMood(mood), TranslateOccasion(occasion), "fashion outfit product photo" }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string TranslateCategory(string category, string slot)
    {
        var t = category + " " + slot;
        if (Any(t, "短靴", "雪地靴", "靴")) return "ankle boots";
        if (Any(t, "跑步鞋", "运动鞋")) return "sneakers";
        if (Any(t, "帆布鞋")) return "canvas shoes";
        if (Any(t, "乐福鞋")) return "loafers";
        if (Any(t, "凉鞋")) return "sandals";
        if (Any(t, "鞋")) return "shoes";
        if (Any(t, "西装")) return "blazer";
        if (Any(t, "风衣")) return "trench coat";
        if (Any(t, "羽绒")) return "down jacket";
        if (Any(t, "大衣")) return "wool coat";
        if (Any(t, "夹克")) return "jacket";
        if (Any(t, "开衫")) return "cardigan";
        if (Any(t, "围巾")) return "scarf";
        if (Any(t, "帽")) return "hat";
        if (Any(t, "伞")) return "umbrella";
        if (Any(t, "墨镜")) return "sunglasses";
        if (Any(t, "衬衫")) return "shirt";
        if (Any(t, "T恤", "t恤")) return "t shirt";
        if (Any(t, "卫衣")) return "hoodie";
        if (Any(t, "针织", "毛衣")) return "knit sweater";
        if (Any(t, "裙")) return "skirt";
        if (Any(t, "短裤")) return "shorts";
        if (Any(t, "阔腿裤")) return "wide leg pants";
        if (Any(t, "西裤")) return "tailored trousers";
        if (Any(t, "牛仔")) return "jeans";
        if (Any(t, "裤")) return "pants";
        return slot switch { "上衣" => "top", "下装" => "bottom clothing", "鞋子" => "shoes", _ => "fashion accessory" };
    }

    private static string TranslateColor(string color)
    {
        if (color.StartsWith('#')) return string.Empty;
        if (Any(color, "黄")) return "yellow"; if (Any(color, "白")) return "white"; if (Any(color, "蓝", "藏青")) return "blue"; if (Any(color, "绿")) return "green"; if (Any(color, "灰")) return "gray"; if (Any(color, "黑")) return "black"; if (Any(color, "橙")) return "orange"; if (Any(color, "燕麦", "米", "卡其")) return "beige"; if (Any(color, "粉")) return "pink"; if (Any(color, "红")) return "red"; if (Any(color, "紫")) return "purple"; return string.Empty;
    }
    private static string TranslateMaterial(string m) { if (Any(m, "防水")) return "waterproof"; if (Any(m, "防风")) return "windproof"; if (Any(m, "羊毛", "羊绒")) return "wool"; if (Any(m, "棉麻", "亚麻")) return "linen"; if (Any(m, "牛仔")) return "denim"; if (Any(m, "针织")) return "knit"; return string.Empty; }
    private static string TranslateMood(string m) { if (Any(m, "正式", "通勤", "面试")) return "business casual"; if (Any(m, "浪漫", "约会")) return "romantic"; if (Any(m, "活力", "运动")) return "sporty"; if (Any(m, "慵懒", "放松")) return "casual relaxed"; return "daily wear"; }
    private static string TranslateOccasion(string o) { if (Any(o, "会议", "面试", "商务")) return "office wear"; if (Any(o, "约会")) return "date outfit"; if (Any(o, "旅行")) return "travel outfit"; return "daily outfit"; }
    private static bool Any(string value, params string[] keys) => keys.Any(k => value.Contains(k, StringComparison.OrdinalIgnoreCase));
    private sealed record ImageSearchResult(string Url, string Credit);
    private sealed record DuckPayload(IReadOnlyList<DuckItem>? Results);
    private sealed record DuckItem(string? Image, string? Thumbnail, string? Title, string? Url);
}
