namespace SenseFin.Application.Interfaces;

/// <summary>
/// Abstraction for velocity/rate-limiting checks.
/// Infrastructure layer provides the concrete implementation (e.g., Redis).
/// </summary>
public interface IVelocityService
{
    /// <summary>
    /// Increments the request counter for a given key and returns the new count.
    /// The counter auto-expires after the specified window.
    /// </summary>
    /// <param name="key">The rate-limiting key (e.g., "velocity:{accountId}").</param>
    /// <param name="window">The sliding window duration for the counter.</param>
    /// <returns>The current count after incrementing.</returns>
    Task<long> IncrementAsync(string key, TimeSpan window);
}
