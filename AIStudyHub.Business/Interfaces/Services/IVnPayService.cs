using Microsoft.AspNetCore.Http;

namespace AIStudyHub.Business.Interfaces.Services;

public interface IVnPayService
{
    string CreatePaymentUrl(HttpContext context, Guid paymentId, decimal amount, string orderInfo);
    bool ValidateSignature(IQueryCollection query);
}
