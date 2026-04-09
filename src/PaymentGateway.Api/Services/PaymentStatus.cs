using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Services;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Authorized,
    Declined,
    Rejected
}