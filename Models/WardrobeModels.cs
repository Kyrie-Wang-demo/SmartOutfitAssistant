namespace SmartOutfitAssistant.Models;

public sealed class WardrobeItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "未命名单品";
    public string Category { get; set; } = "上衣";
    public string Color { get; set; } = "中性色";
    public string Material { get; set; } = "未标注";
    public string Season { get; set; } = "四季";
    public string Occasion { get; set; } = "日常";
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool Favorite { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class WardrobeUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Occasion { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool Favorite { get; set; }
}

public sealed class WeatherInput
{
    public int Temperature { get; set; } = 22;
    public string Condition { get; set; } = "晴";
    public int WindLevel { get; set; } = 2;
    public int Humidity { get; set; } = 50;
}

public sealed class OutfitRequest
{
    public string Mood { get; set; } = "开心";
    public string? CustomMood { get; set; }
    public string Occasion { get; set; } = "日常";
    public string GenderExpression { get; set; } = "不限";
    public string FitPreference { get; set; } = "舒适";
    public bool PreferWardrobe { get; set; } = true;
    public WeatherInput Weather { get; set; } = new();
}

public sealed class OutfitPiece
{
    public string Slot { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = "推荐购买";
    public string? WardrobeItemId { get; set; }
    public string? ImagePath { get; set; }
    public string Note { get; set; } = string.Empty;
    public string? SearchUrl { get; set; }
    public string? ImageCredit { get; set; }
    public int MatchScore { get; set; }
}

public sealed class OutfitResponse
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string Mood { get; set; } = string.Empty;
    public string Occasion { get; set; } = string.Empty;
    public WeatherInput Weather { get; set; } = new();
    public string StyleName { get; set; } = string.Empty;
    public string StyleDescription { get; set; } = string.Empty;
    public List<OutfitPiece> Pieces { get; set; } = new();
    public List<string> Reasons { get; set; } = new();
    public List<string> WeatherAdjustments { get; set; } = new();
    public List<string> ShoppingTips { get; set; } = new();
    public int OwnedCount { get; set; }
    public int RecommendedCount { get; set; }
    public bool Favorite { get; set; }
}

public sealed class UserSettings
{
    public string DisplayName { get; set; } = "我";
    public string DefaultOccasion { get; set; } = "日常";
    public string StylePreference { get; set; } = "简约舒适";
    public string FitPreference { get; set; } = "舒适";
    public string GenderExpression { get; set; } = "不限";
    public List<string> PreferredColors { get; set; } = new() { "白色", "蓝色", "米色" };
    public List<string> AvoidColors { get; set; } = new();
    public bool EnableOnlineImages { get; set; } = true;
    public bool PreferWardrobe { get; set; } = true;
}

public sealed class AppBackup
{
    public int Version { get; set; } = 1;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
    public List<WardrobeItem> Wardrobe { get; set; } = new();
    public List<OutfitResponse> History { get; set; } = new();
    public UserSettings Settings { get; set; } = new();
}

public sealed class DashboardSummary
{
    public int WardrobeCount { get; set; }
    public int FavoriteWardrobeCount { get; set; }
    public int HistoryCount { get; set; }
    public int OwnedPiecesUsed { get; set; }
    public int RecommendedPiecesUsed { get; set; }
    public IReadOnlyDictionary<string, int> CategoryDistribution { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ColorDistribution { get; set; } = new Dictionary<string, int>();
}
