# Add PayOS as Alternative Payment Gateway

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add PayOS as a second payment gateway alongside existing VNPay. Users choose gateway at checkout time. Both gateways write to the same `Payment` table with the same status flow.

**Architecture:** Introduce a `IPaymentGateway` abstraction. Both `VnPayGateway` and `PayOsGateway` implement it. The `PaymentService` picks the right gateway based on a new `Gateway` column on the Payment entity and a new `gateway` field in `CreatePaymentLinkRequestDto`. Webhook controllers remain separate (`vnpay-ipn` and `payos-webhook`) but both delegate to `PaymentService` which routes to the correct gateway for verification.

**Tech Stack:** Official `payOS` NuGet package (`PayOS`), .NET 8, existing VNPay service preserved.

---

## File Map

### Files to CREATE
| File | Purpose |
|------|---------|
| `AIStudyHub.Business/Options/PayOsOptions.cs` | PayOS config (ClientId, ApiKey, ChecksumKey, CancelUrl, WebhookUrl) |
| `AIStudyHub.Business/Interfaces/Services/IPaymentGateway.cs` | Shared gateway abstraction |
| `AIStudyHub.Business/Services/PayOsGateway.cs` | PayOS implementation of `IPaymentGateway` |
| `AIStudyHub.Data/Enums/PaymentGateway.cs` | Enum: `VNPay = 1, PayOS = 2` |

### Files to MODIFY
| File | Changes |
|------|---------|
| `AIStudyHub.Data/Entities/Payment.cs` | Add `Gateway` enum column, add `OrderCode` (long) for PayOS lookup |
| `AIStudyHub.Business/Interfaces/Services/IPaymentService.cs` | Extend `CreatePaymentLinkRequestDto` with gateway choice; new `ProcessPayOsWebhookAsync` |
| `AIStudyHub.Business/Services/ModuleServices.cs` | Extract `PaymentService`; route by gateway; add `PayOsGateway` field |
| `AIStudyHub.Business/Services/VnPayService.cs` | Refactor existing class to implement `IPaymentGateway` (keep current methods, just wrap them) |
| `AIStudyHub.Business/Services/BusinessServiceExtensions.cs` | Register `PayOsOptions`, `IPayOsGateway`, `IPaymentGateway` resolver |
| `AIStudyHub.API/Controllers/PaymentController.cs` | New `payos-webhook` endpoint; pass gateway to `create-checkout-url` |
| `AIStudyHub.API/appsettings.json` | Add `PayOs` section |
| `AIStudyHub.API/appsettings.Development.json` | Add PayOS dev creds (sandbox keys) |

### Files to DELETE
None — all VNPay code preserved for rollback.

---

## Key Design Decisions

### 1. Single `Payment` table, add `Gateway` column
Rather than create a new table for PayOS payments, add a `Gateway` enum to the existing `Payment` entity. This keeps reporting/refund logic unified and avoids data duplication.

### 2. Shared `IPaymentGateway` interface
Both gateways must implement:
```csharp
public interface IPaymentGateway
{
    PaymentGateway Gateway { get; }
    string CreatePaymentLink(Payment payment, HttpContext context);
    WebhookVerifyResult VerifyWebhook(IQueryCollection form);
    bool TryParseOrderRef(IQueryCollection form, out Guid paymentId, out long orderCode);
}
```

### 3. Webhook controllers stay separate, share one service
VNPay: `GET /api/Payment/vnpay-ipn` (existing, keep)
PayOS: `POST /api/Payment/payos-webhook` (new)

Both call `PaymentService.ProcessWebhookAsync(gateway, query, form)` which routes internally.

### 4. `OrderCode` column for PayOS lookup
PayOS uses a `long orderCode` to identify payments. We store this on Payment when the gateway is PayOS. For VNPay, `vnp_TxnRef` is still the GUID.

---

## Task 1: Add PayOS NuGet package

**Files:**
- Modify: `AIStudyHub.Business/AIStudyHub.Business.csproj`

- [ ] **Step 1: Install package**

```bash
cd D:/GitHub/BE-SWP
dotnet add AIStudyHub.Business/AIStudyHub.Business.csproj package payOS --version 2.1.0
```

Verify: csproj has `<PackageReference Include="payOS" Version="2.1.0" />`

---

## Task 2: Add `PaymentGateway` enum

**Files:**
- Create: `AIStudyHub.Data/Enums/PaymentGateway.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace AIStudyHub.Data.Enums;

public enum PaymentGateway
{
    VNPay = 1,
    PayOS = 2
}
```

---

## Task 3: Extend `Payment` entity

**Files:**
- Modify: `AIStudyHub.Data/Entities/Payment.cs`

- [ ] **Step 1: Add Gateway + OrderCode**

```csharp
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Data.Entities;

public sealed class Payment : BaseEntity
{
    public Guid UserId { get; set; }
    public string PaymentInfo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentStatus? Status { get; set; } = PaymentStatus.Pending;
    public Guid? TierId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public PaymentGateway Gateway { get; set; } = PaymentGateway.VNPay;
    public long? OrderCode { get; set; }

    public User User { get; set; } = null!;
    public TierMembership? TierMembership { get; set; }
}
```

- [ ] **Step 2: Run EF migration**

```bash
cd D:/GitHub/BE-SWP
dotnet ef migrations add AddPayOsGateway --project AIStudyHub.Data --startup-project AIStudyHub.API
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

---

## Task 4: Create `PayOsOptions`

**Files:**
- Create: `AIStudyHub.Business/Options/PayOsOptions.cs`

- [ ] **Step 1: Create options class**

```csharp
namespace AIStudyHub.Business.Options;

public sealed class PayOsOptions
{
    public const string SectionName = "PayOs";
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = "http://localhost:3000/pricing";
    public string ReturnUrl { get; set; } = "http://localhost:3000/pricing/payos-return";
    public string WebhookUrl { get; set; } = "http://localhost:5171/api/payment/payos-webhook";
}
```

---

## Task 5: Create `IPaymentGateway` abstraction

**Files:**
- Create: `AIStudyHub.Business/Interfaces/Services/IPaymentGateway.cs`

- [ ] **Step 1: Create the interface**

```csharp
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace AIStudyHub.Business.Interfaces.Services;

public sealed class WebhookVerifyResult
{
    public bool IsValid { get; init; }
    public string ResponseCode { get; init; } = string.Empty; // "00" for success
    public string TransactionId { get; init; } = string.Empty;
    public Guid? PaymentId { get; init; }
    public long? OrderCode { get; init; }
}

public interface IPaymentGateway
{
    PaymentGateway Gateway { get; }
    string CreatePaymentLink(Payment payment, HttpContext context);
    WebhookVerifyResult VerifyWebhook(IQueryCollection form);
}
```

---

## Task 6: Refactor `VnPayService` to implement `IPaymentGateway`

**Files:**
- Modify: `AIStudyHub.Business/Services/VnPayService.cs`

- [ ] **Step 1: Add interface implementation**

```csharp
public sealed class VnPayService : IPaymentGateway
{
    private readonly VnPayOptions _options;
    public PaymentGateway Gateway => PaymentGateway.VNPay;

    public VnPayService(IOptions<VnPayOptions> options)
    {
        _options = options.Value;
    }

    public string CreatePaymentLink(Payment payment, HttpContext context)
    {
        // Existing logic, but use payment.Id directly (already a Guid)
        var vnpayData = new SortedList<string, string>(new VnPayCompare())
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", _options.TmnCode },
            { "vnp_Amount", ((long)(payment.Amount * 100)).ToString() },
            { "vnp_CreateDate", DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss") },
            { "vnp_CurrCode", "VND" },
            { "vnp_IpAddr", GetIpAddress(context) },
            { "vnp_Locale", "vn" },
            { "vnp_OrderInfo", payment.PaymentInfo },
            { "vnp_OrderType", "other" },
            { "vnp_ReturnUrl", _options.ReturnUrl },
            { "vnp_TxnRef", payment.Id.ToString() },
            { "vnp_IpnUrl", _options.IpnUrl }
        };

        var queryString = BuildQueryString(vnpayData);
        var vnpSecureHash = HmacSHA512(_options.HashSecret, queryString);

        return $"{_options.BaseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    public WebhookVerifyResult VerifyWebhook(IQueryCollection form)
    {
        var vnp_SecureHash = form["vnp_SecureHash"].ToString();
        var vnpayData = new SortedList<string, string>(new VnPayCompare());
        foreach (var (key, value) in form)
        {
            if (string.IsNullOrEmpty(key) || key.StartsWith("vnp_SecureHash")) continue;
            vnpayData.Add(key, value.ToString());
        }

        var signData = BuildQueryString(vnpayData);
        var checkSum = HmacSHA512(_options.HashSecret, signData);
        var isValid = checkSum.Equals(vnp_SecureHash, StringComparison.InvariantCultureIgnoreCase);

        return new WebhookVerifyResult
        {
            IsValid = isValid,
            ResponseCode = form["vnp_ResponseCode"].ToString(),
            TransactionId = form["vnp_TransactionNo"].ToString(),
            PaymentId = Guid.TryParse(form["vnp_TxnRef"].ToString(), out var id) ? id : null
        };
    }

    // Keep existing private helpers: GetIpAddress, BuildQueryString, HmacSHA512
    // Keep VnPayCompare class
}
```

**Keep all existing private methods (GetIpAddress, BuildQueryString, HmacSHA512, VnPayCompare).**

---

## Task 7: Create `PayOsGateway`

**Files:**
- Create: `AIStudyHub.Business/Services/PayOsGateway.cs`

- [ ] **Step 1: Create PayOsGateway.cs**

```csharp
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models;

namespace AIStudyHub.Business.Services;

public sealed class PayOsGateway : IPaymentGateway
{
    private readonly PayOSClient _client;
    private readonly PayOsOptions _options;

    public PaymentGateway Gateway => PaymentGateway.PayOS;

    public PayOsGateway(IOptions<PayOsOptions> options)
    {
        _options = options.Value;
        _client = new PayOSClient(_options.ClientId, _options.ApiKey, _options.ChecksumKey);
    }

    public string CreatePaymentLink(Payment payment, HttpContext context)
    {
        // PayOS requires long orderCode. Generate from Guid hash or sequential counter.
        var orderCode = payment.OrderCode ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        payment.OrderCode = orderCode; // store for later lookup

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)payment.Amount, // PayOS uses int VND
            Description = payment.PaymentInfo,
            CancelUrl = _options.CancelUrl,
            ReturnUrl = $"{_options.ReturnUrl}?orderCode={orderCode}"
        };

        var result = _client.PaymentRequests.CreateAsync(request).GetAwaiter().GetResult();
        return result.CheckoutUrl;
    }

    public WebhookVerifyResult VerifyWebhook(IQueryCollection form)
    {
        // PayOS sends JSON body, not query string. Controller must parse body
        // and call a different overload. This default impl returns invalid.
        return new WebhookVerifyResult { IsValid = false };
    }

    public WebhookVerifyResult VerifyWebhookPayload(PayOsWebhookPayload payload)
    {
        var webhook = new Webhook
        {
            Code = payload.Code,
            Description = payload.Description,
            Success = payload.Success,
            Signature = payload.Signature,
            Data = System.Text.Json.JsonSerializer.Deserialize<WebhookData>(payload.Data,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
        };

        var isValid = _client.Webhooks.VerifyAsync(webhook).GetAwaiter().GetResult();

        if (!isValid)
            return new WebhookVerifyResult { IsValid = false };

        return new WebhookVerifyResult
        {
            IsValid = true,
            ResponseCode = webhook.Data.Code,
            TransactionId = webhook.Data.Reference,
            OrderCode = webhook.Data.OrderCode
        };
    }
}

public sealed record PayOsWebhookPayload(
    string Code,
    string Description,
    bool Success,
    string Data,
    string Signature);
```

---

## Task 8: Update `IPaymentService`

**Files:**
- Modify: `AIStudyHub.Business/Interfaces/Services/IPaymentService.cs`

- [ ] **Step 1: Add gateway choice to request**

```csharp
using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IPaymentService : ICrudService<PaymentResponseDto, CreatePaymentRequestDto, UpdatePaymentRequestDto>
{
    Task<PaymentLinkResponseDto> CreatePaymentUrlAsync(
        CreatePaymentLinkRequestDto request,
        PaymentGateway gateway,
        HttpContext context,
        CancellationToken cancellationToken = default);

    Task<bool> ProcessVnPayWebhookAsync(IQueryCollection query, CancellationToken cancellationToken = default);

    Task<bool> ProcessPayOsWebhookAsync(PayOsWebhookPayload payload, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentResponseDto>> GetUserPaymentsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RefundPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<VnpayReturnResult> HandleVnpayReturnAsync(IQueryCollection query, CancellationToken cancellationToken = default);

    Task<PayOsReturnResult> HandlePayOsReturnAsync(long orderCode, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Update DTOs**

In `AIStudyHub.Business/DTOs/Payments/PaymentDtos.cs`, add:

```csharp
public sealed record CreatePaymentLinkRequestDto(Guid TierId, decimal? Amount = null);

public sealed record PayOsReturnResult(bool IsSuccess, string Message, string? Status);
```

Update `PaymentResponseDto` to include gateway:
```csharp
public sealed record PaymentResponseDto(
    Guid Id,
    Guid UserId,
    string PaymentInfo,
    DateTime PaymentDate,
    PaymentStatus? Status,
    Guid? TierId,
    decimal Amount,
    string TransactionId,
    PaymentGateway Gateway,
    long? OrderCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

---

## Task 9: Refactor `PaymentService` to dispatch by gateway

**Files:**
- Modify: `AIStudyHub.Business/Services/ModuleServices.cs` (extract `PaymentService`)
- Create: `AIStudyHub.Business/Services/PaymentService.cs`

- [ ] **Step 1: Create new `PaymentService.cs`**

```csharp
using AIStudyHub.Business.DTOs.Payments;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Enums;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly VnPayService _vnPayGateway;
    private readonly PayOsGateway _payOsGateway;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        VnPayService vnPayGateway,
        PayOsGateway payOsGateway)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _vnPayGateway = vnPayGateway;
        _payOsGateway = payOsGateway;
    }

    public async Task<PaymentLinkResponseDto> CreatePaymentUrlAsync(
        CreatePaymentLinkRequestDto request,
        PaymentGateway gateway,
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var userIdString = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            throw new UnauthorizedAccessException("User not authenticated.");

        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(request.TierId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tier with ID {request.TierId} not found.");

        var amount = request.Amount ?? 100000m;

        var payment = new Data.Entities.Payment
        {
            UserId = userId,
            TierId = request.TierId,
            Amount = amount,
            Status = PaymentStatus.Pending,
            PaymentInfo = $"Upgrade to {tier.TierName} tier",
            PaymentDate = DateTime.UtcNow,
            Gateway = gateway
        };

        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var url = gateway switch
        {
            PaymentGateway.VNPay => _vnPayGateway.CreatePaymentLink(payment, context),
            PaymentGateway.PayOS => _payOsGateway.CreatePaymentLink(payment, context),
            _ => throw new NotSupportedException($"Gateway {gateway} not supported.")
        };

        return new PaymentLinkResponseDto(url);
    }

    public async Task<bool> ProcessVnPayWebhookAsync(IQueryCollection query, CancellationToken cancellationToken = default)
    {
        var result = _vnPayGateway.VerifyWebhook(query);
        if (!result.IsValid || result.PaymentId is null) return false;

        var payment = await _unitOfWork.Payments.GetByIdAsync(result.PaymentId.Value, cancellationToken);
        if (payment is null) return false;

        return await ApplyPaymentResultAsync(payment, result.TransactionId, result.ResponseCode, cancellationToken);
    }

    public async Task<bool> ProcessPayOsWebhookAsync(PayOsWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        var result = _payOsGateway.VerifyWebhookPayload(payload);
        if (!result.IsValid || result.OrderCode is null) return false;

        var payment = await _unitOfWork.Payments.Query()
            .FirstOrDefaultAsync(p => p.OrderCode == result.OrderCode.Value, cancellationToken);
        if (payment is null) return false;

        return await ApplyPaymentResultAsync(payment, result.TransactionId, result.ResponseCode, cancellationToken);
    }

    private async Task<bool> ApplyPaymentResultAsync(
        Data.Entities.Payment payment,
        string transactionId,
        string responseCode,
        CancellationToken cancellationToken)
    {
        payment.TransactionId = transactionId;

        if (responseCode == "00")
        {
            payment.Status = PaymentStatus.Completed;
            if (payment.TierId.HasValue)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(payment.UserId, cancellationToken);
                if (user is not null)
                {
                    var tier = await _unitOfWork.TierMemberships.GetByIdAsync(payment.TierId.Value, cancellationToken);
                    user.TierId = payment.TierId.Value;
                    user.TierExpireAt = tier is not null && !tier.TierName.Equals("Free", StringComparison.OrdinalIgnoreCase)
                        ? DateTime.UtcNow.AddDays(30)
                        : null;
                    _unitOfWork.Users.Update(user);
                }
            }
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
        }

        _unitOfWork.Payments.Update(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Keep existing methods: GetAllAsync, GetByIdAsync, GetUserPaymentsAsync, RefundPaymentAsync, HandleVnpayReturnAsync
    // (Copy unchanged from ModuleServices.cs)

    public async Task<IReadOnlyList<PaymentResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments.Query()
            .Include(p => p.User).Include(p => p.TierMembership)
            .AsNoTracking().ToListAsync(cancellationToken);
        return payments.Select(_mapper.Map<PaymentResponseDto>).ToList();
    }

    public async Task<PaymentResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _unitOfWork.Payments.Query()
            .Include(p => p.User).Include(p => p.TierMembership)
            .AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return p is null ? null : _mapper.Map<PaymentResponseDto>(p);
    }

    public async Task<PaymentResponseDto> CreateAsync(CreatePaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        var p = _mapper.Map<Data.Entities.Payment>(request);
        await _unitOfWork.Payments.AddAsync(p, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<PaymentResponseDto>(p);
    }

    public async Task<PaymentResponseDto> UpdateAsync(Guid id, UpdatePaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        var p = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment {id} not found.");
        _mapper.Map(request, p);
        _unitOfWork.Payments.Update(p);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<PaymentResponseDto>(p);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment {id} not found.");
        _unitOfWork.Payments.Remove(p);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentResponseDto>> GetUserPaymentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var payments = await _unitOfWork.Payments.Query()
            .Include(p => p.User).Include(p => p.TierMembership)
            .Where(p => p.UserId == userId).AsNoTracking()
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);
        return payments.Select(_mapper.Map<PaymentResponseDto>).ToList();
    }

    public async Task RefundPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var p = await _unitOfWork.Payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Payment {paymentId} not found.");
        if (p.Status == PaymentStatus.Refunded)
            throw new InvalidOperationException("Already refunded.");
        if (p.Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Only completed payments can be refunded.");
        p.Status = PaymentStatus.Refunded;
        _unitOfWork.Payments.Update(p);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VnpayReturnResult> HandleVnpayReturnAsync(IQueryCollection query, CancellationToken cancellationToken = default)
    {
        // Keep existing logic, but call _vnPayGateway.VerifyWebhook instead
        var result = _vnPayGateway.VerifyWebhook(query);
        if (!result.IsValid) return new VnpayReturnResult { IsValidSignature = false };

        if (result.PaymentId is null || !Guid.TryParse(result.PaymentId.ToString(), out var pid))
            return new VnpayReturnResult { Message = "Invalid payment ID" };

        var payment = await _unitOfWork.Payments.GetByIdAsync(pid, cancellationToken);
        if (payment is null) return new VnpayReturnResult { Message = "Payment not found" };

        if (payment.Status != PaymentStatus.Pending)
        {
            return new VnpayReturnResult
            {
                IsSuccess = payment.Status == PaymentStatus.Completed,
                Message = "Payment already processed",
                Status = payment.Status.ToString()
            };
        }

        payment.TransactionId = result.TransactionId;
        if (result.ResponseCode == "00")
        {
            payment.Status = PaymentStatus.Completed;
            var user = await _unitOfWork.Users.GetByIdAsync(payment.UserId, cancellationToken);
            if (user is not null && payment.TierId.HasValue)
            {
                var tier = await _unitOfWork.TierMemberships.GetByIdAsync(payment.TierId.Value, cancellationToken);
                user.TierId = payment.TierId.Value;
                user.TierExpireAt = tier is not null && !tier.TierName.Equals("Free", StringComparison.OrdinalIgnoreCase)
                    ? DateTime.UtcNow.AddDays(30) : null;
                _unitOfWork.Users.Update(user);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new VnpayReturnResult { IsSuccess = true, Message = "Thanh toán thành công", Status = PaymentStatus.Completed.ToString() };
        }

        payment.Status = PaymentStatus.Failed;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new VnpayReturnResult { IsSuccess = false, Message = "Thanh toán thất bại", Status = PaymentStatus.Failed.ToString() };
    }

    public async Task<PayOsReturnResult> HandlePayOsReturnAsync(long orderCode, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.Query()
            .FirstOrDefaultAsync(p => p.OrderCode == orderCode, cancellationToken);
        if (payment is null) return new PayOsReturnResult(false, "Payment not found", null);

        if (payment.Status == PaymentStatus.Completed)
            return new PayOsReturnResult(true, "Thanh toán thành công", payment.Status.ToString());

        if (payment.Status == PaymentStatus.Failed)
            return new PayOsReturnResult(false, "Thanh toán thất bại", payment.Status.ToString());

        return new PayOsReturnResult(false, "Đang chờ xử lý", payment.Status.ToString());
    }
}
```

- [ ] **Step 2: Delete `PaymentService` from `ModuleServices.cs`**

Remove the `PaymentService` class (lines 1237-1558).

---

## Task 10: Update `PaymentController` to accept gateway choice

**Files:**
- Modify: `AIStudyHub.API/Controllers/PaymentController.cs`

- [ ] **Step 1: Update `create-checkout-url` endpoint**

```csharp
[HttpPost("create-checkout-url")]
public async Task<ActionResult<PaymentLinkResponseDto>> CreatePaymentUrl(
    [FromBody] CreateCheckoutRequest request,
    CancellationToken cancellationToken)
{
    var gateway = request.Gateway?.ToLowerInvariant() switch
    {
        "payos" => Data.Enums.PaymentGateway.PayOS,
        _ => Data.Enums.PaymentGateway.VNPay
    };

    var response = await _service.CreatePaymentUrlAsync(
        new CreatePaymentLinkRequestDto(request.TierId, request.Amount),
        gateway,
        HttpContext,
        cancellationToken);
    return Ok(response);
}

public sealed record CreateCheckoutRequest(Guid TierId, decimal? Amount = null, string? Gateway = null);
```

- [ ] **Step 2: Add `payos-webhook` endpoint**

```csharp
[HttpPost("payos-webhook")]
[AllowAnonymous]
public async Task<IActionResult> PayOsWebhook([FromBody] PayOsWebhookPayload payload, CancellationToken cancellationToken)
{
    var success = await _service.ProcessPayOsWebhookAsync(payload, cancellationToken);
    if (success)
        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    return Ok(new { RspCode = "97", Message = "Invalid Signature" });
}
```

Keep `vnpay-return` and `vnpay-ipn` endpoints unchanged.

---

## Task 11: Update DI registration

**Files:**
- Modify: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`

- [ ] **Step 1: Register PayOS**

```csharp
services.Configure<PayOsOptions>(configuration.GetSection(PayOsOptions.SectionName));
services.AddScoped<VnPayService>();
services.AddScoped<PayOsGateway>();
services.AddScoped<IPaymentService, PaymentService>();
```

---

## Task 12: Update `appsettings.json`

**Files:**
- Modify: `AIStudyHub.API/appsettings.json`
- Modify: `AIStudyHub.API/appsettings.Development.json`

- [ ] **Step 1: Add PayOs section to `appsettings.json`**

```json
"PayOs": {
  "ClientId": "REPLACE_ME",
  "ApiKey": "REPLACE_ME",
  "ChecksumKey": "REPLACE_ME",
  "CancelUrl": "http://localhost:3000/pricing",
  "ReturnUrl": "http://localhost:3000/pricing/payos-return",
  "WebhookUrl": "http://localhost:5171/api/payment/payos-webhook"
}
```

Keep `VnPay` section as-is.

- [ ] **Step 2: Get sandbox credentials from PayOS**

Register at https://my.payos.vn/ → Sandbox → Get ClientId / ApiKey / ChecksumKey.

---

## Task 13: Build & verify

**Files:**
- None (verification only)

- [ ] **Step 1: Build the solution**

```bash
cd D:/GitHub/BE-SWP
dotnet build
```

Expected: 0 errors. Warnings about nullable `payment.OrderCode ??=...` are OK.

- [ ] **Step 2: Run migrations**

```bash
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

- [ ] **Step 3: Test VNPay flow still works**

POST `/api/Payment/create-checkout-url` with `{"tierId":"...","gateway":null}` → expect VNPay URL, no "Sai chữ ký" error.

- [ ] **Step 4: Test PayOS flow**

POST `/api/Payment/create-checkout-url` with `{"tierId":"...","gateway":"payos"}` → expect `https://pay.payos.vn/...` URL.

---

## Frontend Integration

The frontend `create-checkout-url` call must include `gateway` field:

```javascript
// VNPay (existing, default)
POST /api/Payment/create-checkout-url
{ "tierId": "...", "amount": 100000 }

// PayOS (new)
POST /api/Payment/create-checkout-url
{ "tierId": "...", "amount": 100000, "gateway": "payos" }
```

The response `paymentUrl` is a fully formed URL — redirect user to it (VNPay) or open in iframe (PayOS).

---

## Rollback Plan

If PayOS integration causes issues:
1. Remove `gateway: "payos"` from frontend request → falls back to VNPay
2. Don't remove VNPay code — both run in parallel
3. To fully remove PayOS: delete `PayOsGateway.cs`, `PayOsOptions.cs`, `PayOsGateway.cs` registration, and `PayOs` from appsettings

---

## PayOS Webhook Setup for Local Dev

PayOS requires the webhook URL to be publicly accessible. For local dev:

1. Install ngrok: `ngrok http 5171`
2. Copy https URL (e.g. `https://abc123.ngrok.io`)
3. Update `PayOs.WebhookUrl` in `appsettings.Development.json`:
   ```json
   "WebhookUrl": "https://abc123.ngrok.io/api/payment/payos-webhook"
   ```
4. After app starts, call once to register webhook with PayOS:
   ```csharp
   // In PayOsGateway constructor or startup
   await _client.Webhooks.ConfirmAsync("https://abc123.ngrok.io/api/payment/payos-webhook");
   ```
