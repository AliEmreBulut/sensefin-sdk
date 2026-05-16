using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Interfaces;

// Yapay zeka destekli risk analizi servisi için arayüz.
// Infrastructure katmanı bunu Gemini, OpenAI gibi servislerle doldurur.
public interface IRiskAnalystService
{
    // İşlemi analiz eder ve bir risk skoru ile açıklama döner
    Task<RiskAnalysisResult> AnalyzeAsync(TransactionAggregate transaction, string? receiverRiskContext = null, CancellationToken cancellationToken = default);
}

// AI risk analizi çıktısı.
// Score: 0.0 (güvenli) ile 1.0 (dolandırıcılık) arasındadır.
// Reason: Kararın nedenini açıklayan metin.
public sealed record RiskAnalysisResult(double Score, string Reason);
