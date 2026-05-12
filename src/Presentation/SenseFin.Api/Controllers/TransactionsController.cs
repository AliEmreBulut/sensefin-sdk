using MediatR;
using Microsoft.AspNetCore.Mvc;
using SenseFin.Api.DTOs;
using SenseFin.Application.Features.FraudEvaluation.Commands;

namespace SenseFin.Api.Controllers;

/// <summary>
/// API controller for transaction fraud analysis operations.
/// Receives transaction data from the mobile SDK and delegates to the CQRS pipeline.
/// </summary>
[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(
    IMediator mediator,
    ILogger<TransactionsController> logger) : ControllerBase
{
    /// <summary>
    /// Analyzes a transaction for potential fraud using the Sense-Fin pipeline.
    /// Pipeline: Persist → Velocity Check → AI Analysis → Risk Profile Update.
    /// </summary>
    /// <param name="request">Transaction data from the mobile SDK.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Risk assessment result including AI explanation.</returns>
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
