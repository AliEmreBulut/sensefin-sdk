using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Interfaces;

/// <summary>
/// Contract for an AI-powered risk analysis service.
/// Infrastructure layer provides the concrete implementation (e.g., Gemini, OpenAI).
/// </summary>
public interface IRiskAnalystService
{
    /// <summary>
    /// Analyzes a transaction and returns a risk score with an explanation.
    /// </summary>
    /// <param name="transaction">The transaction aggregate to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing a numeric score and human-readable reason.</returns>
    Task<RiskAnalysisResult> AnalyzeAsync(TransactionAggregate transaction, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the output of an AI risk analysis.
/// Score is normalized between 0.0 (safe) and 1.0 (fraudulent).
/// Reason provides Explainable AI (XAI) context for the decision.
/// </summary>
/// <param name="Score">Risk score between 0.0 and 1.0.</param>
/// <param name="Reason">Human-readable explanation of the risk assessment.</param>
public sealed record RiskAnalysisResult(double Score, string Reason);
