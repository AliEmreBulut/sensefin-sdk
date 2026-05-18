using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Infrastructure.AiServices;

// Google Gemini üzerinden dolandırıcılık analizi yapan servis.
// İşlemi prompt olarak gönderip risk skorunu ve sebebini geri alır.
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Gemini API request was explicitly canceled by the user or system pipeline.");
                throw; // Retry döngüsüne girmeden doğrudan metottan fırlatır.
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

    // Gemini'ye gönderilecek prompt'u hazırlar. JSON formatında cevap ister.
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
        - Tür: {{transaction.TransactionType}}{{(transaction.TransactionType == TransactionType.PaymentRequest ? " ⚠️ (ÖDEME İSTEĞİ — onaylandığında para KARŞI TARAFA gider!)" : "")}}
        - Gönderen Hesap: {{transaction.SenderAccountId}}
        - Alıcı Hesap: {{transaction.ReceiverAccountId}}
        - Gönderen IBAN: {{transaction.SenderIban ?? "N/A"}}
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
        2. Değerlendirmen için net, anlaşılır ve ÇOK KISA bir Türkçe ile neden belirt. (Maksimum 2-3 kısa cümle, toplam en fazla 40 kelime olmalıdır. Mobil uygulamada küçük bir ekrana sığacak). Hukuki sebeplerden dolayı KESİN yargılardan kaçın. "Kanıtlamaktadır", "dolandırıcıdır" gibi kesin hüküm bildiren ifadeler YERİNE "yüksek risk taşımaktadır", "şüpheli görünmektedir", "dolandırıcılık ihtimali bulunmaktadır" gibi esnek ifadeler kullan.
        3. Şu faktörleri göz önünde bulundur: Olağandışı tutar, şüpheli gönderici/alıcı desenleri, coğrafi anomaliler ve işlem açıklaması. DİKKAT: Yüksek Titreme Puanı veya Yazım Hızı Puanı tek başına KESİN dolandırıcılık kanıtı DEĞİLDİR (kullanıcı sadece aceleci veya hareket halinde olabilir). Bu fiziksel cihaz anomalilerini yalnızca diğer şüpheli durumlarla (yanıltıcı açıklama, gizli IBAN vb.) BİRLEŞTİĞİNDE riski artıran yan faktörler olarak kullan.
        4. Alıcı IBAN bilgisini mutlaka analiz et. IBAN'ın ülke kodu, banka kodu ve eğer varsa risk geçmişi hakkında yorumda bulun.
        5. ÖNEMLİ — ÖDEME İSTEĞİ DOLANDIRICILIK KONTROLÜ: İşlem türü "PaymentRequest" ise bu bir ÖDEME İSTEĞİDİR (gelen para DEĞİLDİR). 
           - SADECE şu durumlarda 0.80+ risk ver: Açıklamada "para yatacak", "hesabınıza gelecek", "iade", "kazandınız" gibi kandırmaca kelimeler varsa VEYA fiziksel cihaz anomalileri (titreme/yazım) yüksekse.
           - EĞER açıklama sıradan, günlük veya genel bir ödeme/borç talebiyse (örneğin "borcunuzu ödeyin", "kira", "hesap", "inşaat") ve cihaz verileri normalse, SIRF ödeme isteği olduğu için YÜKSEK RİSK VERME. Risk skorunu düşük/orta (0.10 - 0.40) seviyede tut. Sadece şu uyarıyı yap: "Bu bir ÖDEME İSTEĞİDİR. Onaylarsanız hesabınızdan para çıkıp karşı tarafa gidecektir. Lütfen tarafı tanıdığınızdan emin olun."
        6. ⚠️ ŞİRKET TAKLİDİ / SAHTE TİCARİ KONTROL: Eğer Alıcı Hesap Türü 'BIREYSEL (Şahıs Hesabı)' olarak belirtilmişse, ancak işlem açıklamasında 'sipariş no', 'fatura', 'ürün bedeli', 'ilan no', 'kargo bedeli', 'shopier', 'mağaza' gibi kurumsal/ticari ifadeler geçiyorsa, bu durum kuvvetli bir Şirket Taklidi Dolandırıcılığı (Corporate Impersonation) riskidir. Bu çelişkiyi yakaladığında risk skorunu doğrudan 0.85+ (High) olarak belirle ve reason kısmında 'Açıklamadaki kurumsal ifadeler ile alıcı hesabın bireysel şahıs hesabı olması çelişki ve anomali oluşturmaktadır — sahte mağaza/şirket taklidi dolandırıcılığı ihtimali yüksektir' uyarısını kurumsal, olasılık tabanlı siber güvenlik diliyle yap.
        7. Eğer para gönderim işlemi varsa açıklamaya bakma sadece karşıdaki kişinin iban risk scoreuna göre değerlendirme yap.

        **🚨 ÇIKTI FORMATI GÜVENCESİ:**
        - Yalnızca tek bir satırda, ham ve geçerli bir JSON objesi döndür.
        - Yanıtının başına veya sonuna ```json veya ``` gibi markdown işaretleri ekleme.
        - JSON içindeki "reason" metninin içinde asla çift tırnak (") kullanma, gerekirse tek tırnak (') kullan. Formatı bozacak hiçbir kaçış karakteri üretme.

        **Yanıt Formatı:**
        {"riskScore": 0.0, "reason": "Buraya Türkçe açıklamanı yaz"}
        """;
}

    // Gelen JSON cevabını parçalayıp skor ve sebebi ayıklar.
    private RiskAnalysisResult ParseGeminiResponse(string responseBody)
    {
        try
        {

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Navigate: candidates[0].content.parts[0].text
            var candidate = root.GetProperty("candidates")[0];
            if (candidate.TryGetProperty("finishReason", out var finishReasonElement) && finishReasonElement.GetString() == "SAFETY")
            {
                // Güvenlik filtresini tetikleyen bir açıklama kesinlikle dolandırıcılık şüphesidir!
                return new RiskAnalysisResult(0.99, "⚠️ AI GÜVENLİK FİLTRESİ TETİKLENDİ: İşlem açıklamasındaki ifadeler sistem tarafından engellendi.");
            }
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

            // Yapay zekanın ürettiği metinden sadece saf JSON bloğunu çekip çıkarır.
            var cleanedText = textContent.Trim();
            var firstCurly = cleanedText.IndexOf('{');
            var lastCurly = cleanedText.LastIndexOf('}');
            
            if (firstCurly != -1 && lastCurly != -1 && lastCurly > firstCurly)
            {
                cleanedText = cleanedText.Substring(firstCurly, lastCurly - firstCurly + 1);
            }

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
