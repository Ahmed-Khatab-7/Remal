namespace Remal.Api.Middleware;

/// <summary>Adds basic security response headers (per spec).</summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["X-XSS-Protection"] = "1; mode=block";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=(self)";
        // CSP — baseline policy. We allow Google Identity Services, inline scripts/styles
        // (Remal storefront has heavy inline CSS/JS today; tighten later by moving to nonces),
        // and HTTPS images from any origin (product images come from various CDNs).
        // Don't add CSP on API responses — only on HTML/static pages.
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            // ملاحظة: googletagmanager + google-analytics مسموحان من أجل Google Analytics 4
            // (GA4 يرسل القياسات إلى نطاقات إقليمية مثل region1.google-analytics.com).
            // ⚠️ أي منصة تتبع جديدة لازم نطاقاتها تتضاف هنا في script-src **و** connect-src،
            // وإلا المتصفح بيمنع تشغيل السكربت من غير ما يظهر خطأ واضح — البيكسل يبان
            // "مركّب" في Events Manager وهو مش بيجمع ولا حدث. حصل بالظبط مع بيكسل Meta.
            // connect.facebook.net = ملف البيكسل · www.facebook.com = نداءات الأحداث (/tr)
            h["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://accounts.google.com https://cdn.jsdelivr.net " +
                    "https://www.googletagmanager.com https://connect.facebook.net https://analytics.tiktok.com; " +
                // accounts.google.com لازم هنا كمان مش في script-src بس — زرار
                // "تسجيل الدخول بجوجل" بيحمّل ستايل خاص بيه، وبدونه الزرار بيظهر
                // مكسور الشكل من غير أي خطأ واضح للمستخدم.
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://accounts.google.com; " +
                "font-src 'self' https://fonts.gstatic.com data:; " +
                "img-src 'self' https: data: blob:; " +
                "connect-src 'self' https://accounts.google.com https://oauth2.googleapis.com https://wa.me " +
                    "https://www.googletagmanager.com https://*.google-analytics.com https://*.analytics.google.com " +
                    "https://connect.facebook.net https://www.facebook.com https://analytics.tiktok.com; " +
                "frame-src https://accounts.google.com https://www.facebook.com; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                // ⚠️ www.facebook.com **ضروري** هنا. لما حمولة حدث البيكسل تكبر على
                // beacon من نوع GET، ملف fbevents.js بيبعتها عن طريق <form> مخفي
                // بيعمل POST على facebook.com/tr/. مع "form-action 'self'" لوحدها
                // المتصفح كان **بيمنع الإرسال** — والحدث بيضيع من غير ما يظهر في
                // Events Manager ولا في أي تحذير. أخطر أنواع الأخطاء: صامت وبيأثر
                // على دقة الإعلانات وربط التحويلات.
                "form-action 'self' https://www.facebook.com;";
        }
        await _next(ctx);
    }
}
