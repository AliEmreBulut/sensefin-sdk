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
        string? receiverRiskContext = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");

        var model = configuration["Gemini:Model"] ?? "gemini-2.0-flash";

        // v1 for 1.5 models, v1beta for 2.0+ models — can be overridden via config
        var apiVersion = configuration["Gemini:ApiVersion"]
            ?? (model.Contains("1.5") ? "v1" : "v1beta");

        var endpoint = $"https://generativelanguage.googleapis.com/{apiVersion}/models/{model}:generateContent?key={apiKey}";

        var prompt = BuildPrompt(transaction, receiverRiskContext);

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

        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var isTransient = response.StatusCode is
                        System.Net.HttpStatusCode.ServiceUnavailable or   // 503
                        System.Net.HttpStatusCode.TooManyRequests or      // 429
                        System.Net.HttpStatusCode.BadGateway or           // 502
                        System.Net.HttpStatusCode.GatewayTimeout;         // 504

                    if (isTransient && attempt < maxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                        logger.LogWarning(
                            "Gemini API returned {StatusCode} on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s...",
                            response.StatusCode, attempt, maxRetries, delay.TotalSeconds);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    logger.LogError(
                        "Gemini API returned {StatusCode} after {Attempt} attempt(s): {Body}",
                        response.StatusCode, attempt, responseBody);

                    return new RiskAnalysisResult(0.5, "AI analysis unavailable — default medium risk assigned.");
                }

                return ParseGeminiResponse(responseBody);
            }
            catch (TaskCanceledException)
            {
                throw; // Don't retry on cancellation
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    logger.LogWarning(ex,
                        "Gemini request failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s...",
                        attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                logger.LogError(ex, "Failed to invoke Gemini risk analysis for transaction {TransactionId} after {MaxRetries} attempts.",
                    transaction.Id, maxRetries);

                return new RiskAnalysisResult(0.5, $"AI analysis failed: {ex.Message} — default medium risk assigned.");
            }
        }

        // Should never reach here, but safety net
        return new RiskAnalysisResult(0.5, "AI analysis exhausted all retries — default medium risk assigned.");
    }

    /// <summary>
    /// Builds a structured prompt that instructs Gemini to return a JSON risk assessment.
    /// </summary>
    private static string BuildPrompt(TransactionAggregate transaction, string? receiverRiskContext = null)
    {
        var ibanSection = string.IsNullOrWhiteSpace(receiverRiskContext)
            ? ""
            : $"""

            **⚠️ Alıcı IBAN Risk Geçmişi (Sistemden):**
            {receiverRiskContext}
            Bu bilgiyi değerlendirmende mutlaka dikkate al ve reason kısmında alıcı IBAN'ının risk durumunu açıkça belirt.
            """;

        return $$"""
            Sen bir finansal dolandırıcılık tespit yapay zeka analistisin. Aşağıdaki işlemi analiz et ve dolandırıcılık riskini değerlendir.

            **İşlem Detayları:**
            - İşlem ID: {{transaction.Id}}
            - Tutar: {{transaction.Money.Amount}} {{transaction.Money.Currency}}
            - Tür: {{transaction.TransactionType}}
            - Gönderen Hesap: {{transaction.SenderAccountId}}
            - Alıcı Hesap: {{transaction.ReceiverAccountId}}
            - Alıcı IBAN: {{transaction.ReceiverIban ?? "N/A"}}
            - Gönderen Cihaz ID: {{transaction.SenderDeviceId}}
            - Gönderen IP: {{transaction.SenderIpAddress ?? "N/A"}}
            - Konum: {{transaction.Location?.ToString() ?? "N/A"}}
            - Üye İşyeri: {{transaction.MerchantId ?? "N/A"}}
            - Açıklama (Önemli): {{transaction.Description ?? "N/A"}}
            - Yazım Hızı Puanı (0-100, >60 anomali gösterir): {{transaction.TypingScore?.ToString() ?? "N/A"}}
            - Cihaz Titreme Puanı (0-100, >60 anomali gösterir): {{transaction.TremorScore?.ToString() ?? "N/A"}}
            - Tarih: {{transaction.TransactionDate:O}}
            {{ibanSection}}

            **Talimatlar:**
            1. Dolandırıcılık riskini 0.0 (tamamen güvenli) ile 1.0 (kesinlikle dolandırıcılık) arasında bir ölçekte değerlendir.
            2. Değerlendirmen için net ve kısa bir neden belirt (1-2 cümle). Neden mutlaka TÜRKÇE olmalıdır.
            3. Şu faktörleri göz önünde bulundur: Olağandışı tutar, şüpheli gönderici/alıcı desenleri, coğrafi anomaliler, cihaz parmak izi, işlem açıklaması anomalileri ve en önemlisi: Yüksek Titreme Puanı (kullanıcı gergin veya baskı altında olabilir) veya Yüksek Yazım Hızı Puanı (kullanıcı düzensiz davranıyor olabilir) gibi fiziksel anomaliler.
            4. Alıcı IBAN bilgisini mutlaka analiz et. IBAN'ın ülke kodu, banka kodu ve eğer varsa risk geçmişi hakkında yorumda bulun.

            **Yanıt Formatı (sadece saf JSON, markdown kullanma):**
            {"riskScore": 0.0, "reason": "Buraya Türkçe açıklamanı yaz"}
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
