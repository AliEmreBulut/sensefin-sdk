using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

// İşlem açıklamalarındaki sosyal mühendislik kalıplarını tarar.
// Özellikle "ödeme isteği" (payment request) dolandırıcılıklarını hedefler.
public static class DescriptionFraudStrategy
{
    // Dolandırıcıların sık kullandığı Türkçe kalıplar
    private static readonly string[] SuspiciousPatterns =
    [
        "hesabınıza para",
        "hesabınıza yatacak",
        "hesabiniza para",
        "hesabiniza yatacak",
        "onaylayın para",
        "onaylayin para",
        "onayladığınızda",
        "onayladiginizda",
        "para gönderilecek",
        "para gonderilecek",
        "havale gelecek",
        "iade edilecek",
        "geri ödeme",
        "geri odeme",
        "kazandınız",
        "kazandiniz",
        "ödülünüz",
        "odulunuz",
        "hediye",
        "para iadesi",
        "ücret iadesi",
        "ucret iadesi",
        "para transferi onay",
        "hesabınıza geçecek",
        "hesabiniza gececek",
        "onaylamanız halinde",
        "onaylamaniz halinde",
        "size para",
        "paranız gelecek",
        "paraniz gelecek"
    ];

    // Açıklamayı analiz eder. Ödeme isteği tipindeyse daha katı davranır.
    public static DescriptionFraudResult Analyze(string? description, TransactionType transactionType)
    {
        if (string.IsNullOrWhiteSpace(description))
            return DescriptionFraudResult.Safe();

        var lowerDescription = description.ToLowerInvariant();

        var matchedPatterns = SuspiciousPatterns
            .Where(pattern => lowerDescription.Contains(pattern, StringComparison.Ordinal))
            .ToArray();

        if (matchedPatterns.Length == 0)
            return DescriptionFraudResult.Safe();

        // PaymentRequest + suspicious description = definite scam
        if (transactionType == TransactionType.PaymentRequest)
        {
            return new DescriptionFraudResult(
                IsSuspicious: true,
                Confidence: DescriptionFraudConfidence.Definite,
                MatchedPatterns: matchedPatterns,
                RecommendedRiskScore: 98,
                Reason: $"⚠️ ÖDEME İSTEĞİ DOLANDIRICILIK TESPİTİ: " +
                        $"Bu bir 'ödeme isteği' (payment request) işlemidir — onaylandığında para size GELMEZ, " +
                        $"karşı tarafa GİDER. Açıklamada şüpheli ifadeler tespit edildi: " +
                        $"[{string.Join(", ", matchedPatterns)}]. " +
                        $"Bu klasik bir sosyal mühendislik dolandırıcılığıdır.");
        }

        // Other transaction types with multiple suspicious phrases
        if (matchedPatterns.Length >= 2)
        {
            return new DescriptionFraudResult(
                IsSuspicious: true,
                Confidence: DescriptionFraudConfidence.High,
                MatchedPatterns: matchedPatterns,
                RecommendedRiskScore: 85,
                Reason: $"⚠️ ŞÜPHELİ AÇIKLAMA TESPİTİ: İşlem açıklamasında birden fazla dolandırıcılık " +
                        $"göstergesi tespit edildi: [{string.Join(", ", matchedPatterns)}]. " +
                        $"Sosyal mühendislik saldırısı olma ihtimali yüksektir.");
        }

        // Single match on non-PaymentRequest — moderate suspicion
        return new DescriptionFraudResult(
            IsSuspicious: true,
            Confidence: DescriptionFraudConfidence.Moderate,
            MatchedPatterns: matchedPatterns,
            RecommendedRiskScore: 70,
            Reason: $"⚠️ ŞÜPHELİ AÇIKLAMA: İşlem açıklamasında dolandırıcılık göstergesi tespit edildi: " +
                    $"[{string.Join(", ", matchedPatterns)}]. Dikkatli olunması önerilir.");
    }
}

// Analiz sonucu
public sealed record DescriptionFraudResult(
    bool IsSuspicious,
    DescriptionFraudConfidence Confidence,
    string[] MatchedPatterns,
    double RecommendedRiskScore,
    string Reason)
{
    // Temiz (güvenli) sonuç döndürür
    public static DescriptionFraudResult Safe() => new(
        IsSuspicious: false,
        Confidence: DescriptionFraudConfidence.None,
        MatchedPatterns: [],
        RecommendedRiskScore: 0,
        Reason: string.Empty);
}

// Tespit güven düzeyi
public enum DescriptionFraudConfidence
{
    None = 0,
    Moderate = 1,
    High = 2,
    Definite = 3
}
