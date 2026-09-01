namespace Remal.Application.Common.Interfaces;

/// <summary>سطر واحد في فاتورة الإيميل.</summary>
public record OrderEmailLine(string Name, string? Variant, int Quantity, decimal UnitPrice);

/// <summary>
/// كل اللي الإيميل محتاجه عشان يطبع فاتورة كاملة.
/// كان بيتبعت الكود والإجمالي بس، فالعميل بياخد رقم من غير ما يعرف اشترى إيه —
/// وده بيزوّد رسايل "الطلب ده كان إيه؟" على الواتساب.
/// </summary>
public record OrderEmailSummary(
    string OrderCode,
    IReadOnlyList<OrderEmailLine> Lines,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Discount,
    decimal Total,
    string PaymentMethod,
    string? ShippingAddress,
    string? City);

/// <summary>
/// Email abstraction. Current implementation logs links (per spec). Swap with SMTP / Resend later.
/// </summary>
public interface IEmailService
{
    Task SendEmailConfirmationAsync(string toEmail, string fullName, string confirmationUrl, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string fullName, string resetUrl, CancellationToken ct = default);
    Task SendOrderConfirmationAsync(string toEmail, string fullName, OrderEmailSummary order, CancellationToken ct = default);
    Task SendWelcomeAsync(string toEmail, string fullName, CancellationToken ct = default);
}
