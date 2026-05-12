using System.Text.Json.Serialization;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Api.DTOs;

/// <summary>
/// Inbound DTO representing a transaction analysis request from the mobile SDK.
/// </summary>
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
    string? ReceiverIban = null,
    double? TypingScore = null,
    double? TremorScore = null
);

/// <summary>
/// DTO for monetary amount with ISO 4217 currency code.
/// </summary>
public sealed record MoneyDto(decimal Amount, string Currency);

/// <summary>
/// DTO for geographic location data.
/// </summary>
public sealed record LocationDto(
    double Latitude,
    double Longitude,
    string? Country = null,
    string? City = null
);
