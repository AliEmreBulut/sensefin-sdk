namespace SenseFin.Application.Interfaces;

// Hız (velocity) ve limit kontrolleri için soyutlama.
// Altyapı katmanı bunu Redis gibi teknolojilerle doldurur.
public interface IVelocityService
{
    // Verilen key için sayacı artırır ve yeni değeri döner.
    // Belirlenen süre sonunda sayaç otomatik sıfırlanır.
    Task<long> IncrementAsync(string key, TimeSpan window);
}
