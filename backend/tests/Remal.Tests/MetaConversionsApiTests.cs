using Remal.Infrastructure.Services;
using Xunit;

namespace Remal.Tests;

/// <summary>
/// تطبيع بيانات العميل قبل تشفيرها لـ Meta. الدقة هنا مش تفصيلة: Meta بتطابق
/// بالهاش نفسه، فأي اختلاف في الصيغة = مطابقة فاشلة = Event Match Quality أقل
/// = تكلفة تحويل أعلى في الحملات.
/// </summary>
public class MetaConversionsApiTests
{
    [Theory]
    [InlineData("01114545419", "201114545419")]      // الصيغة المصرية المعتادة
    [InlineData("0111 454 5419", "201114545419")]    // بمسافات
    [InlineData("+201114545419", "201114545419")]    // دولية بعلامة +
    [InlineData("00201114545419", "201114545419")]   // دولية بصفرين
    [InlineData("201114545419", "201114545419")]     // دولية بدون بادئة
    [InlineData("1114545419", "201114545419")]       // من غير الصفر
    public void Phone_is_normalised_to_international_format(string input, string expected)
        => Assert.Equal(expected, MetaConversionsApi.NormalizePhone(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("مفيش أرقام")]
    public void Phone_without_digits_is_dropped(string? input)
        => Assert.Null(MetaConversionsApi.NormalizePhone(input));

    [Theory]
    // الواجهة بتبعت "المدينة — المحافظة"؛ Meta عايزة المدينة بس بدون مسافات ولا رموز
    [InlineData("الفشن — بني سويف", "الفشن")]
    [InlineData("مدينة نصر — القاهرة", "مدينةنصر")]
    [InlineData("القاهرة", "القاهرة")]
    [InlineData("El Fashn - Beni Suef", "ElFashn")]
    public void City_keeps_only_the_city_part(string input, string expected)
        => Assert.Equal(expected, MetaConversionsApi.NormalizeCity(input));

    [Fact]
    public void Full_name_splits_into_first_and_last()
    {
        Assert.Equal(("أحمد", "خطاب"), MetaConversionsApi.SplitName("أحمد خطاب"));
        Assert.Equal(("أحمد", "محمد خطاب"), MetaConversionsApi.SplitName("أحمد محمد خطاب"));
        Assert.Equal(("أحمد", null), MetaConversionsApi.SplitName("أحمد"));
        Assert.Equal((null, null), MetaConversionsApi.SplitName("   "));
    }

    [Fact]
    public void Hash_is_lowercase_hex_sha256()
    {
        // قيمة SHA-256 معروفة لـ "test" — لو التشفير اتغيّر، الاختبار ده هيمسك
        Assert.Equal("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
            MetaConversionsApi.Sha256("test"));
        Assert.Null(MetaConversionsApi.Sha256(null));
        Assert.Null(MetaConversionsApi.Sha256("  "));
    }

    [Fact]
    public void Hash_output_is_64_hex_chars_for_arabic_input()
    {
        var h = MetaConversionsApi.Sha256("أحمد");
        Assert.NotNull(h);
        Assert.Equal(64, h!.Length);
        Assert.Matches("^[0-9a-f]+$", h);
    }
}
