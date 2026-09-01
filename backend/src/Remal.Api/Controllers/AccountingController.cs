using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remal.Application.Common.Models;
using Remal.Application.Features.Accounting;
using Remal.Application.Features.Accounting.Dtos;
using Remal.Domain.Enums;

namespace Remal.Api.Controllers;

[ApiController]
[Route("api/accounting")]
[Authorize(Policy = "Partner")]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _svc;
    public AccountingController(IAccountingService svc) => _svc = svc;

    /// <summary>Full P&L summary, partner balances, and suggested settlements.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<AccountingSummaryDto>>> Summary(CancellationToken ct)
        => Ok(ApiResponse<AccountingSummaryDto>.Ok(await _svc.GetSummaryAsync(ct)));

    [HttpGet("expenses")]
    public async Task<ActionResult<ApiResponse<PagedResult<ExpenseDto>>>> Expenses(
        int page = 1, int pageSize = 50, string? search = null, string? userId = null, ExpenseCategory? category = null, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<ExpenseDto>>.Ok(await _svc.GetExpensesAsync(page, pageSize, search, userId, category, ct)));

    [HttpPost("expenses")]
    public async Task<ActionResult<ApiResponse<ExpenseDto>>> CreateExpense(ExpenseWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<ExpenseDto>.Ok(await _svc.CreateExpenseAsync(dto, ct), "تم تسجيل المصروف"));

    [HttpPut("expenses/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExpenseDto>>> UpdateExpense(Guid id, ExpenseWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<ExpenseDto>.Ok(await _svc.UpdateExpenseAsync(id, dto, ct), "تم الحفظ"));

    [HttpDelete("expenses/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteExpense(Guid id, CancellationToken ct)
    {
        await _svc.DeleteExpenseAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }

    [HttpGet("settlements")]
    public async Task<ActionResult<ApiResponse<List<SettlementDto>>>> Settlements(CancellationToken ct)
        => Ok(ApiResponse<List<SettlementDto>>.Ok(await _svc.GetSettlementsAsync(ct)));

    [HttpPost("settlements")]
    public async Task<ActionResult<ApiResponse<SettlementDto>>> CreateSettlement(SettlementWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<SettlementDto>.Ok(await _svc.CreateSettlementAsync(dto, ct), "تم تسجيل التسوية"));

    [HttpDelete("settlements/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteSettlement(Guid id, CancellationToken ct)
    {
        await _svc.DeleteSettlementAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }

    /// <summary>Partner drawings (money a partner withdrew from the project for personal use).</summary>
    [HttpGet("withdrawals")]
    public async Task<ActionResult<ApiResponse<List<PartnerWithdrawalDto>>>> Withdrawals(CancellationToken ct)
        => Ok(ApiResponse<List<PartnerWithdrawalDto>>.Ok(await _svc.GetWithdrawalsAsync(ct)));

    [HttpPost("withdrawals")]
    public async Task<ActionResult<ApiResponse<PartnerWithdrawalDto>>> CreateWithdrawal(PartnerWithdrawalWriteDto dto, CancellationToken ct)
        => Ok(ApiResponse<PartnerWithdrawalDto>.Ok(await _svc.CreateWithdrawalAsync(dto, ct), "تم تسجيل السحب"));

    [HttpDelete("withdrawals/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteWithdrawal(Guid id, CancellationToken ct)
    {
        await _svc.DeleteWithdrawalAsync(id, ct);
        return Ok(ApiResponse.Ok("تم الحذف"));
    }

    public record ResetAccountingDto(string? ConfirmationText);

    /// <summary>
    /// DANGER: hard-deletes ALL expenses, settlements and partner-withdrawals — عملية غير قابلة للتراجع.
    /// تتطلب تأكيدًا نصيًا: لازم يبعت ConfirmationText = "RESET" بالظبط، وإلا نرفض بـ 400.
    /// العملية تُسجَّل في AuditLog (مين ومتى) داخل ResetAllAsync.
    /// </summary>
    [HttpPost("reset")]
    public async Task<ActionResult<ApiResponse<object>>> ResetAccounting([FromBody] ResetAccountingDto dto, CancellationToken ct)
    {
        if (dto?.ConfirmationText?.Trim() != "RESET")
            return BadRequest(ApiResponse<object>.Fail("لتأكيد التصفير النهائي لازم تكتب RESET بالحروف الكبيرة بالظبط."));

        var (exp, stl, wdr) = await _svc.ResetAllAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { expenses = exp, settlements = stl, withdrawals = wdr },
            $"تم تصفير الحسابات: {exp} مصروف، {stl} تسوية، {wdr} سحب"));
    }
}
