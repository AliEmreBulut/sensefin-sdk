namespace SenseFin.Application.Interfaces;

// Tüm veri tabanı işlemlerini tek bir işlem (Transaction) altında toplamak ve
// toplu kaydetmeyi (Unit of Work) sağlamak için kullanılan arayüz.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
