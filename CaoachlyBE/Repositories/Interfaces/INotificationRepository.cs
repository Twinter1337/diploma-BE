using CaoachlyBE.Entities;

namespace CaoachlyBE.Repositories.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification entity);
}
