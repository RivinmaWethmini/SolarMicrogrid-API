// RESTful Web API controller for digital QR dispatch verification passes, energy collection authentication, and token validation.

using Microsoft.AspNetCore.Mvc;
using SolarAPI.Models;
using SolarAPI.Services;

namespace SolarAPI.Controllers;

[ApiController]
[Route("api/qr")]
public class QrVerificationController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public QrVerificationController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    /// <summary>
    /// Verifies a prosumer's scanned QR dispatch token and finalizes energy dispatch.
    /// Scanned by native Android operator app or React Web operator dashboard.
    /// </summary>
    /// <param name="request">Contains the full raw scanned JSON payload and operatorId</param>
    /// <returns>QrVerifyResult with dispatch details or error message</returns>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] QrVerifyRequest request)
    {
        // Begin execution of Verify method
        if (request == null || string.IsNullOrWhiteSpace(request.ScannedPayload))
        {
            return BadRequest(new QrVerifyResult
            {
                Success = false,
                Message = "Scanned QR payload is required."
            });
        }

        var result = await _reservationService.VerifyAndDispatchAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
