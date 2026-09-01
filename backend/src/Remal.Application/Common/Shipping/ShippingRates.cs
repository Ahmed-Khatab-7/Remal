using System.Text.Json;

namespace Remal.Application.Common.Shipping;

/// <summary>
/// يحسب تكلفة الشحن من إعداد <c>shipping_rates_json</c> اللي بيتدار من لوحة التحكم.
/// <para>الصيغة المدعومة (v2) — محافظات وبداخل كل محافظة مدن اختيارية:</para>
/// <code>
/// { "v": 2, "govs": [
///     { "ar": "بني سويف", "en": "Beni Suef", "price": 45,
///       "cities": [ { "ar": "الفشن", "en": "El Fashn", "price": 20 } ] },
///     { "ar": "القاهرة", "en": "Cairo", "price": 60 }
/// ]}
/// </code>
/// <para>والصيغة القديمة (v1) لسه مدعومة عشان أي بيانات محفوظة قبل التحديث:</para>
/// <code>{ "القاهرة": 60, "أسوان": 120 }</code>
/// <para>
/// الواجهة بتبعت <c>City</c> بصيغة "المدينة — المحافظة" (أو المحافظة وحدها لو مالهاش مدن)،
/// فبنطابق بالاحتواء ونختار أطول اسم مطابق — أطول تطابق يمنع أي التباس بين الأسماء المتشابهة.
/// السعر النهائي بيتحسب في السيرفر دايمًا، فمفيش مجال لتلاعب العميل من المتصفح.
/// </para>
/// </summary>
public static class ShippingRates
{
    /// <summary>تكلفة الشحن للعنوان المُدخل، أو <paramref name="defaultFee"/> لو مفيش تطابق أو الإعداد تالف.</summary>
    public static decimal Resolve(string? ratesJson, string? city, decimal defaultFee)
    {
        if (string.IsNullOrWhiteSpace(ratesJson) || string.IsNullOrWhiteSpace(city)) return defaultFee;
        var target = city.Trim();
        if (target.Length == 0) return defaultFee;

        try
        {
            using var doc = JsonDocument.Parse(ratesJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return defaultFee;

            return root.TryGetProperty("govs", out var govs) && govs.ValueKind == JsonValueKind.Array
                ? ResolveV2(govs, target, defaultFee)
                : ResolveV1(root, target, defaultFee);
        }
        catch (JsonException) { return defaultFee; } // JSON تالف → السعر الافتراضي
    }

    // ===== v2: محافظة (+ مدن اختيارية) =====
    private static decimal ResolveV2(JsonElement govs, string target, decimal defaultFee)
    {
        JsonElement? matchedGov = null;
        var govNameLen = 0;
        var govPrice = defaultFee;

        foreach (var g in govs.EnumerateArray())
        {
            if (g.ValueKind != JsonValueKind.Object) continue;
            foreach (var name in Names(g))
            {
                if (name.Length <= govNameLen || !Contains(target, name)) continue;
                govNameLen = name.Length;
                matchedGov = g;
                govPrice = Price(g) ?? defaultFee;
            }
        }
        if (matchedGov is null) return defaultFee;

        // نطاق البحث عن المدينة = العنوان بعد شيل اسم المحافظة منه. الواجهة بتبعت
        // "المدينة — المحافظة" فاسم المحافظة في الآخر، عشان كده بنشيل آخر ظهور ليه.
        // من غير الخطوة دي، مدينة اسمها زي اسم محافظتها (بني سويف) كانت تكسب أي مدينة تانية
        // لمجرد إن اسمها أطول.
        var matchedName = MatchedName(matchedGov.Value, target, govNameLen);
        var cityScope = RemoveLast(target, matchedName);

        var price = govPrice;
        var cityNameLen = 0;
        if (matchedGov.Value.TryGetProperty("cities", out var cities) && cities.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cities.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;
                var p = Price(c);
                if (p is null) continue;
                foreach (var name in Names(c))
                {
                    if (name.Length <= cityNameLen || !Contains(cityScope, name)) continue;
                    cityNameLen = name.Length;
                    price = p.Value;
                }
            }
        }
        return price;
    }

    // ===== v1: كائن مسطّح { "المحافظة": السعر } =====
    private static decimal ResolveV1(JsonElement root, string target, decimal defaultFee)
    {
        string? bestKey = null;
        var bestVal = defaultFee;
        foreach (var prop in root.EnumerateObject())
        {
            var key = prop.Name.Trim();
            if (key.Length == 0) continue;
            var parsed = AsPrice(prop.Value);
            if (parsed is null) continue;

            if (string.Equals(target, key, StringComparison.OrdinalIgnoreCase)) return parsed.Value;
            if (Contains(target, key) && (bestKey is null || key.Length > bestKey.Length))
            { bestKey = key; bestVal = parsed.Value; }
        }
        return bestKey is null ? defaultFee : bestVal;
    }

    /// <summary>اسم المحافظة (عربي أو إنجليزي) اللي طابق فعلاً بالطول المحدد.</summary>
    private static string MatchedName(JsonElement gov, string target, int length)
    {
        foreach (var name in Names(gov))
            if (name.Length == length && Contains(target, name)) return name;
        return "";
    }

    private static string RemoveLast(string text, string needle)
    {
        if (needle.Length == 0) return text;
        var i = text.LastIndexOf(needle, StringComparison.OrdinalIgnoreCase);
        return i < 0 ? text : text.Remove(i, needle.Length);
    }

    private static IEnumerable<string> Names(JsonElement e)
    {
        foreach (var key in new[] { "ar", "en" })
        {
            if (!e.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.String) continue;
            var s = (v.GetString() ?? "").Trim();
            if (s.Length > 0) yield return s;
        }
    }

    private static decimal? Price(JsonElement e)
        => e.TryGetProperty("price", out var p) ? AsPrice(p) : null;

    private static decimal? AsPrice(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number => v.TryGetDecimal(out var n) && n >= 0 ? n : null,
        JsonValueKind.String => decimal.TryParse(v.GetString(), out var s) && s >= 0 ? s : null,
        _ => null,
    };

    private static bool Contains(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
