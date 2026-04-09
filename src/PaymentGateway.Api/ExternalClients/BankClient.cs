using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.ExternalClients;

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions BankJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public BankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/payments", request, BankJsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<BankPaymentResponse>(BankJsonOptions);
    }
}
