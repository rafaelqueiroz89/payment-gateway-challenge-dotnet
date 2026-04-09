using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validators;

public static class PaymentValidator
{
    private static readonly Dictionary<string, int> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        { "BRL", 2 },
        { "EUR", 2 },
        { "GBP", 2 }
    };

    public static IReadOnlyList<string> Validate(PostPaymentRequest request)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(request.CardNumber))
        {
            errors.Add("Card number is required.");
        }
        else
        {
            if (request.CardNumber.Length < 14 || request.CardNumber.Length > 19)
            {
                errors.Add("Card number must be between 14 and 19 characters long.");
            }

            if (!request.CardNumber.All(char.IsDigit))
            {
                errors.Add("Card number must only contain numeric characters.");
            }
        }

        if (request.ExpiryMonth < 1 || request.ExpiryMonth > 12)
        {
            errors.Add("Expiry month must be between 1 and 12.");
        }

        DateTime now = DateTime.UtcNow;
        if (request.ExpiryYear < now.Year ||
            (request.ExpiryYear == now.Year && request.ExpiryMonth < now.Month))
        {
            errors.Add("Card expiry date must be in the future.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            errors.Add("Currency is required.");
        }
        else if (request.Currency.Length != 3 || !SupportedCurrencies.ContainsKey(request.Currency))
        {
            errors.Add($"Currency must be one of: {string.Join(", ", SupportedCurrencies.Keys)}.");
        }

        if (request.Amount <= 0)
        {
            errors.Add("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Cvv))
        {
            errors.Add("CVV is required.");
        }
        else
        {
            if (request.Cvv.Length < 3 || request.Cvv.Length > 4)
            {
                errors.Add("CVV must be 3 or 4 characters long.");
            }

            if (!request.Cvv.All(char.IsDigit))
            {
                errors.Add("CVV must only contain numeric characters.");
            }
        }

        return errors;
    }
}
