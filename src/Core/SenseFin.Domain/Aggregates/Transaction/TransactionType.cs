namespace SenseFin.Domain.Aggregates.Transaction;

// Finansal işlem tiplerini temsil eder.
public enum TransactionType
{
    WireTransfer = 0,
    CardPayment = 1,
    P2PTransfer = 2,
    AtmWithdrawal = 3,
    OnlinePurchase = 4,
    PointOfSale = 5,
    CryptoTransfer = 6,
    Transfer = 7,
    PaymentRequest = 8 // Ödeme isteği (dolandırıcılık hedefi)
}
