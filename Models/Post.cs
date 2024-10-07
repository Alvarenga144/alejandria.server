using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Alejandria.Server.Models
{
    public class Post
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("postedBy")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string PostedBy { get; set; } = null!;

        [BsonElement("userAutor")]
        public string UserAutor { get; set; } = null!;

        [BsonElement("text")]
        public string Text { get; set; } = null!;

        [BsonElement("img")]
        public string Img { get; set; } = null!;

        [BsonElement("likes")]
        [BsonRepresentation(BsonType.ObjectId)]
        public List<string> Likes { get; set; } = new List<string>();

        [BsonElement("replies")]
        public List<Reply> Replies { get; set; } = new List<Reply>();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("postResume")]
        public string PostResume { get; set; } = null!;
    }

    public class Reply
    {
        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = null!;

        [BsonElement("text")]
        public string Text { get; set; } = null!;

        [BsonElement("userProfilePic")]
        public string UserProfilePic { get; set; } = null!;

        [BsonElement("username")]
        public string Username { get; set; } = null!;
    }

    public class CreatePostRequest
    {
        public string PostedBy { get; set; } = null!;

        public string Text { get; set; } = null!;

        public string Img { get; set; }
    }

    public class ReplyToPostRequest
    {
        // public string UserId { get; set; } = null!;

        public string Text { get; set; } = null!;

        // public string UserProfilePic { get; set; } = null!;

        // public string Username { get; set; } = null!;
    }
}