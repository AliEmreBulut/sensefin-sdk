using MediatR;
using SenseFin.Application.Common;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

// CQRS Command — gelen işlem analizi isteğini temsil eder.
// Controller'dan MediatR aracılığıyla gönderilir.
public sealed record AnalyzeTransactionCommand(
    decimal Amount,
    string Currency,
    TransactionType TransactionType,
    string SenderDeviceId,
    string SenderAccountId,
    string ReceiverAccountId,
    DateTime TransactionDate,
    string? SenderIpAddress = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Country = null,
    string? City = null,
    string? MerchantId = null,
    string? Description = null,
    string? ReceiverIban = null,
    double? TypingScore = null,
    double? TremorScore = null
) : IRequest<Result<AnalyzeTransactionResponse>>;
