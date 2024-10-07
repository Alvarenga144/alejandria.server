using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Alejandria.Server.Models
{
    public class Notifications
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = null!;

        [BsonElement("postId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string PostId { get; set; }

        [BsonElement("actorId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ActorId { get; set; } = null!;

        [BsonElement("message")]
        public string Message { get; set; } = null!;

        [BsonElement("status")]
        public NotificationStatus Status { get; set; } = NotificationStatus.Unread;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("type")]
        public NotificationType Type { get; set; }

        [BsonElement("action")]
        public NotificationAction Action { get; set; }
    }

    public enum NotificationStatus
    {
        Unread,
        Read
    }

    public enum NotificationType
    {
        Like,
        Comment,
        Follow,
        News,
        Mention
    }

    public enum NotificationAction
    {
        Liked,
        Commented,
        Followed,
        Mentioned
    }

    public class NotificationDto
    {
        public string Id { get; set; }
        public UserDto User { get; set; } // Datos del usuario creador del post, o del usuario seguido
        public string? PostId { get; set; } // Puede ser null para notificaciones que no están relacionadas con un post
        public UserDto Actor { get; set; } // Datos del actor de la notificación
        public string Message { get; set; }
        public NotificationStatus Status { get; set; }
        public NotificationType Type { get; set; }
        public NotificationAction Action { get; set; }
    }

    public class UserDto
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string ProfilePic { get; set; }
        public bool IsVerified { get; set; }
        // Otras propiedades relevantes del usuario
    }

}

/*
 *  COMO SE PODRIA MANEJAR EN EL FRONT

const notificationTypeMap = {
    'Like': 'Te gustó tu publicación',
    'Comment': 'Comentó en tu publicación',
    'Follow': 'Te siguió',
    'News': 'Novedad'
};

const notificationStatusMap = {
    'Unread': 'No leído',
    'Read': 'Leído'
};

 */