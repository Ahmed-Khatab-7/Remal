namespace Remal.Application.Common.Interfaces;

/// <summary>
/// إشعارات فورية لصاحب المتجر عبر بوت تليجرام.
///
/// <para><b>ليه تليجرام:</b> Web Push على الآيفون بيشتغل بس لو الداشبورد متثبّت
/// على الشاشة الرئيسية كتطبيق مستقل، والاشتراك بيتلغى بصمت لو التطبيق اتشال أو
/// الكاش اتنضّف — فتفوت طلبات من غير ما تعرف. تليجرام تطبيق أصلي: إشعاراته من
/// نظام آبل نفسه، بتوصل والموبايل مقفول، ومفيش إذن ممكن يتلغى.</para>
///
/// <para>Web Push موجود بالتوازي كطبقة تانية (لابتوب/أندرويد)، والاتنين مستقلين
/// عن بعض — فشل أي واحد ما بيأثرش على التاني ولا على الطلب نفسه.</para>
/// </summary>
public interface ITelegramNotifier
{
    /// <summary>مفعّل فقط لما التوكن ومعرّف المحادثة يكونوا محفوظين في الإعدادات.</summary>
    Task<bool> IsConfiguredAsync(CancellationToken ct = default);

    /// <summary>
    /// يبعت رسالة. أي فشل بيتسجّل في اللوج وبيرجع false — **ما بيرميش استثناء**،
    /// لأن الإشعار ما ينفعش يفشّل طلب حقيقي اتسجّل بالفعل.
    /// </summary>
    /// <param name="text">نص الرسالة (HTML بسيط مسموح: b, i, code, a).</param>
    Task<bool> SendAsync(string text, CancellationToken ct = default);
}
