using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

public static class DescriptionFraudStrategy
{
    private static readonly string[] SuspiciousPatterns = [
        "hesabınıza para", "hesabınıza yatacak", "hesabiniza para", "hesabiniza yatacak",
        "onaylayın para", "onaylayin para", "onayladığınızda", "onayladiginizda",
        "para gönderilecek", "para gonderilecek", "havale gelecek", "iade edilecek",
        "geri ödeme", "geri odeme", "kazandınız", "kazandiniz", "ödülünüz", "odulunuz",
        "hediye", "para iadesi", "ücret iadesi", "ucret iadesi", "para transferi onay",
        "hesabınıza geçecek", "hesabiniza gececek", "onaylamanız halinde", "onaylamaniz halinde",
        "size para", "paranız gelecek", "paraniz gelecek"
    ];

    private static readonly string[] CorporatePatterns = [
        "siparis no", "sipariş no", "sipariş", "siparis", "fatura", "kargo bedeli",
        "kargo ücreti", "kargo ucreti", "urun bedeli", "ürün bedeli", "ürün kodu",
        "urun kodu", "shopier", "ilan no", "ilan bedeli", "mağaza", "magaza",
        "stok kodu", "teslimat ücreti", "teslimat ucreti", "tahsilat"
    ];

    public static DescriptionFraudResult Analyze(string? description, TransactionType transactionType, bool isReceiverCorporate = false)
    {
        if (string.IsNullOrWhiteSpace(description))
            return DescriptionFraudResult.Safe();

        // Performans Optimizasyonu: Heap allocation (ToLower) yerine doğrudan OrdinalIgnoreCase kullanımı
        var matchedCorporatePatterns = CorporatePatterns
            .Where(pattern => description.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matchedCorporatePatterns.Length > 0 && !isReceiverCorporate)
        {
            return new DescriptionFraudResult(
                IsSuspicious: true,
                Confidence: DescriptionFraudConfidence.High,
                MatchedPatterns: matchedCorporatePatterns,
                RecommendedRiskScore: 88,
                Reason: $"⚠️ ŞİRKET TAKLİDİ TESPİTİ (Semantic Identity Mismatch): İşlem açıklamasında kurumsal ifadeler tespit edildi: [{string.Join(", ", matchedCorporatePatterns)}], ancak alıcı hesap bireysel şahıs hesabıdır. Bu bir dolandırıcılık anomalisidir.");
        }

        var matchedPatterns = SuspiciousPatterns
            .Where(pattern => description.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matchedPatterns.Length == 0)
            return DescriptionFraudResult.Safe();

        if (transactionType == TransactionType.PaymentRequest)
        {
            return new DescriptionFraudResult(
                IsSuspicious: true,
                Confidence: DescriptionFraudConfidence.Definite,
                MatchedPatterns: matchedPatterns,
                RecommendedRiskScore: 98,
                Reason: $"⚠️ ÖDEME İSTEĞİ DOLANDIRICILIK TESPİTİ: Bu bir 'ödeme isteği' işlemidir — onaylandığında para size GELMEZ, karşı tarafa GİDER. Açıklamadaki şüpheli ifadeler: [{string.Join(", ", matchedPatterns)}].");
        }

        if (matchedPatterns.Length >= 2)
        {
            return new DescriptionFraudResult(
                IsSuspicious: true,
                Confidence: DescriptionFraudConfidence.High,
                MatchedPatterns: matchedPatterns,
                RecommendedRiskScore: 85,
                Reason: $"⚠️ ŞÜPHELİ AÇIKLAMA TESPİTİ: İşlem açıklamasında birden fazla dolandırıcılık göstergesi tespit edildi: [{string.Join(", ", matchedPatterns)}].");
        }

        return new DescriptionFraudResult(
            IsSuspicious: true,
            Confidence: DescriptionFraudConfidence.Moderate,
            MatchedPatterns: matchedPatterns,
            RecommendedRiskScore: 70,
            Reason: $"⚠️ ŞÜPHELİ AÇIKLAMA: İşlem açıklamasında dolandırıcılık göstergesi tespit edildi: [{string.Join(", ", matchedPatterns)}].");
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