using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentResult
{
    public PostPaymentResponse? Payment { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public bool IsValid => Errors == null || Errors.Count == 0;
}
