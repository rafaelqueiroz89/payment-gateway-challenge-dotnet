using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.ExternalClients;

public interface IBankClient
{
    Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request);
}
