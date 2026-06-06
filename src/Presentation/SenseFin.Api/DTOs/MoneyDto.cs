using System.Text.Json.Serialization;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Api.DTOs;

// Tutar ve para birimi bilgisi.
public sealed record MoneyDto(decimal Amount, string Currency);