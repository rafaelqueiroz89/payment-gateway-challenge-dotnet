using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.ExternalClients;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private readonly Random _random = new();

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        PostPaymentResponse payment = new()
        {
            Id = Guid.NewGuid(),
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999),
            Currency = "GBP"
        };

        PaymentsRepository paymentsRepository = new();
        paymentsRepository.Add(payment);

        HttpClient client = CreateClient(paymentsRepository, new MockBankClient(true));

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/Payments/{payment.Id}");
        GetPaymentResponse? paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse.Id);
        Assert.Equal(payment.CardNumberLastFour, paymentResponse.CardNumberLastFour);
        Assert.Equal(payment.Amount, paymentResponse.Amount);
        Assert.Equal(payment.Currency, paymentResponse.Currency);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- POST: Authorized flow ---

    [Fact]
    public async Task PostPayment_ValidRequest_BankAuthorizes_ReturnsAuthorized()
    {
        // Arrange
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = new()
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);
        PostPaymentResponse? paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal(8877, paymentResponse.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(100, paymentResponse.Amount);
        Assert.NotEqual(Guid.Empty, paymentResponse.Id);
    }

    [Fact]
    public async Task PostPayment_ValidRequest_BankDeclines_ReturnsDeclined()
    {
        // Arrange
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(false));

        PostPaymentRequest request = new()
        {
            CardNumber = "2222405343248878",
            ExpiryMonth = 6,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "BRL",
            Amount = 500,
            Cvv = "456"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);
        PostPaymentResponse? paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse.Status);
        Assert.Equal(8878, paymentResponse.CardNumberLastFour);
    }

    [Fact]
    public async Task PostPayment_ValidRequest_BankError_ReturnsDeclined()
    {
        // Arrange
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(null));

        PostPaymentRequest request = new()
        {
            CardNumber = "2222405343248870",
            ExpiryMonth = 1,
            ExpiryYear = DateTime.UtcNow.Year + 2,
            Currency = "EUR",
            Amount = 1050,
            Cvv = "789"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);
        PostPaymentResponse? paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse.Status);
    }

    // --- POST: Retrieve after creation ---

    [Fact]
    public async Task GetPayment_AfterPost_ReturnsCorrectPayment()
    {
        // Arrange
        PaymentsRepository repository = new();
        HttpClient client = CreateClient(repository, new MockBankClient(true));

        PostPaymentRequest request = new()
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 250,
            Cvv = "123"
        };

        // Act - POST
        HttpResponseMessage postResponse = await client.PostAsJsonAsync("/api/Payments", request);
        PostPaymentResponse? postResult = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Act - GET
        HttpResponseMessage getResponse = await client.GetAsync($"/api/Payments/{postResult!.Id}");
        GetPaymentResponse? getResult = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getResult);
        Assert.Equal(postResult.Id, getResult.Id);
        Assert.Equal(postResult.Status, getResult.Status);
        Assert.Equal(postResult.CardNumberLastFour, getResult.CardNumberLastFour);
        Assert.Equal(postResult.Amount, getResult.Amount);
    }

    // --- POST: Validation - Card Number ---

    [Fact]
    public async Task PostPayment_CardNumberTooShort_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.CardNumber = "1234567890123"; // 13 digits

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_CardNumberTooLong_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.CardNumber = "12345678901234567890"; // 20 digits

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_CardNumberNonNumeric_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.CardNumber = "2222ABCD43248877";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_CardNumberEmpty_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.CardNumber = "";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- POST: Validation - Expiry ---

    [Fact]
    public async Task PostPayment_ExpiryMonthBelow1_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.ExpiryMonth = 0;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_ExpiryMonthAbove12_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.ExpiryMonth = 13;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_ExpiryDateInPast_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year - 1;
        request.ExpiryMonth = 1;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_ExpiryCurrentYearPastMonth_ReturnsRejected()
    {
        // Only run this test if we're not in January (otherwise there's no past month this year)
        if (DateTime.UtcNow.Month == 1)
        {
            return;
        }

        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year;
        request.ExpiryMonth = DateTime.UtcNow.Month - 1;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- POST: Validation - Currency ---

    [Fact]
    public async Task PostPayment_InvalidCurrency_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Currency = "ABC";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_CurrencyWrongLength_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Currency = "US";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- POST: Validation - Amount ---

    [Fact]
    public async Task PostPayment_AmountZero_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Amount = 0;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_AmountNegative_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Amount = -100;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- POST: Validation - CVV ---

    [Fact]
    public async Task PostPayment_CvvTooShort_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Cvv = "12";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_CvvTooLong_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Cvv = "12345";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_CvvNonNumeric_ReturnsRejected()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Cvv = "12A";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPayment_CvvFourDigits_IsValid()
    {
        HttpClient client = CreateClient(new PaymentsRepository(), new MockBankClient(true));

        PostPaymentRequest request = CreateValidRequest();
        request.Cvv = "1234";

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PostPaymentResponse? body = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.Equal(PaymentStatus.Authorized, body!.Status);
    }

    // --- Helpers ---

    private static PostPaymentRequest CreateValidRequest()
    {
        return new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }

    private static HttpClient CreateClient(IPaymentsRepository repository, IBankClient bankClient)
    {
        WebApplicationFactory<PaymentsController> webApplicationFactory = new();
        return webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ((ServiceCollection)services).AddSingleton(repository);
                ((ServiceCollection)services).AddSingleton(bankClient);
            }))
            .CreateClient();
    }
}

internal class MockBankClient : IBankClient
{
    private readonly BankPaymentResponse? _response;

    public MockBankClient(bool? authorized)
    {
        if (authorized.HasValue)
        {
            _response = new BankPaymentResponse
            {
                Authorized = authorized.Value,
                AuthorizationCode = authorized.Value ? Guid.NewGuid().ToString() : string.Empty
            };
        }
        else
        {
            _response = null;
        }
    }

    public Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request)
    {
        return Task.FromResult(_response);
    }
}
