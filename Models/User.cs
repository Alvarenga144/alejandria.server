using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Alejandria.Server.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("name")]
        public string Name { get; set; } = null!;

        [BsonElement("username")]
        public string Username { get; set; } = null!;

        [BsonElement("email")]
        public string Email { get; set; } = null!;

        [BsonElement("emailConfirmed")]
        public bool EmailConfirmed { get; set; }

        [BsonElement("password")]
        public string Password { get; set; } = null!;

        [BsonElement("confirmPassword")]
        public string ConfirmPassword { get; set; }

        [BsonElement("restorePassword")]
        public bool RestorePassword { get; set; } = false;

        [BsonElement("categoria")]
        public string UserType { get; set; } = null!;

        [BsonElement("profilePic")]
        public string ProfilePic { get; set; } = null!;

        [BsonElement("followers")]
        public List<string> Followers { get; set; } = new List<string>();

        [BsonElement("following")]
        public List<string> Following { get; set; } = new List<string>();

        [BsonElement("biografia")]
        public string Bio { get; set; } = null!;

        [BsonElement("isVerified")]
        public bool IsVerified { get; set; }

        [BsonElement("verificationToken")]
        public string VerificationToken { get; set; } = null!;

        [BsonElement("isFrozen")]
        public bool IsFrozen { get; set; }

        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updatedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAt { get; set; }
    }

    public class SignupRequest
    {
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string UserType { get; set; } = null!;
    }

    public class LoginUserRequest
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class UpdatePasswordTkn
    {
        public string VerificationToken { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }


    public class UpdateUserRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Bio { get; set; }
        public string ProfilePic { get; set; }
        public string UserType { get; set; }
    }

    public class ResetPasswordModel
    {
        public string Email { get; set; }
    }

}
