using MediatR;
using Microsoft.AspNetCore.Mvc;
using SenseFin.Api.DTOs;
using SenseFin.Application.Features.FraudEvaluation.Commands;

namespace SenseFin.Api.Controllers;

// İşlem dolandırıcılık analizi işlemlerini yöneten controller.
// Mobil SDK'dan gelen verileri CQRS pipeline'ına yönlendirir.
[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(
    IMediator mediator,
    ILogger<TransactionsController> logger) : ControllerBase
{
    // Mobil SDK'dan gelen işlemleri analiz eder.
    // Akış: Kayıt -> Limit Kontrolü -> AI Analizi -> Risk Profil Güncelleme.
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalyzeTransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Analyze(
        [FromBody] TransactionRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Received analysis request for account {AccountId}, amount {Amount} {Currency}.",
            request.SenderAccountId, request.Money.Amount, request.Money.Currency);

        var command = new AnalyzeTransactionCommand(
            Amount: request.Money.Amount,
            Currency: request.Money.Currency,
            TransactionType: request.TransactionType,
            SenderDeviceId: request.SenderDeviceId,
            SenderAccountId: request.SenderAccountId,
            ReceiverAccountId: request.ReceiverAccountId,
            TransactionDate: request.TransactionDate ?? DateTime.UtcNow,
            SenderIpAddress: request.SenderIpAddress,
            Latitude: request.Location?.Latitude,
            Longitude: request.Location?.Longitude,
            Country: request.Location?.Country,
            City: request.Location?.City,
            MerchantId: request.MerchantId,
            Description: request.Description,
            ReceiverIban: request.ReceiverIban,
            TypingScore: request.TypingScore,
            TremorScore: request.TremorScore);

        var result = await mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Transaction analysis failed: {Error}", result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }
}
