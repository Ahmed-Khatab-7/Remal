using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Identity;
using Remal.Infrastructure.Identity;
using Remal.Infrastructure.Persistence;
using Remal.Infrastructure.Persistence.Interceptors;
using Remal.Infrastructure.Persistence.Repositories;
using Remal.Infrastructure.Services;

namespace Remal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // -------- DbContext --------
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)
                          .EnableRetryOnFailure());
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // -------- Identity --------
        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
        {
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;
            o.Password.RequiredLength = 8;
            o.User.RequireUniqueEmail = true;
            o.SignIn.RequireConfirmedEmail = false;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // -------- JWT --------
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        var jwt = config.GetSection("Jwt").Get<JwtOptions>()!;

        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            o.RequireHttpsMetadata = false;
            o.SaveToken = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

        services.AddAuthorization(o =>
        {
            o.AddPolicy("Partner", p => p.RequireRole(Roles.Partner));
            o.AddPolicy("Admin", p => p.RequireRole(Roles.Admin));
        });

        // -------- Application services backed by Infrastructure --------
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // -------- Memory cache + Cache service --------
        services.AddMemoryCache(o => o.SizeLimit = 1024);
        services.AddSingleton<ICacheService, MemoryCacheService>();

        // -------- Email: إرسال فعلي عبر SMTP عند تعبئة SmtpHost و LogOnly=false --------
        // (سيرفر MonsterASP المحلي لا يحتاج SmtpUser، لذا نشترط الـ Host فقط.)
        services.Configure<EmailOptions>(config.GetSection("Email"));
        var emailCfg = config.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        if (!emailCfg.LogOnly && !string.IsNullOrWhiteSpace(emailCfg.SmtpHost))
            services.AddScoped<IEmailService, SmtpEmailService>();
        else
            services.AddScoped<IEmailService, LoggingEmailService>();

        services.AddHttpContextAccessor();

        // -------- Paymob --------
        services.Configure<PaymobOptions>(config.GetSection("Paymob"));
        services.AddHttpClient<IPaymobService, PaymobService>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        // -------- Web Push (VAPID) — auto-generate keys on first run, persist to disk --------
        services.Configure<VapidOptions>(opts =>
        {
            var fromConfig = config.GetSection("Vapid").Get<VapidOptions>();
            var keysPath = Path.Combine(AppContext.BaseDirectory, "vapid-keys.json");
            VapidOptions resolved;
            if (fromConfig != null && !string.IsNullOrWhiteSpace(fromConfig.PublicKey) && !string.IsNullOrWhiteSpace(fromConfig.PrivateKey))
            {
                resolved = fromConfig;
            }
            else if (File.Exists(keysPath))
            {
                resolved = System.Text.Json.JsonSerializer.Deserialize<VapidOptions>(File.ReadAllText(keysPath))!;
            }
            else
            {
                var gen = WebPush.VapidHelper.GenerateVapidKeys();
                resolved = new VapidOptions
                {
                    Subject = fromConfig?.Subject ?? "mailto:hello@remal.eg",
                    PublicKey = gen.PublicKey,
                    PrivateKey = gen.PrivateKey,
                };
                File.WriteAllText(keysPath, System.Text.Json.JsonSerializer.Serialize(resolved));
            }
            opts.Subject = resolved.Subject;
            opts.PublicKey = resolved.PublicKey;
            opts.PrivateKey = resolved.PrivateKey;
        });
        services.AddScoped<IPushService, PushService>();

        // -------- إشعارات تليجرام (الطبقة الأضمن على الآيفون) --------
        // التوكن ومعرّف المحادثة بيتقروا من AppSettings وقت الإرسال، مش من الإعدادات
        // — فتغييرهم من اللوحة بيسري فورًا بدون إعادة نشر.
        services.AddHttpClient("telegram");
        services.AddScoped<ITelegramNotifier, TelegramNotifier>();

        // -------- Meta Conversions API (تتبع من السيرفر) --------
        services.AddHttpClient<IMetaConversionsApi, MetaConversionsApi>(c =>
        {
            // مهلة قصيرة عن قصد: التتبع ما ينفعش يأخّر تأكيد طلب حقيقي
            c.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    public static async Task UseInfrastructureAsync(this IServiceProvider sp, bool runSeed = true)
    {
        if (runSeed) await DbSeeder.SeedAsync(sp);
    }
}
