using MediatR;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands.AnalyzeTransaction;

// İşlem analizi isteğini temsil eden command nesnesi.
public sealed record AnalyzeTransactionCommand : IRequest<AnalyzeTransactionResult>
{
    // Temel bilgiler

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required TransactionType TransactionType { get; init; }

    // Cihaz bilgileri

    public required string SenderDeviceId { get; init; }

    public string? SenderIpAddress { get; init; }

    // Hesaplar

    public required string SenderAccountId { get; init; }

    public required string ReceiverAccountId { get; init; }

    // Konum

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? Country { get; init; }

    public string? City { get; init; }

    // Diğer veriler

    public required DateTime TransactionDate { get; init; }

    public string? MerchantId { get; init; }

    public string? Description { get; init; }
}
