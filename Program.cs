using System.IO.Compression;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using SmartOutfitAssistant.Models;
using SmartOutfitAssistant.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ImageSearchOptions>(builder.Configuration.GetSection("ImageSearch"));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Upload"));
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 64 * 1024 * 1024);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddSingleton<AppDataStore>();
builder.Services.AddSingleton<OutfitService>();
builder.Services.AddHttpClient<ImageSearchService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalException");
            logger.LogError(feature?.Error, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await Results.Problem("服务暂时不可用，请稍后重试。", statusCode: 500).ExecuteAsync(context);
        });
    });
    app.UseHsts();
}

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name is "sw.js" or "manifest.webmanifest") ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

app.MapHealthChecks("/health");
var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new { status = "ok", app = "Smart Outfit Assistant", time = DateTimeOffset.Now }));
api.MapGet("/summary", async (AppDataStore store) => Results.Ok(await store.GetSummaryAsync()));

api.MapGet("/settings", async (AppDataStore store) => Results.Ok(await store.GetSettingsAsync()));
api.MapPut("/settings", async (UserSettings settings, AppDataStore store) => Results.Ok(await store.SaveSettingsAsync(settings)));

api.MapGet("/wardrobe", async (AppDataStore store, string? q, string? category, string? season, bool? favorite) =>
{
    var items = await store.GetWardrobeAsync();
    IEnumerable<WardrobeItem> query = items;
    if (!string.IsNullOrWhiteSpace(q))
    {
        query = query.Where(x => $"{x.Name} {x.Category} {x.Color} {x.Material} {x.Occasion} {string.Join(' ', x.Tags)}".Contains(q, StringComparison.OrdinalIgnoreCase));
    }
    if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(season)) query = query.Where(x => x.Season.Contains(season, StringComparison.OrdinalIgnoreCase));
    if (favorite.HasValue) query = query.Where(x => x.Favorite == favorite.Value);
    return Results.Ok(query.OrderByDescending(x => x.Favorite).ThenByDescending(x => x.UpdatedAt));
});

api.MapPost("/wardrobe/upload", async (HttpRequest request, AppDataStore store, IWebHostEnvironment env, IOptions<UploadOptions> uploadOptions) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "请使用 multipart/form-data 上传衣物图片。" });
    var form = await request.ReadFormAsync();
    var files = form.Files;
    if (files.Count == 0) return Results.BadRequest(new { error = "请选择至少一张图片。" });

    var category = Clean(form["category"], "上衣");
    var color = Clean(form["color"], "中性色");
    var material = Clean(form["material"], "未标注");
    var name = Clean(form["name"], string.Empty);
    var season = Clean(form["season"], "四季");
    var occasion = Clean(form["occasion"], "日常");
    var notes = Clean(form["notes"], string.Empty);
    var favorite = bool.TryParse(form["favorite"], out var fav) && fav;
    var tags = SplitTags(form["tags"]);

    var options = uploadOptions.Value;
    var maxBytes = Math.Max(1, options.MaxImageMegabytes) * 1024L * 1024L;
    var uploadDir = Path.Combine(env.WebRootPath, "uploads");
    Directory.CreateDirectory(uploadDir);
    var created = new List<WardrobeItem>();

    foreach (var file in files)
    {
        if (file.Length <= 0) continue;
        if (file.Length > maxBytes) return Results.BadRequest(new { error = $"{file.FileName} 超过 {options.MaxImageMegabytes}MB。" });
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = $"{file.FileName} 不是图片文件。" });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!options.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { error = $"{file.FileName} 格式不支持。" });
        var stored = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(uploadDir, stored);
        await using (var stream = File.Create(path)) await file.CopyToAsync(stream);
        var displayName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : (files.Count == 1 ? name : $"{name} {created.Count + 1}");
        created.Add(new WardrobeItem
        {
            Id = Guid.NewGuid().ToString("N"), Name = displayName, Category = category, Color = color, Material = material,
            Season = season, Occasion = occasion, Notes = notes, Tags = tags, Favorite = favorite,
            ImagePath = $"/uploads/{stored}", CreatedAt = DateTimeOffset.Now, UpdatedAt = DateTimeOffset.Now
        });
    }
    await store.AddWardrobeItemsAsync(created);
    return Results.Ok(created);
});

api.MapPut("/wardrobe/{id}", async (string id, WardrobeUpdateRequest update, AppDataStore store) =>
{
    var item = await store.UpdateWardrobeItemAsync(id, update);
    return item is null ? Results.NotFound(new { error = "未找到该衣物。" }) : Results.Ok(item);
});

api.MapDelete("/wardrobe/{id}", async (string id, AppDataStore store, IWebHostEnvironment env) =>
{
    var item = await store.DeleteWardrobeItemAsync(id);
    if (item is null) return Results.NotFound(new { error = "未找到该衣物。" });
    DeletePublicFileIfSafe(env.WebRootPath, item.ImagePath, "uploads");
    return Results.Ok(item);
});

api.MapPost("/outfits/recommend", async (OutfitRequest request, OutfitService outfitService, ImageSearchService imageSearch, AppDataStore store, CancellationToken ct) =>
{
    var wardrobe = await store.GetWardrobeAsync();
    var settings = await store.GetSettingsAsync();
    request.PreferWardrobe = request.PreferWardrobe && settings.PreferWardrobe;
    var result = outfitService.Recommend(request, wardrobe, settings);
    result = await imageSearch.EnrichRecommendedImagesAsync(result, settings.EnableOnlineImages, ct);
    await store.AddHistoryAsync(result);
    return Results.Ok(result);
});

api.MapGet("/history", async (AppDataStore store, bool favoritesOnly = false) =>
{
    var history = await store.GetHistoryAsync();
    return Results.Ok(favoritesOnly ? history.Where(x => x.Favorite) : history);
});
api.MapPost("/history/{id}/favorite", async (string id, AppDataStore store) =>
{
    var item = await store.ToggleHistoryFavoriteAsync(id);
    return item is null ? Results.NotFound(new { error = "未找到历史记录。" }) : Results.Ok(item);
});
api.MapDelete("/history", async (AppDataStore store) => { await store.ClearHistoryAsync(); return Results.Ok(new { ok = true }); });

api.MapGet("/export", async (AppDataStore store) =>
{
    var backup = await store.ExportAsync();
    return Results.Json(backup, contentType: "application/json", statusCode: 200);
});

app.MapFallbackToFile("index.html");
app.Run();

static string Clean(Microsoft.Extensions.Primitives.StringValues value, string fallback) => string.IsNullOrWhiteSpace(value.ToString()) ? fallback : value.ToString().Trim();
static List<string> SplitTags(Microsoft.Extensions.Primitives.StringValues value) => value.ToString().Split([',', '，', ';', '；', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
static void DeletePublicFileIfSafe(string webRoot, string? publicPath, string expectedTopFolder)
{
    if (string.IsNullOrWhiteSpace(publicPath)) return;
    var relative = publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
    if (!relative.StartsWith(expectedTopFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
    var full = Path.GetFullPath(Path.Combine(webRoot, relative));
    var root = Path.GetFullPath(Path.Combine(webRoot, expectedTopFolder));
    if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) File.Delete(full);
}
