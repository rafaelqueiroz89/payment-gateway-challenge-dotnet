using PaymentGateway.Api.ExternalClients;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Validators;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IBankClient _bankClient;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IPaymentsRepository paymentsRepository, IBankClient bankClient, ILogger<PaymentService> logger)
    {
        _paymentsRepository = paymentsRepository;
        _bankClient = bankClient;
        _logger = logger;
    }

    public PostPaymentResponse? GetPayment(Guid id)
    {
        _logger.LogInformation("Retrieving payment {PaymentId}", id);
        return _paymentsRepository.Get(id);
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PostPaymentRequest request)
    {
        IReadOnlyList<string> validationErrors = PaymentValidator.Validate(request);

        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("Payment validation failed: {Errors}", string.Join(", ", validationErrors));
            return new PaymentResult { Errors = validationErrors };
        }

        BankPaymentRequest bankRequest = new()
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency.ToUpperInvariant(),
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        BankPaymentResponse? bankResponse;

        try
        {
            bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bank communication error");
            bankResponse = null;
        }

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

        _logger.LogInformation("Payment {PaymentId} processed with status {Status}", response.Id, status);

        return new PaymentResult { Payment = response };
    }
}
