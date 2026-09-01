using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Remal.Application.Common.Interfaces;

namespace Remal.Infrastructure.Services;

public class EmailOptions
{
    public string FromName { get; set; } = "Remal";
    public string FromAddress { get; set; } = "no-reply@remal.eg";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5500";
    public bool LogOnly { get; set; } = true; // when true, just logs (per spec)

    // SMTP: عند تعبئة SmtpHost و LogOnly=false يتم الإرسال الفعلي عبر SmtpEmailService.
    //
    // الطريقة (أ) — سيرفر MonsterASP المحلي (مُوصى بها؛ الإيميلات تطلع من دومينك):
    //   SmtpHost = siteXXXX.siteasp.net   (من Control Panel > Manage > Overview > Server)
    //   SmtpPort = 25 , SmtpUseSsl = false , SmtpUser/SmtpPassword فاضيين (بدون مصادقة)
    //   FromAddress = no-reply@remalfragrances.com
    //
    // الطريقة (ب) — Gmail:
    //   SmtpHost = smtp.gmail.com , SmtpPort = 587 , SmtpUseSsl = true
    //   SmtpUser = your@gmail.com , SmtpPassword = App-Password
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 25;
    public bool SmtpUseSsl { get; set; } = false;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
}

/// <summary>
/// Per-spec: confirmation links are LOGGED (not actually sent). Swap with SMTP / Resend later
/// by introducing a second implementation behind the same IEmailService.
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;
    private readonly EmailOptions _opts;

    public LoggingEmailService(ILogger<LoggingEmailService> logger, IOptions<EmailOptions> opts)
    {
        _logger = logger; _opts = opts.Value;
    }

    public Task SendEmailConfirmationAsync(string toEmail, string fullName, string confirmationUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("✉️ [EMAIL][CONFIRM] To={To} Name={Name} Link={Link}",
            toEmail, fullName, confirmationUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string fullName, string resetUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("✉️ [EMAIL][RESET] To={To} Name={Name} Link={Link}",
            toEmail, fullName, resetUrl);
        return Task.CompletedTask;
    }

    public Task SendOrderConfirmationAsync(string toEmail, string fullName, OrderEmailSummary order, CancellationToken ct = default)
    {
        _logger.LogInformation("✉️ [EMAIL][ORDER] To={To} Name={Name} OrderCode={Code} Total={Total} Lines={Lines}",
            toEmail, fullName, order.OrderCode, order.Total, order.Lines.Count);
        return Task.CompletedTask;
    }

    public Task SendWelcomeAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        _logger.LogInformation("✉️ [EMAIL][WELCOME] To={To} Name={Name}", toEmail, fullName);
        return Task.CompletedTask;
    }
}

/// <summary>
/// إرسال فعلي عبر SMTP (يعمل مع Gmail App Password أو أي مزود SMTP).
/// يُفعَّل تلقائياً من DI عندما تكون بيانات SMTP معبأة في الإعدادات و LogOnly=false.
/// أي فشل في الإرسال يُسجَّل ولا يُفشل العملية الأصلية (تسجيل/استعادة كلمة السر تستمر).
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly EmailOptions _opts;

    public SmtpEmailService(ILogger<SmtpEmailService> logger, IOptions<EmailOptions> opts)
    {
        _logger = logger; _opts = opts.Value;
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            var msg = new MimeMessage();
            // العنوان الظاهر للمستقبل = دومينك؛ لو فيه مستخدم مصادقة نستخدمه كـ envelope sender.
            var fromAddress = string.IsNullOrWhiteSpace(_opts.FromAddress) ? _opts.SmtpUser : _opts.FromAddress;
            msg.From.Add(new MailboxAddress(_opts.FromName, fromAddress));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            // اختيار وضع التشفير تلقائياً حسب البورت:
            //   465 → SSL ضمني (SslOnConnect) ، 587 → STARTTLS ، 25 → بدون/STARTTLS عند توفره
            var socketOpt = _opts.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect
                          : _opts.SmtpUseSsl ? SecureSocketOptions.StartTls
                          : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(_opts.SmtpHost, _opts.SmtpPort, socketOpt, ct);
            if (!string.IsNullOrWhiteSpace(_opts.SmtpUser))
                await client.AuthenticateAsync(_opts.SmtpUser, _opts.SmtpPassword, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("✉️ [SMTP] Sent '{Subject}' to {To}", subject, toEmail);
        }
        catch (Exception ex)
        {
            // لا نكسر العملية الأصلية — نسجل الفشل فقط (الرسالة الصامتة تحمي خصوصية الحسابات)
            _logger.LogError(ex, "✉️ [SMTP] FAILED sending '{Subject}' to {To}", subject, toEmail);
        }
    }

    // ══════════════ قوالب البريد ══════════════
    //
    // ثلاث قيود بتحكم كل سطر هنا، ومخالفة أي واحد فيهم بتكسر الرسالة عند نسبة
    // كبيرة من المستقبِلين:
    //
    // ١) **مفيش JavaScript.** كل عملاء البريد بيشيلوه. يعني "زرار Copy" بالمعنى
    //    الحرفي مستحيل تقنيًا — الحل البديل موجود تحت في OrderCodeBlock.
    // ٢) **CSS داخلي وجداول.** Outlook بيستخدم محرك Word في التنسيق: مفيش flex
    //    ولا grid ولا position. الجداول هي الطريقة الوحيدة المضمونة للتخطيط.
    // ٣) **الوضع الداكن مش مضمون.** Gmail و Outlook بيعكسوا الألوان بنفسهم بطريقة
    //    عشوائية. بنستخدم prefers-color-scheme للعملاء اللي بيحترموه (Apple Mail
    //    و iOS و Thunderbird)، وبنختار ألوان أساسية تفضل مقروءة حتى لو العميل
    //    عكسها بنفسه — يعني ما نعتمدش على الأبيض النقي كخلفية للنص المهم.

    private const string LogoUrl = "https://remalfragrances.com/logo-remal.png";
    private const string FontStack = "'Segoe UI', Tahoma, 'Helvetica Neue', Arial, sans-serif";

    // ⚠️ الشعار أبيض، فلازم يقعد على خلفية داكنة. كتير من عملاء البريد بيتجاهلوا
    // style="background:..." على الـ <td> فكان بيبان أبيض على أبيض. الحل: سمة
    // bgcolor القديمة (HTML مش CSS) — محترمة في كل العملاء تقريبًا.

    private static string Money(decimal v) => $"{v:0} ج.م";

    /// <summary>
    /// الغلاف العام. الوضع الداكن متطبّق بـ media query + كلاسات، والفاتح هو
    /// الافتراضي عشان العميل اللي مش بيدعم الـ query يشوف تصميم سليم.
    /// </summary>
    private static string Wrap(string title, string inner) => $@"
<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<meta name=""color-scheme"" content=""light dark"">
<meta name=""supported-color-schemes"" content=""light dark"">
<style>
  /* الوضع الداكن — بيشتغل في Apple Mail و iOS Mail و Thunderbird.
     Gmail بيتجاهله وبيعكس الألوان بنفسه، وعشان كده الألوان تحت مختارة
     بحيث تفضل مقروءة في الحالتين. */
  @media (prefers-color-scheme: dark) {{
    .r-bg     {{ background:#0e0e0e !important; }}
    .r-card   {{ background:#1a1a1a !important; }}
    .r-text   {{ color:#ededed !important; }}
    .r-muted  {{ color:#9d9d9d !important; }}
    .r-line   {{ border-color:#2e2e2e !important; }}
    .r-soft   {{ background:#212121 !important; }}
    .r-btn    {{ background:#ffffff !important; }}
    .r-btn a  {{ color:#111111 !important; }}
  }}
  /* الموبايل أولاً: الحشو بيقل والخط بيكبر شوية عشان القراءة بالإبهام */
  @media only screen and (max-width:600px) {{
    .r-pad   {{ padding:24px 18px !important; }}
    .r-code  {{ font-size:24px !important; letter-spacing:2px !important; }}
    .r-h     {{ font-size:19px !important; }}
    .r-hide-sm {{ display:none !important; }}
  }}
</style>
</head>
<body class=""r-bg"" style=""margin:0;padding:0;background:#f4f2ee;"">
<div class=""r-bg"" style=""margin:0;padding:28px 12px;background:#f4f2ee;font-family:{FontStack};"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:580px;margin:0 auto;"">
    <tr><td class=""r-card"" style=""background:#ffffff;border-radius:16px;overflow:hidden;"">

      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
        <tr>
          <td bgcolor=""#111111"" align=""center"" style=""background-color:#111111;padding:32px 24px;text-align:center;"">
            <img src=""{LogoUrl}"" width=""168"" height=""72"" alt=""رمال — REMAL FRAGRANCES""
                 style=""display:block;margin:0 auto;border:0;outline:none;text-decoration:none;width:168px;max-width:168px;height:auto;"" />
          </td>
        </tr>
        <tr><td style=""height:1px;background:#2a2a2a;line-height:1px;font-size:0;"">&nbsp;</td></tr>
        <tr>
          <td class=""r-pad r-text"" style=""padding:34px 30px;color:#1a1a1a;font-size:15px;line-height:1.9;"">
            <h1 class=""r-h r-text"" style=""margin:0 0 16px;font-size:21px;font-weight:700;color:#111111;letter-spacing:-0.2px;"">{title}</h1>
            {inner}
          </td>
        </tr>
        <tr>
          <td class=""r-soft"" style=""padding:22px 24px;text-align:center;background:#faf9f7;"">
            <div class=""r-muted"" style=""color:#8f8a83;font-size:11.5px;line-height:1.9;"">
              عطور نيش فاخرة — صناعة مصرية بزيوت مستوردة<br/>
              <a href=""https://remalfragrances.com"" class=""r-text"" style=""color:#111111;text-decoration:none;font-weight:600;"">remalfragrances.com</a>
              &nbsp;·&nbsp;
              <a href=""https://www.instagram.com/remalfragrances"" class=""r-muted"" style=""color:#8f8a83;text-decoration:none;"">Instagram</a>
              &nbsp;·&nbsp; © رمال {DateTime.UtcNow:yyyy}
            </div>
          </td>
        </tr>
      </table>

    </td></tr>
  </table>
</div>
</body>
</html>";

    /// <summary>زر CTA. جدول مش &lt;a&gt; لوحده عشان Outlook يحترم الخلفية.</summary>
    private static string Button(string href, string label) =>
        $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:26px auto;"">
             <tr><td class=""r-btn"" style=""border-radius:10px;background:#111111;"">
               <a href=""{href}"" style=""display:inline-block;padding:15px 36px;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:10px;letter-spacing:0.3px;"">{label}</a>
             </td></tr>
           </table>";

    /// <summary>
    /// كتلة رقم الطلب.
    ///
    /// <para><b>ليه مفيش زرار Copy:</b> عملاء البريد بيشيلوا الـ JavaScript كله،
    /// و<c>navigator.clipboard</c> ما بيشتغلش جوّه رسالة. أي "زرار نسخ" في إيميل
    /// هو إما صورة ما بتعملش حاجة أو رابط بيفتح صفحة — الاتنين بيوهموا المستخدم.</para>
    ///
    /// <para><b>البديل الأفضل فعليًا:</b> الرقم مكتوب بخط أحادي المسافة وكبير
    /// (اضغط مطوّلاً عليه في أي موبايل → نسخ)، و<b>تحته زر بيفتح صفحة التتبع
    /// والرقم متعبّي فيها أصلاً</b> — يعني العميل مش محتاج ينسخ من أساسه. ده
    /// أقل عدد نقرات ممكن، وهو الغرض الحقيقي من زرار النسخ.</para>
    /// </summary>
    private static string OrderCodeBlock(string code, string trackUrl) => $@"
      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:22px 0;"">
        <tr><td class=""r-soft r-line"" align=""center"" style=""background:#f7f5f2;border:1px solid #ece8e2;border-radius:12px;padding:20px 16px;text-align:center;"">
          <div class=""r-muted"" style=""color:#8f8a83;font-size:11px;letter-spacing:1.4px;text-transform:uppercase;margin-bottom:8px;"">رقم الطلب</div>
          <div class=""r-code r-text"" style=""font-family:'SFMono-Regular',Consolas,'Courier New',monospace;font-size:27px;font-weight:700;letter-spacing:3px;color:#111111;direction:ltr;"">{WebUtility.HtmlEncode(code)}</div>
          <div class=""r-muted"" style=""color:#8f8a83;font-size:11.5px;margin-top:10px;"">اضغط مطوّلاً على الرقم لنسخه — أو افتح صفحة التتبع من الزر تحت وهتلاقيه متعبّي</div>
        </tr>
      </table>
      {Button(trackUrl, "تتبع طلبك")}";

    /// <summary>جدول الفاتورة. عرض ثابت بالنسب المئوية عشان يتقلّص على الموبايل.</summary>
    private static string InvoiceTable(OrderEmailSummary o)
    {
        var rows = new System.Text.StringBuilder();
        foreach (var l in o.Lines)
        {
            var name = WebUtility.HtmlEncode(l.Name);
            var variant = string.IsNullOrWhiteSpace(l.Variant)
                ? ""
                : $@"<span class=""r-muted"" style=""color:#8f8a83;font-size:12px;""> · {WebUtility.HtmlEncode(l.Variant)}</span>";
            rows.Append($@"
              <tr>
                <td class=""r-line r-text"" style=""padding:12px 0;border-bottom:1px solid #f0ede8;font-size:14px;color:#1a1a1a;"">
                  {name}{variant}
                  <span class=""r-muted"" style=""color:#8f8a83;font-size:12px;""> × {l.Quantity}</span>
                </td>
                <td class=""r-line r-text"" align=""left"" style=""padding:12px 0;border-bottom:1px solid #f0ede8;font-size:14px;color:#1a1a1a;white-space:nowrap;direction:ltr;text-align:left;"">
                  {Money(l.UnitPrice * l.Quantity)}
                </td>
              </tr>");
        }

        string SummaryRow(string label, string value, bool strong = false, string? color = null) => $@"
              <tr>
                <td class=""{(strong ? "r-text" : "r-muted")}"" style=""padding:{(strong ? "14px 0 0" : "7px 0 0")};font-size:{(strong ? "16px" : "13.5px")};font-weight:{(strong ? "700" : "400")};color:{color ?? (strong ? "#111111" : "#6f6a64")};"">{label}</td>
                <td class=""{(strong ? "r-text" : "r-muted")}"" align=""left"" style=""padding:{(strong ? "14px 0 0" : "7px 0 0")};font-size:{(strong ? "16px" : "13.5px")};font-weight:{(strong ? "700" : "400")};color:{color ?? (strong ? "#111111" : "#6f6a64")};direction:ltr;text-align:left;white-space:nowrap;"">{value}</td>
              </tr>";

        var discountRow = o.Discount > 0
            ? SummaryRow("الخصم", "− " + Money(o.Discount), color: "#1a7f4b")
            : "";
        var shipping = o.ShippingFee <= 0 ? "مجاني" : Money(o.ShippingFee);
        var address = string.IsNullOrWhiteSpace(o.ShippingAddress) ? "" : $@"
          <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-top:22px;"">
            <tr><td class=""r-soft r-line"" style=""background:#f7f5f2;border:1px solid #ece8e2;border-radius:12px;padding:16px 18px;"">
              <div class=""r-muted"" style=""color:#8f8a83;font-size:11px;letter-spacing:1.2px;text-transform:uppercase;margin-bottom:6px;"">عنوان التوصيل</div>
              <div class=""r-text"" style=""color:#1a1a1a;font-size:13.5px;line-height:1.75;"">{WebUtility.HtmlEncode(o.ShippingAddress)}{(string.IsNullOrWhiteSpace(o.City) ? "" : " — " + WebUtility.HtmlEncode(o.City))}</div>
              <div class=""r-muted"" style=""color:#8f8a83;font-size:12.5px;margin-top:8px;"">طريقة الدفع: {WebUtility.HtmlEncode(o.PaymentMethod)}</div>
            </td></tr>
          </table>";

        return $@"
      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-top:26px;"">
        <tr><td colspan=""2"" class=""r-muted"" style=""color:#8f8a83;font-size:11px;letter-spacing:1.4px;text-transform:uppercase;padding-bottom:6px;"">تفاصيل الطلب</td></tr>
        {rows}
        {SummaryRow("المنتجات", Money(o.Subtotal))}
        {discountRow}
        {SummaryRow("الشحن", shipping)}
        {SummaryRow("الإجمالي", Money(o.Total), strong: true)}
      </table>
      {address}";
    }

    public Task SendEmailConfirmationAsync(string toEmail, string fullName, string confirmationUrl, CancellationToken ct = default)
        => SendAsync(toEmail, "تأكيد بريدك — رمال",
            Wrap("أهلاً " + WebUtility.HtmlEncode(fullName),
                $@"<p style=""margin:0 0 4px;"">خطوة أخيرة لتفعيل حسابك — اضغط الزر لتأكيد بريدك الإلكتروني:</p>
                   {Button(confirmationUrl, "تأكيد البريد")}"), ct);

    public Task SendPasswordResetAsync(string toEmail, string fullName, string resetUrl, CancellationToken ct = default)
        => SendAsync(toEmail, "استعادة كلمة السر — رمال",
            Wrap("أهلاً " + WebUtility.HtmlEncode(fullName),
                $@"<p style=""margin:0 0 4px;"">وصلنا طلب لاستعادة كلمة السر الخاصة بحسابك. اضغط الزر لتعيين كلمة سر جديدة:</p>
                   {Button(resetUrl, "تعيين كلمة سر جديدة")}
                   <p class=""r-muted"" style=""color:#8f8a83;font-size:12.5px;margin:6px 0 0;"">لو ما طلبتش ده، تجاهل الرسالة وكلمة السر هتفضل زي ما هي. الرابط صالح لمدة محدودة ويُستخدم مرة واحدة.</p>"), ct);

    public Task SendOrderConfirmationAsync(string toEmail, string fullName, OrderEmailSummary order, CancellationToken ct = default)
    {
        // الرقم بيروح في الرابط عشان صفحة التتبع تتعبّى لوحدها — العميل ما يحتاجش ينسخ.
        var trackUrl = $"{_opts.FrontendBaseUrl}/tracking?code={Uri.EscapeDataString(order.OrderCode)}";
        return SendAsync(toEmail, $"تأكيد طلبك {order.OrderCode} — رمال",
            Wrap("شكراً لطلبك يا " + WebUtility.HtmlEncode(fullName),
                $@"<p style=""margin:0;"">استلمنا طلبك وجاري تجهيزه بعناية. هنتواصل معاك قبل الشحن.</p>
                   {OrderCodeBlock(order.OrderCode, trackUrl)}
                   {InvoiceTable(order)}"), ct);
    }

    public Task SendWelcomeAsync(string toEmail, string fullName, CancellationToken ct = default)
        => SendAsync(toEmail, "أهلاً بيك في عيلة رمال",
            Wrap("أهلاً " + WebUtility.HtmlEncode(fullName),
                $@"<p style=""margin:0 0 4px;"">حسابك اتعمل بنجاح، ورصيدك دلوقتي <b>١٠٠ نقطة ترحيبية</b>.</p>
                   <p style=""margin:0;"">اكتشف عطورنا النيش الفاخرة وعروض الباقات الموسمية:</p>
                   {Button(_opts.FrontendBaseUrl, "تسوّق دلوقتي")}"), ct);
}
