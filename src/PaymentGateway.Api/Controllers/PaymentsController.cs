using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostPaymentResponse>> GetPaymentAsync(Guid id)
    {
        PostPaymentResponse? payment = _paymentService.GetPayment(id);

        if (payment == null)
        {
            return NotFound();
        }

        return Ok(payment);
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(PostPaymentRequest request)
    {
        PaymentResult result = await _paymentService.ProcessPaymentAsync(request);

        if (!result.IsValid)
        {
            ModelStateDictionary modelState = new();
            foreach (string error in result.Errors!)
            {
                modelState.AddModelError("Validation", error);
            }
            return ValidationProblem(modelState);
        }

        return Ok(result.Payment);
    }
}
