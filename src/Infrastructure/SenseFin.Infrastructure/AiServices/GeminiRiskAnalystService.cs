using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Infrastructure.AiServices;

/// <summary>
/// Google Gemini Pro API integration for AI-powered fraud risk analysis.
/// Sends structured transaction data as a prompt and parses the risk assessment response.
/// Implements the Explainable AI (XAI) pattern by extracting a human-readable reason.
/// </summary>
public sealed class GeminiRiskAnalystService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GeminiRiskAnalystService> logger) : IRiskAnalystService
{
    public async Task<RiskAnalysisResult> AnalyzeAsync(
        TransactionAggregate transaction,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");

        var model = configuration["Gemini:Model"] ?? "gemini-2.0-flash";

        // v1 for 1.5 models, v1beta for 2.0+ models — can be overridden via config
        var apiVersion = configuration["Gemini:ApiVersion"]
            ?? (model.Contains("1.5") ? "v1" : "v1beta");

        var endpoint = $"https://generativelanguage.googleapis.com/{apiVersion}/models/{model}:generateContent?key={apiKey}";

        var prompt = BuildPrompt(transaction);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 512
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Gemini API returned {StatusCode}: {Body}",
                    response.StatusCode, responseBody);

                return new RiskAnalysisResult(0.5, "AI analysis unavailable — default medium risk assigned.");
            }

            return ParseGeminiResponse(responseBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to invoke Gemini risk analysis for transaction {TransactionId}.",
                transaction.Id);

            return new RiskAnalysisResult(0.5, $"AI analysis failed: {ex.Message} — default medium risk assigned.");
        }
    }

    /// <summary>
    /// Builds a structured prompt that instructs Gemini to return a JSON risk assessment.
    /// </summary>
    private static string BuildPrompt(TransactionAggregate transaction)
    {
        return $$"""
            You are a financial fraud detection AI analyst. Analyze the following transaction and assess its fraud risk.

            **Transaction Details:**
            - Transaction ID: {{transaction.Id}}
            - Amount: {{transaction.Money.Amount}} {{transaction.Money.Currency}}
            - Type: {{transaction.TransactionType}}
            - Sender Account: {{transaction.SenderAccountId}}
            - Receiver Account: {{transaction.ReceiverAccountId}}
            - Receiver IBAN: {{transaction.ReceiverIban ?? "N/A"}}
            - Sender Device ID: {{transaction.SenderDeviceId}}
            - Sender IP: {{transaction.SenderIpAddress ?? "N/A"}}
            - Location: {{transaction.Location?.ToString() ?? "N/A"}}
            - Merchant: {{transaction.MerchantId ?? "N/A"}}
            - Description (Important): {{transaction.Description ?? "N/A"}}
            - Typing Cadence Score (0-100, >60 indicates anomaly): {{transaction.TypingScore?.ToString() ?? "N/A"}}
            - Device Tremor Score (0-100, >60 indicates anomaly): {{transaction.TremorScore?.ToString() ?? "N/A"}}
            - Date: {{transaction.TransactionDate:O}}

            **Instructions:**
            1. Evaluate the fraud risk on a scale from 0.0 (completely safe) to 1.0 (definitely fraudulent).
            2. Provide a clear, concise reason for your assessment (1-2 sentences).
            3. Consider factors like: unusual amount, suspicious sender/receiver patterns, geographic anomalies, device fingerprint, transaction description anomalies, and importantly: physical anomalies like High Tremor Score (e.g., user might be nervous or under coercion) or High Typing Cadence Score (e.g., user is behaving erratically).

            **Response Format (strict JSON only, no markdown):**
            {"riskScore": 0.0, "reason": "Your explanation here"}
            """;
    }

    /// <summary>
    /// Parses the Gemini API response to extract riskScore and reason fields.
    /// Falls back to a medium risk score if parsing fails.
    /// </summary>
    private RiskAnalysisResult ParseGeminiResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Navigate: candidates[0].content.parts[0].text
            var textContent = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(textContent))
            {
                logger.LogWarning("Empty text content in Gemini response.");
                return new RiskAnalysisResult(0.5, "AI returned empty response — default medium risk assigned.");
            }

            // Clean up potential markdown code fences from the response
            var cleanedText = textContent
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            using var resultDoc = JsonDocument.Parse(cleanedText);
            var resultRoot = resultDoc.RootElement;

            var score = resultRoot.GetProperty("riskScore").GetDouble();
            var reason = resultRoot.GetProperty("reason").GetString() ?? "No reason provided.";

            // Clamp score to valid range
            score = Math.Clamp(score, 0.0, 1.0);

            logger.LogInformation("Gemini analysis complete. Score: {Score}, Reason: {Reason}", score, reason);

            return new RiskAnalysisResult(score, reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse Gemini response: {ResponseBody}", responseBody);
            return new RiskAnalysisResult(0.5, "AI response parsing failed — default medium risk assigned.");
        }
    }
}
