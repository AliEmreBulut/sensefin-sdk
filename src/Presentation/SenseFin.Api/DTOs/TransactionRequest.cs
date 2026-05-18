using System.Text.Json.Serialization;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Api.DTOs;

// Mobil SDK'dan gelen işlem analizi isteğini temsil eden DTO.
public sealed record TransactionRequest(
    MoneyDto Money,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    TransactionType TransactionType,
    string SenderDeviceId,
    string SenderAccountId,
    string ReceiverAccountId,
    DateTime? TransactionDate = null,
    string? SenderIpAddress = null,
    LocationDto? Location = null,
    string? MerchantId = null,
    string? Description = null,
    string? SenderIban = null,
    string? ReceiverIban = null,
    double? TypingScore = null,
    double? TremorScore = null
);

// Tutar ve para birimi bilgisi.
public sealed record MoneyDto(decimal Amount, string Currency);

// Coğrafi konum verisi.
public sealed record LocationDto(
    double Latitude,
    double Longitude,
    string? Country = null,
    string? City = null
);
