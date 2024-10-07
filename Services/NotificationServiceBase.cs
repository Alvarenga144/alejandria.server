using Alejandria.Server.Models;
using MongoDB.Driver;

namespace Alejandria.Server.Services
{
    public class NotificationServiceBase
    {
        private readonly IMongoCollection<Notifications> _notisCollection;
    }
}