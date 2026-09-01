using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;

namespace Remal.Infrastructure.Services;

public class PaymobOptions
{
    public string ApiKey { get; set; } = null!;
    public string IframeId { get; set; } = null!;
    public string IntegrationId { get; set; } = null!;
    public string HmacSecret { get; set; } = null!;
    public string BaseUrl { get; set; } = "https://accept.paymob.com/api";
}

public class PaymobService : IPaymobService
{
    private readonly HttpClient _http;
    private readonly PaymobOptions _opts;
    private readonly ILogger<PaymobService> _logger;

    public PaymobService(HttpClient http, IOptions<PaymobOptions> opts, ILogger<PaymobService> logger)
    {
        _http = http; _opts = opts.Value; _logger = logger;
    }

    public async Task<PaymobPaymentSession> CreatePaymentSessionAsync(
        decimal amount, string orderCode, string customerName, string customerPhone, string customerEmail,
        CancellationToken ct = default)
    {
        // Step 1: Auth — get token
        var authToken = await GetAuthTokenAsync(ct);

        // Step 2: Register order
        var paymobOrderId = await RegisterOrderAsync(authToken, amount, orderCode, ct);

        // Step 3: Get payment key
        var paymentKey = await GetPaymentKeyAsync(authToken, paymobOrderId, amount, customerName, customerPhone, customerEmail, ct);

        return new PaymobPaymentSession
        {
            PaymentToken = paymentKey,
            PaymobOrderId = paymobOrderId,
            IframeUrl = $"https://accept.paymob.com/api/acceptance/iframes/{_opts.IframeId}?payment_token={paymentKey}",
        };
    }

    public bool VerifyHmac(string hmac, IDictionary<string, string> payload)
    {
        // Standard Paymob HMAC fields ordered:
        // amount_cents, created_at, currency, error_occured, has_parent_transaction, id, integration_id,
        // is_3d_secure, is_auth, is_capture, is_refunded, is_standalone_payment, is_voided,
        // order.id, owner, pending, source_data.pan, source_data.sub_type, source_data.type, success
        var keys = new[]
        {
            "amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction",
            "id", "integration_id", "is_3d_secure", "is_auth", "is_capture", "is_refunded",
            "is_standalone_payment", "is_voided", "order.id", "owner", "pending",
            "source_data.pan", "source_data.sub_type", "source_data.type", "success",
        };

        var concat = new StringBuilder();
        foreach (var k in keys) concat.Append(payload.TryGetValue(k, out var v) ? v : string.Empty);

        using var hmacSha512 = new HMACSHA512(Encoding.UTF8.GetBytes(_opts.HmacSecret));
        var hash = hmacSha512.ComputeHash(Encoding.UTF8.GetBytes(concat.ToString()));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(computed, hmac, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetAuthTokenAsync(CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync($"{_opts.BaseUrl}/auth/tokens", new { api_key = _opts.ApiKey }, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("token").GetString()!;
    }

    private async Task<string> RegisterOrderAsync(string authToken, decimal amount, string orderCode, CancellationToken ct)
    {
        var body = new
        {
            auth_token = authToken,
            delivery_needed = false,
            amount_cents = (int)(amount * 100),
            currency = "EGP",
            merchant_order_id = orderCode,
            items = Array.Empty<object>(),
        };
        var resp = await _http.PostAsJsonAsync($"{_opts.BaseUrl}/ecommerce/orders", body, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("id").GetRawText();
    }

    private async Task<string> GetPaymentKeyAsync(string authToken, string paymobOrderId, decimal amount,
        string customerName, string customerPhone, string customerEmail, CancellationToken ct)
    {
        var firstName = customerName.Split(' ').FirstOrDefault() ?? "Customer";
        var lastName = customerName.Split(' ').Length > 1 ? customerName.Split(' ').Last() : "Remal";

        var body = new
        {
            auth_token = authToken,
            amount_cents = (int)(amount * 100),
            expiration = 3600,
            order_id = paymobOrderId,
            billing_data = new
            {
                first_name = firstName, last_name = lastName,
                email = string.IsNullOrWhiteSpace(customerEmail) ? "customer@remal.eg" : customerEmail,
                phone_number = customerPhone,
                country = "EG", city = "Cairo", street = "NA",
                building = "NA", floor = "NA", apartment = "NA",
                shipping_method = "NA", postal_code = "NA", state = "NA",
            },
            currency = "EGP",
            integration_id = int.Parse(_opts.IntegrationId),
        };
        var resp = await _http.PostAsJsonAsync($"{_opts.BaseUrl}/acceptance/payment_keys", body, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("Paymob payment key failed: {Error}", err);
            throw new BadRequestException("فشل بدء عملية الدفع، حاول تاني");
        }
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.GetProperty("token").GetString()!;
    }
}
