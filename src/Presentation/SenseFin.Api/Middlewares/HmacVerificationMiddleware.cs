using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SenseFin.Api.Middlewares;

// Gelen isteklerin HMAC-SHA256 imzalarını kontrol eden middleware.
// X-SenseFin-Signature ve X-SenseFin-Timestamp header'larını doğrular, replay attackları önler.
public sealed partial class HmacVerificationMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<HmacVerificationMiddleware> logger)
{
    private const string SignatureHeader = "X-SenseFin-Signature";
    private const string TimestampHeader = "X-SenseFin-Timestamp";
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow non-API and health-check requests to pass through without HMAC
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        // 1. Header'ları ayıkla

        if (!context.Request.Headers.TryGetValue(SignatureHeader, out var signatureValues) ||
            string.IsNullOrWhiteSpace(signatureValues.FirstOrDefault()))
        {
            logger.LogWarning("Missing {Header} header.", SignatureHeader);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing {SignatureHeader} header." });
            return;
        }

        if (!context.Request.Headers.TryGetValue(TimestampHeader, out var timestampValues) ||
            string.IsNullOrWhiteSpace(timestampValues.FirstOrDefault()))
        {
            logger.LogWarning("Missing {Header} header.", TimestampHeader);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing {TimestampHeader} header." });
            return;
        }

        var receivedSignature = signatureValues.First()!;
        var timestampString = timestampValues.First()!;

        // 2. Replay attack koruması (Zaman damgası kontrolü)

        if (!long.TryParse(timestampString, out var timestampUnix))
        {
            logger.LogWarning("Invalid timestamp format: {Timestamp}", timestampString);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid timestamp format." });
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
        var timeDiff = DateTimeOffset.UtcNow - requestTime;

        if (timeDiff.Duration() > ReplayWindow)
        {
            logger.LogWarning(
                "Replay attack detected. Timestamp: {Timestamp}, Drift: {Drift}s",
                timestampString, timeDiff.TotalSeconds);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Request timestamp is outside the allowed window (replay attack protection)." });
            return;
        }

        // 3. Request body'sini oku (Controller da okuyabilsin diye buffer'ı aç)

        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        // Reset the stream position so downstream middleware/controllers can read it again
        context.Request.Body.Position = 0;

        // 4. HMAC-SHA256 hesapla ve karşılaştır

        var secretKey = configuration["HmacSettings:SecretKey"]
            ?? throw new InvalidOperationException("HmacSettings:SecretKey is not configured.");

        // Strip all whitespace to match the SDK/Postman pre-request script behavior
        var normalizedBody = WhitespaceRegex().Replace(body, "");

        var payload = $"{normalizedBody}.{timestampString}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var computedHash = HMACSHA256.HashData(keyBytes, payloadBytes);
        var computedSignature = Convert.ToBase64String(computedHash);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(receivedSignature)))
        {
            logger.LogWarning("HMAC signature mismatch for request to {Path}.", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid HMAC signature." });
            return;
        }

        logger.LogDebug("HMAC verification passed for {Path}.", context.Request.Path);

        // 5. Her şey tamamsa devam et

        await next(context);
    }

    [GeneratedRegex(@"\s")]
    private static partial Regex WhitespaceRegex();
}
