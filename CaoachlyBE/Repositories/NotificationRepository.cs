using CaoachlyBE.Entities;
using CaoachlyBE.Repositories.Interfaces;

namespace CaoachlyBE.Repositories;

public class NotificationRepository(AppDbContext context) : INotificationRepository
{
    public async Task AddAsync(Notification entity)
    {
        await context.Notifications.AddAsync(entity);
    }
}
