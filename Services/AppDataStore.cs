using System.Text.Json;
using SmartOutfitAssistant.Models;

namespace SmartOutfitAssistant.Services;

public sealed class AppDataStore
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _wardrobePath;
    private readonly string _historyPath;
    private readonly string _settingsPath;

    public AppDataStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _wardrobePath = Path.Combine(dataDir, "wardrobe.json");
        _historyPath = Path.Combine(dataDir, "history.json");
        _settingsPath = Path.Combine(dataDir, "settings.json");
    }

    public async Task<List<WardrobeItem>> GetWardrobeAsync()
    {
        await _mutex.WaitAsync();
        try { return await ReadListUnsafeAsync<WardrobeItem>(_wardrobePath); }
        finally { _mutex.Release(); }
    }

    public async Task AddWardrobeItemsAsync(IEnumerable<WardrobeItem> items)
    {
        await _mutex.WaitAsync();
        try
        {
            var all = await ReadListUnsafeAsync<WardrobeItem>(_wardrobePath);
            all.AddRange(items);
            await WriteUnsafeAsync(_wardrobePath, all.OrderByDescending(x => x.CreatedAt).ToList());
        }
        finally { _mutex.Release(); }
    }

    public async Task<WardrobeItem?> UpdateWardrobeItemAsync(string id, WardrobeUpdateRequest request)
    {
        await _mutex.WaitAsync();
        try
        {
            var all = await ReadListUnsafeAsync<WardrobeItem>(_wardrobePath);
            var item = all.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null) return null;

            item.Name = Clean(request.Name, item.Name);
            item.Category = Clean(request.Category, item.Category);
            item.Color = Clean(request.Color, item.Color);
            item.Material = Clean(request.Material, item.Material);
            item.Season = Clean(request.Season, item.Season);
            item.Occasion = Clean(request.Occasion, item.Occasion);
            item.Notes = request.Notes?.Trim() ?? string.Empty;
            item.Tags = request.Tags.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
            item.Favorite = request.Favorite;
            item.UpdatedAt = DateTimeOffset.Now;

            await WriteUnsafeAsync(_wardrobePath, all.OrderByDescending(x => x.UpdatedAt).ToList());
            return item;
        }
        finally { _mutex.Release(); }
    }

    public async Task<WardrobeItem?> DeleteWardrobeItemAsync(string id)
    {
        await _mutex.WaitAsync();
        try
        {
            var all = await ReadListUnsafeAsync<WardrobeItem>(_wardrobePath);
            var item = all.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null) return null;
            all.Remove(item);
            await WriteUnsafeAsync(_wardrobePath, all);
            return item;
        }
        finally { _mutex.Release(); }
    }

    public async Task<List<OutfitResponse>> GetHistoryAsync()
    {
        await _mutex.WaitAsync();
        try { return await ReadListUnsafeAsync<OutfitResponse>(_historyPath); }
        finally { _mutex.Release(); }
    }

    public async Task AddHistoryAsync(OutfitResponse response)
    {
        await _mutex.WaitAsync();
        try
        {
            var all = await ReadListUnsafeAsync<OutfitResponse>(_historyPath);
            all.Insert(0, response);
            await WriteUnsafeAsync(_historyPath, all.Take(100).ToList());
        }
        finally { _mutex.Release(); }
    }

    public async Task<OutfitResponse?> ToggleHistoryFavoriteAsync(string id)
    {
        await _mutex.WaitAsync();
        try
        {
            var all = await ReadListUnsafeAsync<OutfitResponse>(_historyPath);
            var item = all.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null) return null;
            item.Favorite = !item.Favorite;
            await WriteUnsafeAsync(_historyPath, all);
            return item;
        }
        finally { _mutex.Release(); }
    }

    public async Task ClearHistoryAsync()
    {
        await _mutex.WaitAsync();
        try { await WriteUnsafeAsync(_historyPath, new List<OutfitResponse>()); }
        finally { _mutex.Release(); }
    }

    public async Task<UserSettings> GetSettingsAsync()
    {
        await _mutex.WaitAsync();
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = new UserSettings();
                await WriteUnsafeAsync(_settingsPath, defaults);
                return defaults;
            }
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<UserSettings>(stream, _json) ?? new UserSettings();
        }
        finally { _mutex.Release(); }
    }

    public async Task<UserSettings> SaveSettingsAsync(UserSettings settings)
    {
        settings.PreferredColors = settings.PreferredColors.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().Take(12).ToList();
        settings.AvoidColors = settings.AvoidColors.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().Take(12).ToList();
        await _mutex.WaitAsync();
        try
        {
            await WriteUnsafeAsync(_settingsPath, settings);
            return settings;
        }
        finally { _mutex.Release(); }
    }

    public async Task<AppBackup> ExportAsync()
    {
        await _mutex.WaitAsync();
        try
        {
            UserSettings settings;
            if (File.Exists(_settingsPath))
            {
                await using var settingsStream = File.OpenRead(_settingsPath);
                settings = await JsonSerializer.DeserializeAsync<UserSettings>(settingsStream, _json) ?? new UserSettings();
            }
            else
            {
                settings = new UserSettings();
            }

            return new AppBackup
            {
                ExportedAt = DateTimeOffset.Now,
                Wardrobe = await ReadListUnsafeAsync<WardrobeItem>(_wardrobePath),
                History = await ReadListUnsafeAsync<OutfitResponse>(_historyPath),
                Settings = settings
            };
        }
        finally { _mutex.Release(); }
    }

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        var wardrobe = await GetWardrobeAsync();
        var history = await GetHistoryAsync();
        return new DashboardSummary
        {
            WardrobeCount = wardrobe.Count,
            FavoriteWardrobeCount = wardrobe.Count(x => x.Favorite),
            HistoryCount = history.Count,
            OwnedPiecesUsed = history.SelectMany(x => x.Pieces).Count(x => x.Source == "衣柜"),
            RecommendedPiecesUsed = history.SelectMany(x => x.Pieces).Count(x => x.Source != "衣柜"),
            CategoryDistribution = wardrobe.GroupBy(x => x.Category).OrderByDescending(x => x.Count()).ToDictionary(x => x.Key, x => x.Count()),
            ColorDistribution = wardrobe.GroupBy(x => x.Color).OrderByDescending(x => x.Count()).Take(10).ToDictionary(x => x.Key, x => x.Count())
        };
    }

    private async Task<List<T>> ReadListUnsafeAsync<T>(string path)
    {
        if (!File.Exists(path)) return new List<T>();
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, _json) ?? new List<T>();
    }

    private async Task WriteUnsafeAsync<T>(string path, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, _json);
        }
        File.Move(tempPath, path, true);
    }

    private static string Clean(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
