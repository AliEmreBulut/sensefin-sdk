namespace SenseFin.Application.Common;

// Uygulama katmanındaki işlemler için başarı/hata durumunu dönen generic yapı.
public sealed record Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
    }

    // Başarılı sonuç döner
    public static Result<T> Success(T value) => new(true, value, null);

    // Hatalı sonuç döner
    public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
