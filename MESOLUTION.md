# Payment Gateway - Design Considerations & Assumptions

## Architecture

The solution follows a simple layered approach inside a single API project:

- **Controller** - HTTP routing and request orchestration
- **Services** - Validation, bank communication, in-memory storage
- **Models** - Request/response DTOs and bank simulator contracts

I intentionally kept the architecture flat.

## Key Design Decisions

### Validation
Validation is handled by a static `PaymentValidator` class that returns a list of error messages. I chose this over FluentValidation to avoid adding a NuGet dependency for something that can be done in ~30 lines of simple if-checks.

When validation fails, the API returns HTTP 400 with `Status: Rejected` and does **not** call the acquiring bank. Rejected payments are not stored in the repository since only bank-processed payments (Authorized/Declined) should be retrievable.

### Bank Client
`BankClient` uses the typed HttpClient pattern (`AddHttpClient<IBankClient, BankClient>()`) which is idiomatic ASP.NET Core and handles HttpClient lifecycle properly without extra packages.

The interface `IBankClient` exists solely for testability - it allows tests to inject a mock without needing Moq or any mocking library.

When the bank returns a non-2xx response (e.g. 503), the client returns `null`, which the controller treats as `Declined`. This is a pragmatic choice - the payment was not authorized, so declined is the safest status.

### Card Data Security
The full card number only exists during the POST request processing. By the time the response is created and stored, only the last 4 digits are kept (as an integer). The full card number is never persisted.

### Currency Support
Supported currencies are stored in a `Dictionary<string, int>` mapping currency code to decimal places (minor unit digits):
- BRL (2 decimals)
- EUR (2 decimals)
- GBP (2 decimals)

This structure makes it easy to add currencies with different minor units in the future (e.g. JPY with 0 decimals).

### HTTP Status Codes
- **200 OK** - Payment processed (both Authorized and Declined). The bank call succeeded; the status field tells the result.
- **400 Bad Request** - Validation failed (Rejected). The bank was never called.
- **404 Not Found** - Payment ID does not exist.

### Enum Serialization
`PaymentStatus` uses `JsonStringEnumConverter` to serialize as `"Authorized"`, `"Declined"`, `"Rejected"` instead of `0`, `1`, `2`. This makes the API self-documenting.

## Assumptions

1. **In-memory storage is acceptable** - as stated in the requirements. No database or persistence layer.
2. **No authentication/authorization** - the requirements don't mention merchant authentication, so none is implemented.
3. **Bank simulator base URL is configurable** via `appsettings.json` (`BankSimulator:BaseUrl`), defaulting to `http://localhost:8080`.
4. **The expiry date is validated against UTC time** to avoid timezone ambiguity.
5. **A bank error (503) results in Declined** - since the payment was not authorized, treating it as declined is the safest default.

## Testing Strategy

22 integration tests using `WebApplicationFactory` with a hand-written `MockBankClient` (no Moq dependency):

- **2 GET tests** - successful retrieval and 404
- **4 POST flow tests** - authorized, declined, bank error, and GET-after-POST roundtrip
- **16 validation tests** - covering every field's edge cases (too short, too long, non-numeric, expired, invalid currency, zero amount, etc.)

All tests mock the bank client via DI replacement, so they run without Docker or the bank simulator.

## What I'd Add for Production

This solution meets the functional requirements but is not production-ready. Here's what I'd prioritise next:

### Security & Compliance
- **Merchant authentication** - API keys or OAuth tokens so only authorised merchants can process payments.
- **PCI-DSS compliance** - Card numbers should never appear in logs or memory dumps. Tokenisation or a dedicated card vault would replace storing even the last 4 digits in plain objects.
- **Idempotency keys** - A merchant-supplied idempotency key on POST to prevent duplicate charges on retries.

### Resilience
- **Timeouts** - The bank client currently has no timeout. A hanging bank call would block indefinitely.
- **Retry with backoff** - On transient bank failures (503), retry 2-3 times before giving up. Polly is a good fit here.
- **Circuit breaker** - If the bank is consistently failing, stop calling it temporarily to avoid cascading failures.

### Data & Storage
- **Persistent database** - Replace the in-memory list with a real store (PostgreSQL, DynamoDB, etc.).
- **Thread safety** - The current `List<T>` repository is not thread-safe under concurrent requests. A `ConcurrentDictionary` or proper DB solves this.
- **Merchant isolation** - Payments should be scoped to a merchant ID so merchants only see their own data.

### Observability
- **Structured logging** - Serilog with correlation IDs to trace a payment across gateway and bank calls.
- **Health checks** - A `/health` endpoint for load balancers and orchestrators.
- **Metrics** - Prometheus counters for payment outcomes (authorized/declined/rejected rates), bank latency, error rates.

### API
- **Global error handling middleware** - Catch unhandled exceptions and return a clean JSON error instead of a stack trace.
- **Rate limiting** - Protect against abuse or misconfigured merchant integrations.
- **API versioning** - So we can evolve the contract without breaking existing merchants.

## How to Run

```
make docker-up    # Start the bank simulator
make build        # Build the solution
make test         # Run all tests
make run          # Start the API (https://localhost:7092)
make docker-down  # Stop the bank simulator
```
