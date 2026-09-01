namespace Remal.Application.Common.Interfaces;

/// <summary>
/// بيانات الحدث اللي بيتبعت لـ Meta من السيرفر (Conversions API).
/// </summary>
/// <param name="EventName">اسم الحدث القياسي عند Meta، مثل "Purchase".</param>
/// <param name="EventId">
/// نفس المعرّف اللي البيكسل بعت بيه من المتصفح — ده اللي بيخلي Meta تلغي التكرار
/// وتاخد أحسن نسخة من الحدثين بدل ما تحسبه مرتين.
/// </param>
/// <param name="Value">قيمة الطلب.</param>
/// <param name="Currency">العملة (EGP).</param>
/// <param name="OrderId">كود الطلب — Meta بتستخدمه كمان في منع التكرار.</param>
/// <param name="Contents">المنتجات: المعرّف والكمية والسعر.</param>
/// <param name="Email">بريد العميل (يتشفّر SHA-256 قبل الإرسال).</param>
/// <param name="Phone">موبايل العميل (يتحول لصيغة دولية ثم يتشفّر).</param>
/// <param name="FullName">اسم العميل (يتقسم لأول واسم عائلة ويتشفّر).</param>
/// <param name="City">المدينة (تتشفّر).</param>
/// <param name="SourceUrl">رابط الصفحة اللي حصل عليها الحدث.</param>
/// <param name="Fbp">كوكي _fbp من المتصفح — بيرفع جودة المطابقة كتير.</param>
/// <param name="Fbc">كوكي _fbc (بيتولّد من fbclid لما الزائر ييجي من إعلان).</param>
public record MetaEvent(
    string EventName,
    string? EventId = null,
    decimal? Value = null,
    string Currency = "EGP",
    string? OrderId = null,
    IReadOnlyList<MetaEventContent>? Contents = null,
    string? Email = null,
    string? Phone = null,
    string? FullName = null,
    string? City = null,
    string? SourceUrl = null,
    string? Fbp = null,
    string? Fbc = null,
    /// <summary>معرّف ثابت للعميل (يتشفّر) — Meta بتستخدمه لربط نفس الشخص عبر الأجهزة.</summary>
    string? ExternalId = null,
    /// <summary>اسم أول منتج — Meta بتوصي بيه في أحداث المحتوى.</summary>
    string? ContentName = null);

public record MetaEventContent(string Id, int Quantity, decimal ItemPrice);

/// <summary>
/// إرسال أحداث التحويل لـ Meta من السيرفر. المفروض ما يرميش استثناء أبدًا —
/// فشل التتبع ما ينفعش يوقف طلب حقيقي.
/// </summary>
public interface IMetaConversionsApi
{
    /// <summary>مفعّل فقط لو فيه Pixel ID و Access Token متسجّلين.</summary>
    bool IsConfigured { get; }

    Task SendAsync(MetaEvent evt, CancellationToken ct = default);
}

/// <summary>نسخة ما بتعملش حاجة — تُستخدم لما التتبع مش مسجّل (الاختبارات مثلاً).</summary>
public sealed class NullMetaConversionsApi : IMetaConversionsApi
{
    public static readonly NullMetaConversionsApi Instance = new();
    public bool IsConfigured => false;
    public Task SendAsync(MetaEvent evt, CancellationToken ct = default) => Task.CompletedTask;
}
