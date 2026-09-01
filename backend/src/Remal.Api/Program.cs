using System.IO.Compression;
using System.Text.Json.Serialization;
using FluentValidation.AspNetCore;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;
using Remal.Api.Hubs;
using Remal.Api.Middleware;
using Remal.Application;
using Remal.Application.Common.Interfaces;
using Remal.Infrastructure;
using Remal.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ----- Logging (Serilog) — pretty console in Dev, JSON in Prod -----
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.MinimumLevel.Is(ctx.HostingEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
      .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
      .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Application", "Remal.Api")
      .ReadFrom.Configuration(ctx.Configuration);

    if (ctx.HostingEnvironment.IsDevelopment())
        lc.WriteTo.Console();
    else
        lc.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());

    lc.WriteTo.File(
        path: "logs/remal-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        formatter: new Serilog.Formatting.Compact.CompactJsonFormatter());
});

// ----- Application + Infrastructure -----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ----- SignalR + Dashboard notifier -----
builder.Services.AddSignalR();
builder.Services.AddScoped<IDashboardNotifier, DashboardNotifier>();

// وسيط تصغير الصور — بيجيب الأصل مرة واحدة ويكاشي النسخة المصغّرة على القرص
builder.Services.AddHttpClient("imgproxy");

// ----- API -----
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddFluentValidationAutoValidation().AddFluentValidationClientsideAdapters();

builder.Services.AddProblemDetails(o =>
{
    o.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});

// ----- Response compression (Brotli + Gzip) -----
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json", "application/problem+json", "text/plain",
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// ----- Swagger with JWT -----
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Remal API",
        Version = "v1",
        Description = "Remal perfume e-commerce backend — products, bundles, orders, accounting, audit, realtime",
        Contact = new OpenApiContact { Name = "Remal", Email = "hello@remal.eg" },
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer — أدخل: Bearer {your token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "default" });
    c.DocInclusionPredicate((name, api) => true);
});

// ----- CORS — explicit origins -----
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("Default", p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// ----- Rate limiting (.NET 9 built-in) — 100 req/min per IP -----
builder.Services.AddRateLimiter(o =>
{
    // ⚠️ الحد العام مقسّم حسب عنوان الـ IP — وده بيصطدم بـ CGNAT.
    // شبكات المحمول في مصر بتشارك عنوان IP عام واحد بين مئات المشتركين. جلسة تصفح
    // واحدة (٥ صفحات) بتستهلك ١٤ طلبًا خاضعًا للحد (‎/api‎ و‎/img‎ الاتنين controllers)،
    // فحد ١٠٠/دقيقة كان بيسمح بـ **٧ زوّار فقط** على نفس الـ IP قبل ما الباقي ياخد
    // 429 — يعني حملة إعلانية تجيب ٢٠ زائر من نفس شبكة المحمول في دقيقة واحدة كانت
    // هتكسر الموقع لأغلبهم (المنتجات ما تحمّلش، السلة والدفع يفشلوا).
    // ١٠٠٠ = ~٧٠ جلسة متزامنة على نفس الـ IP، ولسه بيوقف أي سكرابر جادّ.
    // الحدود الضيّقة اللي بتحمي فعلاً — تسجيل الدخول (١٠) والكتابة العامة (٥) —
    // سياسات مستقلة وما اتغيرتش.
    var globalLimit = builder.Configuration.GetValue<int>("RateLimiting:GlobalPermitLimit", 1000);
    var globalWindow = builder.Configuration.GetValue<int>("RateLimiting:GlobalWindowSeconds", 60);

    o.AddPolicy("global", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = globalLimit,
                Window = TimeSpan.FromSeconds(globalWindow),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // مقسّم حسب الـ IP (مش bucket واحد عام) — كل عميل له حدّه الخاص على مسارات المصادقة،
    // فلا يتسبب مستخدم/مهاجم واحد في حجب تسجيل دخول باقي المستخدمين.
    var authLimit = builder.Configuration.GetValue<int>("RateLimiting:AuthPermitLimit", 10);
    var authWindow = builder.Configuration.GetValue<int>("RateLimiting:AuthWindowSeconds", 60);
    o.AddPolicy("auth", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "auth:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = authLimit,
                Window = TimeSpan.FromSeconds(authWindow),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // M2 — حماية من السبام على المسارات العامة اللي بتكتب بيانات بدون تسجيل دخول
    // (التقييمات، تواصل معنا، الاشتراك في النشرة). حدّ ضيّق مقسّم حسب الـ IP فوق الحدّ العام،
    // فمهاجم واحد ميقدرش يغرق قاعدة البيانات بمراجعات/رسائل مزيّفة.
    var publicWriteLimit = builder.Configuration.GetValue<int>("RateLimiting:PublicWritePermitLimit", 5);
    var publicWriteWindow = builder.Configuration.GetValue<int>("RateLimiting:PublicWriteWindowSeconds", 60);
    o.AddPolicy("public-write", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "pubwrite:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = publicWriteLimit,
                Window = TimeSpan.FromSeconds(publicWriteWindow),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    o.RejectionStatusCode = 429;
});

// ----- Health Checks -----
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "sqlserver");

var app = builder.Build();

// ----- Pipeline (order matters) -----
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseResponseCompression();
app.UseSerilogRequestLogging(o =>
{
    o.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("RemoteIp", http.Connection.RemoteIpAddress?.ToString() ?? "");
        diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Remal API v1");
        o.DocumentTitle = "Remal API";
        o.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();

    // 301 canonical-host redirect: old runasp domain + www → https://remalfragrances.com
    // (single canonical origin = no duplicate content for SEO). /health is excluded so
    // hosting-panel monitors that ping the runasp host keep working.
    var canonicalHost = builder.Configuration["CanonicalHost"]; // e.g. "remalfragrances.com"
    if (!string.IsNullOrWhiteSpace(canonicalHost))
    {
        app.Use(async (ctx, next) =>
        {
            var host = ctx.Request.Host.Host;
            if (!host.Equals(canonicalHost, StringComparison.OrdinalIgnoreCase)
                && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                && !ctx.Request.Path.StartsWithSegments("/health"))
            {
                var target = $"https://{canonicalHost}{ctx.Request.Path}{ctx.Request.QueryString}";
                ctx.Response.Redirect(target, permanent: true);
                return;
            }
            await next();
        });
    }
}

// وسوم المشاركة والفهرسة لكل صفحة على حدة — **لازم قبل** UseDefaultFiles/UseStaticFiles،
// لأن الملف الثابت لو اتخدم الأول يبقى فات الأوان نعدّل الوسوم. كراولر واتساب/ماسنجر/
// فيسبوك ما بيشغّلش JavaScript، فتحديث الوسوم من الواجهة ما بيوصلهوش.
app.UseMiddleware<Remal.Api.Middleware.SocialMetaMiddleware>();

// Serve the storefront/dashboard HTML + sw.js + signalr.min.js from wwwroot.
// remal.html is the default file so https://remal-scent.runasp.net/ shows the storefront immediately.
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "remal.html" }
});
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // أصول /assets بتتطلب ببصمة محتوى في الـ query (?v=hash) — أي تعديل بيغيّر
        // الرابط، فآمن نكاشها سنة كاملة بـ immutable. ده اللي بيخلي الزائر الراجع
        // ما يحمّلش ٣٥٩ كيلوبايت جافاسكريبت و١٧٤ CSS من تاني.
        var path = ctx.Context.Request.Path.Value ?? "";
        if (path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }
});

app.UseRouting();
app.UseCors("Default");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("global");
app.MapHub<DashboardHub>(DashboardHub.Path);
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
});

// Google Analytics 4: يقرأ الـ Measurement ID من appsettings ديناميكياً —
// الواجهة تجلبه وتحقن سكريبت gtag، فيتغيّر من الإعدادات بدون تعديل كود.
// معرّف بيكسل Meta بيترجع من هنا كمان — قيمة عامة (بتبان في سورس الصفحة أصلاً)،
// وحطها هنا معناها إنها تتغيّر من الإعدادات بدون تعديل كود. التوكن السرّي **ما بيرجعش**
// من الـ endpoint ده إطلاقًا — ده بيتقرا في السيرفر بس.
app.MapGet("/api/config/analytics", (IConfiguration cfg) =>
    Results.Json(new
    {
        measurementId = cfg["GoogleAnalytics:MeasurementId"] ?? "",
        metaPixelId = cfg["Meta:PixelId"] ?? "",
        tiktokPixelId = cfg["TikTok:PixelId"] ?? "",
    }));

// /links — صفحة الروابط المستقلة (للـ QR code) — تُدار من لوحة التحكم
app.MapFallbackToFile("/links", "links.html");

// SPA clean URLs: any unmatched non-API path (/perfumes, /bundles, /checkout ...)
// falls back to the storefront so deep links and refreshes work without # in the URL.
app.MapFallbackToFile("remal.html");

// ----- Run migrations + seed -----
// ⚠️ الفشل هنا **ما ينفعش** يقتل التطبيق. قاعدة البيانات على سيرفر منفصل، وأي تأخير
// أو انقطاع لحظي وقت الإقلاع كان بيرمي استثناء يوقف التطبيق كله، فـ IIS يرجّع صفحة
// خطأ للموقع وللوحة التحكم مع بعض — والزائر ما بيرجعش غير لما محاولة تشغيل تانية
// تنجح (وده اللي كان شكله "الموقع وقف، عملت Refresh فاشتغل").
// دلوقتي: ٣ محاولات بتباعد متزايد، وبعدها التطبيق يكمل شغل على أي حال.
{
    const int maxAttempts = 3;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        using var scope = app.Services.CreateScope();
        try
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider);
            Log.Information("Database migrated and seeded successfully (attempt {Attempt})", attempt);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            var delay = TimeSpan.FromSeconds(attempt * 3);
            Log.Warning(ex, "Database initialization failed (attempt {Attempt}/{Max}) — retrying in {Delay}s",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            // آخر محاولة فشلت: نسجّلها بصوت عالي ونكمل. التطبيق هيفضل يخدم الصفحات
            // والملفات الثابتة، وأول ما قاعدة البيانات ترجع الطلبات هتشتغل من غير
            // ما حد يعيد تشغيل حاجة.
            Log.Error(ex, "Database initialization failed after {Max} attempts — "
                        + "the app will keep serving; database-backed endpoints will fail until it recovers", maxAttempts);
        }
    }
}

app.Run();

public partial class Program { }
