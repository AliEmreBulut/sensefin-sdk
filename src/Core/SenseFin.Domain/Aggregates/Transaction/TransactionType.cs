namespace SenseFin.Domain.Aggregates.Transaction;

/// <summary>
/// Represents the type/channel of a financial transaction.
/// </summary>
public enum TransactionType
{
    /// <summary>Wire/bank transfer</summary>
    WireTransfer = 0,

    /// <summary>Card-based payment</summary>
    CardPayment = 1,

    /// <summary>Peer-to-peer transfer</summary>
    P2PTransfer = 2,

    /// <summary>ATM cash withdrawal</summary>
    AtmWithdrawal = 3,

    /// <summary>Online/e-commerce purchase</summary>
    OnlinePurchase = 4,

    /// <summary>Point-of-sale terminal transaction</summary>
    PointOfSale = 5,

    /// <summary>Cryptocurrency transfer</summary>
    CryptoTransfer = 6,

    /// <summary>Generic transfer (catch-all)</summary>
    Transfer = 7
}
