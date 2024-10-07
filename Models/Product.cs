using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Alejandria.Server.Models
{
    public class Product
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("postedBy")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string PostedBy { get; set; } = null!;

        [BsonElement("userAutor")]
        public string UserAutor { get; set; } = null!;

        [BsonElement("category")]
        public string Category { get; set; } = null!;

        [BsonElement("title")]
        public string Title { get; set; } = null!;

        [BsonElement("detailedDescription")]
        public string DetailedDescription { get; set; } = null!;

        [BsonElement("price")]
        public double Price { get; set; }

        [BsonElement("imgs")]
        public List<string> Imgs { get; set; } = new List<string>();

        [BsonElement("available")]
        public bool Available { get; set; } = true;

        [BsonElement("rating")]
        public double Rating { get; set; }

        [BsonElement("supportsInAppPayment")]
        public bool SupportsInAppPayment { get; set; } = false;

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
        public string ProductResume { get; set; } = null!;
    }

    public class CreateProductRequest
    {
        public string PostedBy { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string DetailedDescription { get; set; } = null!;
        public double Price { get; set; }
        public List<string> Imgs { get; set; } = new List<string>();
    }

    public class ReplyToProductRequest
    {
        public string Text { get; set; } = null!;
    }
}
