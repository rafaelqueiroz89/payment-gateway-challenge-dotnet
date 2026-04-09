using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentService
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly IBankClient _bankClient;

    public PaymentService(PaymentsRepository paymentsRepository, IBankClient bankClient)
    {
        _paymentsRepository = paymentsRepository;
        _bankClient = bankClient;
    }

    public PostPaymentResponse? GetPayment(Guid id)
    {
        return _paymentsRepository.Get(id);
    }

    public async Task<PostPaymentResponse?> ProcessPaymentAsync(PostPaymentRequest request)
    {
        List<string> validationErrors = PaymentValidator.Validate(request);

        if (validationErrors.Count > 0)
        {
            return null;
        }

        BankPaymentRequest bankRequest = new()
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency.ToUpperInvariant(),
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        BankPaymentResponse? bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);

        PaymentStatus status = bankResponse is { Authorized: true }
            ? PaymentStatus.Authorized
            : PaymentStatus.Declined;

        PostPaymentResponse response = new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            CardNumberLastFour = int.Parse(request.CardNumber[^4..]),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency.ToUpperInvariant(),
            Amount = request.Amount
        };

        _paymentsRepository.Add(response);

        return response;
    }
}
