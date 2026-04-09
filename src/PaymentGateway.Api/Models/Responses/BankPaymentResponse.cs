namespace PaymentGateway.Api.Models.Responses;

public class BankPaymentResponse
{
    public bool Authorized { get; set; }
    public string AuthorizationCode { get; set; } = string.Empty;
}
