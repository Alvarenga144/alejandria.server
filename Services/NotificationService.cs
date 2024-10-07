using Alejandria.Server.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Alejandria.Server.Services
{
    public class NotificationService
    {
        private readonly IMongoCollection<Notifications> _notisCollection;
        private readonly UserService _userService;

        public NotificationService(IOptions<MongoDBSettings> mongoDatabaseSettings, UserService userService)
        {
            var mongoClient = new MongoClient(mongoDatabaseSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseSettings.Value.DatabaseName);

            _notisCollection = mongoDatabase.GetCollection<Notifications>(mongoDatabaseSettings.Value.NotiCollectionName);
            _userService = userService;
        }

        // Metodos de servicio para notis

        // almacenar la notificacion
        public async Task CreateNotificationAsync(Notifications notification)
        {
            await _notisCollection.InsertOneAsync(notification);
        }

        public async Task<Notifications?> GetNotificationByIdAsync(string notificationId)
        {
            return await _notisCollection.Find(notification => notification.Id == notificationId).FirstOrDefaultAsync();
        }

        public async Task<List<NotificationDto>> GetNotificationsForUserAsync(string userId)
        {
            var notifications = await _notisCollection.Find(notification => notification.UserId == userId)
                                                       .SortByDescending(notification => notification.CreatedAt)
                                                       .Limit(10)
                                                       .ToListAsync();

            var notificationDtos = new List<NotificationDto>();

            foreach (var notification in notifications)
            {
                var user = await _userService.FindUserByIdAsync(notification.UserId); // Usuario creador del post
                var actor = await _userService.FindUserByIdAsync(notification.ActorId); // Actor de la notificación

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    ProfilePic = user.ProfilePic,
                    IsVerified = user.IsVerified
                };

                var actorDto = new UserDto
                {
                    Id = actor.Id,
                    Username = actor.Username,
                    ProfilePic = actor.ProfilePic,
                    IsVerified = actor.IsVerified
                };

                var notificationDto = new NotificationDto
                {
                    Id = notification.Id,
                    User = userDto,
                    PostId = notification.Type == NotificationType.Follow ? null : notification.PostId, // Es null si es un follow
                    Actor = actorDto,
                    Message = notification.Message,
                    Status = notification.Status,
                    Type = notification.Type,
                    Action = notification.Action
                };

                notificationDtos.Add(notificationDto);
            }

            return notificationDtos;
        }

        public async Task UpdateNotificationStatusToReadAsync(string notificationId)
        {
            var filter = Builders<Notifications>.Filter.Eq(notification => notification.Id, notificationId);
            var update = Builders<Notifications>.Update.Set(notification => notification.Status, NotificationStatus.Read);
            await _notisCollection.UpdateOneAsync(filter, update);
        }
    }
}
