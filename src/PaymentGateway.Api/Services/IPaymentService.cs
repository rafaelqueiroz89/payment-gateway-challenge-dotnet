using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentService
{
    PostPaymentResponse? GetPayment(Guid id);
    Task<PaymentResult> ProcessPaymentAsync(PostPaymentRequest request);
}
