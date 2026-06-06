using SenseFin.Domain.Aggregates.Transaction;
using System.Text.Json.Serialization;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;
// Tespit güven düzeyi
public enum DescriptionFraudConfidence
{
    None = 0,
    Moderate = 1,
    High = 2,
    Definite = 3
}