using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Remal.Application.Common.Interfaces;
using Remal.Application.Common.Models;
using Remal.Application.Features.Audit;
using Remal.Application.Features.Audit.Dtos;
using Remal.Application.Features.Reports;
using Remal.Application.Features.Reports.Dtos;
using Remal.Domain.Identity;

namespace Remal.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "Partner")]
public class AuditController : ControllerBase
{
    private readonly IAuditQueryService _svc;
    public AuditController(IAuditQueryService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> List([FromQuery] AuditFilterDto filter, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(await _svc.GetAsync(filter, ct)));
}

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "Partner")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _svc;
    public ReportsController(IReportService svc) => _svc = svc;

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<OverviewKpiDto>>> Overview(CancellationToken ct)
        => Ok(ApiResponse<OverviewKpiDto>.Ok(await _svc.GetOverviewAsync(ct)));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ReportsResponseDto>>> Reports([FromQuery] int days = 30, CancellationToken ct = default)
        => Ok(ApiResponse<ReportsResponseDto>.Ok(await _svc.GetReportsAsync(days, ct)));
}

[ApiController]
[Route("api/team")]
[Authorize(Policy = "Partner")]
public class TeamController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userMgr;
    public TeamController(UserManager<ApplicationUser> userMgr) => _userMgr = userMgr;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<object>>>> List(CancellationToken ct)
    {
        var partners = await _userMgr.GetUsersInRoleAsync(Roles.Partner);
        // The brand-admin (admin@remal.eg) is NOT counted as a 4th partner — exclude.
        var data = partners
            .Where(p => !string.Equals(p.Email, "admin@remal.eg", StringComparison.OrdinalIgnoreCase))
            .Select(p => new
            {
                p.Id, p.FullName, p.Email, p.AvatarInitials, p.AvatarUrl,
                p.IsActive, p.LastLoginAt, p.CreatedAt,
            }).Cast<object>().ToList();
        return Ok(ApiResponse<List<object>>.Ok(data));
    }
}

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymobService _paymob;
    private readonly IApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymobService paymob, IApplicationDbContext db, IAuditService audit, ILogger<PaymentsController> logger)
    {
        _paymob = paymob; _db = db; _audit = audit; _logger = logger;
    }

    /// <summary>Create a Paymob payment session for an existing order. Returns iframe URL.</summary>
    [HttpPost("paymob/session/{orderId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateSession(Guid orderId, CancellationToken ct)
    {
        var order = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_db.Orders, o => o.Id == orderId, ct);
        if (order == null) return NotFound(ApiResponse.Fail("الطلب غير موجود"));

        var session = await _paymob.CreatePaymentSessionAsync(
            order.Total, order.Code, order.CustomerName, order.CustomerPhone, order.CustomerEmail ?? "", ct);

        order.PaymentReference = session.PaymobOrderId;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { session.IframeUrl, session.PaymobOrderId }));
    }

    /// <summary>Paymob webhook (transaction processed callback). Updates order payment status.</summary>
    [HttpPost("paymob/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] System.Text.Json.JsonElement payload, [FromQuery] string hmac, CancellationToken ct)
    {
        try
        {
            var dict = FlattenPaymob(payload);
            if (!_paymob.VerifyHmac(hmac, dict))
            {
                _logger.LogWarning("Invalid Paymob HMAC");
                return BadRequest();
            }

            var orderCode = payload.GetProperty("obj").GetProperty("order").GetProperty("merchant_order_id").GetString();
            var success = payload.GetProperty("obj").GetProperty("success").GetBoolean();
            var transactionId = payload.GetProperty("obj").GetProperty("id").GetRawText();

            var order = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(_db.Orders, o => o.Code == orderCode, ct);
            if (order == null) return NotFound();

            order.PaymentStatus = success ? Domain.Enums.PaymentStatus.Paid : Domain.Enums.PaymentStatus.Failed;
            order.PaymentReference = transactionId;
            if (success && order.Status == Domain.Enums.OrderStatus.Pending)
                order.Status = Domain.Enums.OrderStatus.Preparing;

            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(Domain.Enums.AuditCategory.Payment, success ? "PAYMENT_SUCCESS" : "PAYMENT_FAILED",
                $"الطلب {order.Code}: {(success ? "تم الدفع" : "فشل الدفع")} (#{transactionId})",
                entityName: "Order", entityId: order.Id.ToString(), ct: ct);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paymob webhook handler failed");
            return StatusCode(500);
        }
    }

    private static Dictionary<string, string> FlattenPaymob(System.Text.Json.JsonElement payload)
    {
        var d = new Dictionary<string, string>();
        if (!payload.TryGetProperty("obj", out var obj)) return d;
        void Add(string key, System.Text.Json.JsonElement el)
            => d[key] = el.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => el.GetString() ?? "",
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Number => el.GetRawText(),
                _ => el.GetRawText(),
            };
        foreach (var p in obj.EnumerateObject())
        {
            if (p.Name == "order" && p.Value.TryGetProperty("id", out var oid)) Add("order.id", oid);
            else if (p.Name == "source_data" && p.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (p.Value.TryGetProperty("pan", out var pan)) Add("source_data.pan", pan);
                if (p.Value.TryGetProperty("sub_type", out var st)) Add("source_data.sub_type", st);
                if (p.Value.TryGetProperty("type", out var t)) Add("source_data.type", t);
            }
            else Add(p.Name, p.Value);
        }
        return d;
    }
}
