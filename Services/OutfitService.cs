using SmartOutfitAssistant.Models;

namespace SmartOutfitAssistant.Services;

public sealed class OutfitService
{
    private static readonly Dictionary<string, MoodProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["开心"] = new("阳光治愈风", "明亮轻快、亲和力强，适合把好心情自然表达出来。", ["明黄", "奶油白", "天蓝", "草绿"], "短袖T恤/衬衫", "直筒牛仔裤/半身裙", "轻薄针织开衫", "白色帆布鞋"),
        ["忧郁"] = new("静谧松弛风", "低饱和蓝灰色系降低视觉压力，柔软材质带来稳定感。", ["雾霾蓝", "灰蓝", "米白", "深灰"], "柔软针织衫", "深灰直筒裤", "米白围巾", "灰色休闲鞋"),
        ["活力"] = new("运动活力风", "高对比色和机能感单品放大行动力。", ["橙色", "亮白", "电光蓝", "黑色"], "速干T恤/POLO", "运动短裤/工装裤", "棒球帽", "跑步鞋"),
        ["慵懒"] = new("奶油居家风", "宽松廓形与温柔中性色降低束缚感。", ["燕麦色", "米色", "浅灰", "卡其"], "宽松卫衣/针织衫", "阔腿裤", "软糯开衫", "德训鞋"),
        ["正式"] = new("清爽通勤风", "干净线条和经典配色提升专业感。", ["白色", "藏青", "黑色", "炭灰"], "白衬衫", "西裤/铅笔裙", "西装外套", "乐福鞋/低跟鞋"),
        ["浪漫"] = new("柔雾浪漫风", "柔和粉白与轻盈层次更亲和。", ["樱花粉", "奶油白", "酒红", "浅紫"], "飘带衬衫/细针织", "A字裙/浅色长裤", "珍珠项链", "玛丽珍鞋")
    };

    private static readonly Dictionary<string, string[]> SlotKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["上衣"] = ["上衣", "T恤", "衬衫", "卫衣", "针织", "POLO", "吊带", "毛衣", "背心"],
        ["下装"] = ["下装", "裤", "裙", "牛仔", "短裤", "西裤", "半身裙"],
        ["外套/配饰"] = ["外套", "夹克", "风衣", "西装", "大衣", "开衫", "配饰", "围巾", "帽", "伞", "项链", "包"],
        ["鞋子"] = ["鞋", "靴", "帆布鞋", "运动鞋", "乐福鞋", "凉鞋", "拖鞋"]
    };

    public OutfitResponse Recommend(OutfitRequest request, IReadOnlyList<WardrobeItem> wardrobe, UserSettings settings)
    {
        var mood = NormalizeMood(request.Mood, request.CustomMood);
        var profile = ResolveProfile(mood);
        var weather = NormalizeWeather(request.Weather);
        var occasion = string.IsNullOrWhiteSpace(request.Occasion) ? settings.DefaultOccasion : request.Occasion.Trim();
        var plan = BuildWeatherPlan(weather);
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var pieces = new List<OutfitPiece>
        {
            PickPiece("上衣", profile.Top, profile.Palette, plan.TopMaterial, occasion, wardrobe, usedIds, request.PreferWardrobe && settings.PreferWardrobe, settings),
            PickPiece("下装", profile.Bottom, profile.Palette, plan.BottomMaterial, occasion, wardrobe, usedIds, request.PreferWardrobe && settings.PreferWardrobe, settings),
            PickPiece("外套/配饰", plan.OuterOrAccessory ?? profile.Outer, profile.Palette, plan.OuterMaterial, occasion, wardrobe, usedIds, request.PreferWardrobe && settings.PreferWardrobe, settings),
            PickPiece("鞋子", plan.Shoes ?? profile.Shoes, profile.Palette, plan.ShoeMaterial, occasion, wardrobe, usedIds, request.PreferWardrobe && settings.PreferWardrobe, settings)
        };

        if (!string.IsNullOrWhiteSpace(plan.ExtraAccessory))
        {
            pieces.Add(PickPiece("外套/配饰", plan.ExtraAccessory, profile.Palette, plan.AccessoryMaterial, occasion, wardrobe, usedIds, request.PreferWardrobe && settings.PreferWardrobe, settings));
        }

        ApplyOccasionRefinement(occasion, pieces);

        var owned = pieces.Count(p => p.Source == "衣柜");
        var recommended = pieces.Count - owned;
        return new OutfitResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.Now,
            Mood = mood,
            Occasion = occasion,
            Weather = weather,
            StyleName = BuildStyleName(profile.StyleName, occasion, request.FitPreference, settings.StylePreference),
            StyleDescription = $"{profile.Description} 结合“{occasion}”场景与“{request.FitPreference}”版型偏好，形成完整且可执行的穿搭方案。",
            Pieces = pieces,
            Reasons =
            [
                $"心情“{mood}”对应 {string.Join("、", profile.Palette)} 色系，能更自然地表达当前状态。",
                owned > 0 ? $"已优先使用你衣柜中的 {owned} 件单品，减少额外购买。" : "当前衣柜中缺少匹配单品，已用联网参考图补齐完整造型。",
                settings.PreferredColors.Count > 0 ? $"已参考你的偏好色：{string.Join("、", settings.PreferredColors)}。" : "可在设置里填写偏好色，让推荐更个性化。"
            ],
            WeatherAdjustments = plan.Adjustments,
            ShoppingTips = BuildShoppingTips(pieces, weather),
            OwnedCount = owned,
            RecommendedCount = recommended
        };
    }

    private static OutfitPiece PickPiece(string slot, string preferredCategory, IReadOnlyList<string> palette, string material, string occasion, IReadOnlyList<WardrobeItem> wardrobe, ISet<string> usedIds, bool preferWardrobe, UserSettings settings)
    {
        if (preferWardrobe)
        {
            var keywords = SlotKeywords.TryGetValue(slot, out var values) ? values : [slot];
            var candidate = wardrobe
                .Where(item => !usedIds.Contains(item.Id))
                .Select(item => new { Item = item, Score = ScoreItem(item, keywords, preferredCategory, palette, material, occasion, settings) })
                .Where(x => x.Score >= 45)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Item.Favorite)
                .ThenByDescending(x => x.Item.UpdatedAt)
                .FirstOrDefault();

            if (candidate is not null)
            {
                usedIds.Add(candidate.Item.Id);
                var item = candidate.Item;
                return new OutfitPiece
                {
                    Slot = slot,
                    Category = item.Category,
                    Color = item.Color,
                    Material = item.Material,
                    Name = item.Name,
                    Source = "衣柜",
                    WardrobeItemId = item.Id,
                    ImagePath = item.ImagePath,
                    MatchScore = candidate.Score,
                    Note = $"来自你的衣柜；与“{preferredCategory} / {occasion}”匹配度 {candidate.Score} 分。"
                };
            }
        }

        var color = settings.PreferredColors.FirstOrDefault(c => palette.Any(p => p.Contains(c, StringComparison.OrdinalIgnoreCase) || c.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    ?? palette.FirstOrDefault(c => !settings.AvoidColors.Any(a => c.Contains(a, StringComparison.OrdinalIgnoreCase)))
                    ?? palette.FirstOrDefault()
                    ?? "中性色";
        return new OutfitPiece
        {
            Slot = slot,
            Category = preferredCategory,
            Color = color,
            Material = material,
            Name = $"推荐购买：{color}{preferredCategory}",
            Source = "推荐购买",
            Note = "衣柜中暂未找到合适单品，已用外部款式补齐。",
            MatchScore = 0
        };
    }

    private static int ScoreItem(WardrobeItem item, IReadOnlyList<string> slotKeywords, string preferredCategory, IReadOnlyList<string> palette, string material, string occasion, UserSettings settings)
    {
        var text = $"{item.Name} {item.Category} {item.Color} {item.Material} {item.Season} {item.Occasion} {string.Join(' ', item.Tags)}";
        var score = 0;
        if (slotKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) score += 80;
        if (preferredCategory.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) score += 25;
        if (palette.Any(c => item.Color.Contains(c, StringComparison.OrdinalIgnoreCase) || c.Contains(item.Color, StringComparison.OrdinalIgnoreCase))) score += 26;
        if (settings.PreferredColors.Any(c => item.Color.Contains(c, StringComparison.OrdinalIgnoreCase) || c.Contains(item.Color, StringComparison.OrdinalIgnoreCase))) score += 14;
        if (settings.AvoidColors.Any(c => item.Color.Contains(c, StringComparison.OrdinalIgnoreCase) || c.Contains(item.Color, StringComparison.OrdinalIgnoreCase))) score -= 35;
        if (IsNeutral(item.Color)) score += 10;
        if (!string.IsNullOrWhiteSpace(item.Material) && material.Contains(item.Material, StringComparison.OrdinalIgnoreCase)) score += 15;
        if (item.Occasion.Contains(occasion, StringComparison.OrdinalIgnoreCase) || occasion.Contains(item.Occasion, StringComparison.OrdinalIgnoreCase)) score += 12;
        if (item.Favorite) score += 8;
        return Math.Clamp(score, 0, 100);
    }

    private static WeatherInput NormalizeWeather(WeatherInput? weather)
    {
        weather ??= new WeatherInput();
        return new WeatherInput
        {
            Temperature = Math.Clamp(weather.Temperature, -30, 50),
            Condition = string.IsNullOrWhiteSpace(weather.Condition) ? "晴" : weather.Condition.Trim(),
            WindLevel = Math.Clamp(weather.WindLevel, 0, 12),
            Humidity = Math.Clamp(weather.Humidity, 0, 100)
        };
    }

    private static string NormalizeMood(string mood, string? customMood) => !string.IsNullOrWhiteSpace(customMood) ? customMood.Trim() : (string.IsNullOrWhiteSpace(mood) ? "开心" : mood.Trim());

    private static MoodProfile ResolveProfile(string mood)
    {
        if (Profiles.TryGetValue(mood, out var profile)) return profile;
        if (ContainsAny(mood, "低落", "忧", "sad")) return Profiles["忧郁"];
        if (ContainsAny(mood, "正式", "面试", "通勤", "work")) return Profiles["正式"];
        if (ContainsAny(mood, "活", "运动", "sport")) return Profiles["活力"];
        if (ContainsAny(mood, "爱", "约会", "浪漫")) return Profiles["浪漫"];
        if (ContainsAny(mood, "困", "懒", "放松")) return Profiles["慵懒"];
        return Profiles["开心"];
    }

    private static WeatherPlan BuildWeatherPlan(WeatherInput weather)
    {
        var a = new List<string>();
        string top, bottom, outer = "按需选择", shoe = "常规", accessory = "常规";
        string? outerCat = null, shoes = null, extra = null;
        if (weather.Temperature >= 30) { top = "棉麻/速干/轻薄透气"; bottom = "薄棉/亚麻"; outerCat = "防晒衬衫或遮阳帽"; outer = "UPF防晒"; shoes = "透气凉鞋/网面运动鞋"; shoe = "透气网面"; a.Add("高温：减少层数，优先棉麻、速干和宽松版型。"); }
        else if (weather.Temperature >= 24) { top = "纯棉/莫代尔"; bottom = "薄牛仔/棉质斜纹"; outerCat = "轻薄开衫/帽子"; outer = "薄针织/棉麻"; shoes = "帆布鞋/轻便运动鞋"; a.Add("温暖：保持清爽透气，外套作为空调房备用。"); }
        else if (weather.Temperature >= 15) { top = "棉质/薄针织"; bottom = "牛仔/斜纹棉"; outerCat = "薄夹克/针织开衫"; outer = "轻量风衣/薄针织"; shoes = "德训鞋/乐福鞋/短靴"; a.Add("舒适：增加可脱卸薄外套应对早晚温差。"); }
        else if (weather.Temperature >= 5) { top = "羊毛混纺/加厚棉"; bottom = "厚牛仔/灯芯绒"; outerCat = "呢大衣/棉服"; outer = "羊毛/夹棉"; shoes = "短靴/厚底运动鞋"; shoe = "皮革/厚底"; extra = "围巾"; accessory = "羊毛"; a.Add("低温：增加保暖外套和围巾，面料以羊毛混纺、夹棉为主。"); }
        else { top = "保暖内搭/羊毛"; bottom = "加绒/羊毛混纺"; outerCat = "羽绒服/厚大衣"; outer = "羽绒/防风"; shoes = "雪地靴/防滑短靴"; shoe = "防滑保暖"; extra = "围巾+手套"; accessory = "抓绒/羊毛"; a.Add("严寒：使用三层穿衣法，外层防风，中层保暖，内层亲肤。"); }
        if (ContainsAny(weather.Condition, "雨", "雪")) { shoes = weather.Condition.Contains("雪") ? "防滑保暖短靴" : "防水短靴/防水运动鞋"; shoe = "防水/防滑"; extra = weather.Condition.Contains("雪") ? "保暖帽" : "折叠伞/防水包"; accessory = "防水涂层"; a.Add($"{weather.Condition}：鞋履改为防水防滑款，并提醒携带雨具。"); }
        else if (ContainsAny(weather.Condition, "晴", "晒")) { extra ??= "墨镜/遮阳帽"; accessory = "防晒/轻量"; a.Add("晴天：加入墨镜、遮阳帽或防晒衬衫。"); }
        if (weather.WindLevel >= 6) { outerCat = weather.Temperature >= 24 ? "轻薄防风衬衫" : "防风夹克/风衣"; outer = "防风面料"; a.Add("风力较大：外层选择防风材质，避免过于飘逸的裙摆或围巾。"); }
        if (weather.Humidity >= 75 && weather.Temperature >= 24) a.Add("湿度偏高：选择速干、抗皱材质，避免厚重不透气单品。");
        return new(top, bottom, outer, shoe, accessory, outerCat, shoes, extra, a);
    }

    private static void ApplyOccasionRefinement(string occasion, List<OutfitPiece> pieces)
    {
        if (ContainsAny(occasion, "面试", "商务", "会议"))
        {
            foreach (var p in pieces.Where(p => p.Source != "衣柜" && p.Slot is "上衣" or "下装")) p.Note += " 场景偏正式，建议选择剪裁利落、少图案款。";
        }
        if (ContainsAny(occasion, "旅行", "逛街"))
        {
            foreach (var p in pieces.Where(p => p.Slot == "鞋子")) p.Note += " 出行场景优先舒适、防滑和易清洁。";
        }
    }

    private static List<string> BuildShoppingTips(IEnumerable<OutfitPiece> pieces, WeatherInput weather)
    {
        var tips = pieces.Where(p => p.Source != "衣柜").Select(p => $"补充 {p.Color}{p.Category} 时，优先看材质“{p.Material}”与版型舒适度。" ).ToList();
        if (weather.Condition.Contains("雨")) tips.Add("雨天购买鞋履时确认鞋面防泼水、鞋底防滑纹路。" );
        tips.Add("优先购买能与衣柜里 3 件以上单品复用的基础款，降低闲置率。" );
        return tips;
    }

    private static string BuildStyleName(string baseName, string occasion, string fit, string preference) => $"{baseName} · {occasion} · {fit}/{preference}";
    private static bool IsNeutral(string color) => new[] { "黑", "白", "灰", "米", "卡其", "藏青", "牛仔" }.Any(color.Contains);
    private static bool ContainsAny(string value, params string[] words) => words.Any(w => value.Contains(w, StringComparison.OrdinalIgnoreCase));
    private sealed record MoodProfile(string StyleName, string Description, IReadOnlyList<string> Palette, string Top, string Bottom, string Outer, string Shoes);
    private sealed record WeatherPlan(string TopMaterial, string BottomMaterial, string OuterMaterial, string ShoeMaterial, string AccessoryMaterial, string? OuterOrAccessory, string? Shoes, string? ExtraAccessory, List<string> Adjustments);
}
