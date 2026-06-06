using System.Text.Json.Serialization;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Api.DTOs;

// Coğrafi konum verisi.
public sealed record LocationDto(
    double Latitude,
    double Longitude,
    string? Country = null,
    string? City = null
);
