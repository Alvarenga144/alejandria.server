using Alejandria.Server.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Alejandria.Server.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _userCollection;

        public UserService(IOptions<MongoDBSettings> mongoDatabaseSettings)
        {
            var mongoClient = new MongoClient(mongoDatabaseSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseSettings.Value.DatabaseName);
            _userCollection = mongoDatabase.GetCollection<User>(mongoDatabaseSettings.Value.UserCollectionName);
        }

        #region Methods for User

        #region IMPORTANT!!! UPDATE OLD USER'S DOCUMENTS TO NEW STRUCTURE WITH EMAILCONFIRMED, VERIFICATION TOKEN AND OTHERS

        public async Task<User> UpdateUserFieldsAsync(string userId, User updatedUser)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update
                .Set(u => u.EmailConfirmed, updatedUser.EmailConfirmed)
                .Set(u => u.RestorePassword, updatedUser.RestorePassword)
                .Set(u => u.VerificationToken, updatedUser.VerificationToken)
                .Set(u => u.ConfirmPassword, updatedUser.ConfirmPassword)
                .CurrentDate(u => u.UpdatedAt);

            var options = new FindOneAndUpdateOptions<User>
            {
                ReturnDocument = ReturnDocument.After
            };

            var result = await _userCollection.FindOneAndUpdateAsync(filter, update, options);
            return result;
        }

        #endregion

        #region Create User, Get User by Email or Username, Get User by username and get by id

        // Create a new user in the database and return it
        public async Task<User> CreateUserAsync(User newUser)
        {
            newUser.CreatedAt = DateTime.UtcNow;
            newUser.UpdatedAt = DateTime.UtcNow;
            await _userCollection.InsertOneAsync(newUser);
            return newUser;
        }

        // Verify if the user exists in the database with the given email or username and return it
        public async Task<User?> GetUserByEmailOrUsernameAsync(LoginUserRequest loginRequest) =>
            await _userCollection.Find(user => user.Email == loginRequest.Username || user.Username == loginRequest.Username).FirstOrDefaultAsync();

        // Get an user by its Username and Password (for login)
        public async Task<User> GetUserByUsernameLog(LoginUserRequest loginRequest) =>
            await _userCollection.Find(user => user.Username == loginRequest.Username).FirstOrDefaultAsync();

        // Get an user by its username for search withouth authentication
        public async Task<User?> GetUserByUsernameAsync(string username) =>
            await _userCollection.Find(user => user.Username == username).FirstOrDefaultAsync();

        public async Task<User?> GetUserByEmailAsync(string email) =>
            await _userCollection.Find(user => user.Email == email).FirstOrDefaultAsync();

        public async Task<User?> FindUserByIdAsync(string id) =>
            await _userCollection.Find(user => user.Id == id).FirstOrDefaultAsync();

        // Get user by verification token
        public async Task<User?> FindUserByVTokenAsync(string verificationToken)
        {
            return await _userCollection
                         .Find(user => user.VerificationToken == verificationToken)
                         .FirstOrDefaultAsync();
        }

        #endregion Create User, Get User by Email or Username, Get User by username and get by id

        #region Follow/Unfollow

        public async Task<bool> FollowUserAsync(string currentUserId, string targetUserId)
        {
            // Actualizar el usuario objetivo: Añadir currentUserId a su lista de seguidores
            var updateDefinitionTarget = Builders<User>.Update.Push(u => u.Followers, currentUserId);
            var updateResultTarget = await _userCollection.UpdateOneAsync(u => u.Id == targetUserId, updateDefinitionTarget);

            // Actualizar el usuario actual: Añadir targetUserId a su lista de seguimiento
            var updateDefinitionCurrent = Builders<User>.Update.Push(u => u.Following, targetUserId);
            var updateResultCurrent = await _userCollection.UpdateOneAsync(u => u.Id == currentUserId, updateDefinitionCurrent);

            return updateResultTarget.IsAcknowledged && updateResultCurrent.IsAcknowledged;
        }

        public async Task<bool> UnfollowUserAsync(string currentUserId, string targetUserId)
        {
            // Eliminar currentUserId de la lista de seguidores (Followers) del usuario objetivo
            var updateDefinitionTarget = Builders<User>.Update.Pull(u => u.Followers, currentUserId);
            var updateResultTarget = await _userCollection.UpdateOneAsync(u => u.Id == targetUserId, updateDefinitionTarget);

            // Eliminar targetUserId de la lista de seguimiento (Following) del usuario actual
            var updateDefinitionCurrent = Builders<User>.Update.Pull(u => u.Following, targetUserId);
            var updateResultCurrent = await _userCollection.UpdateOneAsync(u => u.Id == currentUserId, updateDefinitionCurrent);

            return updateResultTarget.IsAcknowledged && updateResultCurrent.IsAcknowledged;
        }

        #endregion Follow/Unfollow

        #region Update User

        public async Task UpdateUserAsync(User user)
        {
            var updateDefinition = Builders<User>.Update
                .Set(u => u.Name, user.Name)
                .Set(u => u.Email, user.Email)
                .Set(u => u.Username, user.Username)
                .Set(u => u.Bio, user.Bio)
                .Set(u => u.UserType, user.UserType)
                .Set(u => u.ProfilePic, user.ProfilePic)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            // If password has been changed (not null), update it too
            if (!string.IsNullOrEmpty(user.Password))
            {
                updateDefinition = updateDefinition.Set(u => u.Password, user.Password);
            }

            await _userCollection.UpdateOneAsync(u => u.Id == user.Id, updateDefinition);
        }

        #endregion Update User

        #region Update restore password to true or false

        public async Task UpdateUserRestorePasswordAsync(string userId, bool restorePassword, bool emailConfirmed, string verificationToken)
        {
            var updateDefinition = Builders<User>.Update
                .Set(u => u.EmailConfirmed, emailConfirmed)
                .Set(u => u.RestorePassword, restorePassword)
                .Set(u => u.ConfirmPassword, null)
                .Set(u => u.VerificationToken, verificationToken)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            await _userCollection.UpdateOneAsync(u => u.Id == userId, updateDefinition);
        }

        #endregion

        #region Suggested Users List

        public async Task<List<User>> GetSuggestedUsers(string currentUserId)
        {
            // Obtener el usuario actual y sus seguidos
            var currentUser = await _userCollection.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
            var usersFollowingByCurrentUser = currentUser?.Following ?? new List<string>();

            // Excluir el usuario actual de los resultados y los que ya está siguiendo
            var filter = Builders<User>.Filter.Ne(u => u.Id, currentUserId) &
                         Builders<User>.Filter.Nin(u => u.Id, usersFollowingByCurrentUser);

            // Obtener usuarios sugeridos (limitar el número si es necesario, aquí se limita a 10)
            var suggestedUsers = await _userCollection.Find(filter).Limit(4).ToListAsync();
            return suggestedUsers;
        }

        #endregion

        #region Users Search

        public async Task<IEnumerable<User>> SearchUsersByTermAsync(string term)
        {
            var filter = Builders<User>.Filter.Regex("username", new BsonRegularExpression(term, "i")) |
                         Builders<User>.Filter.Regex("name", new BsonRegularExpression(term, "i"));

            var users = await _userCollection.Find(filter).Limit(20).ToListAsync();
            return users;
        }

        #endregion Users Search

        #region Update User Password

        // Update an existing user's password
        public async Task<bool> UpdateUserPasswordAsync(string userId, string hashedPassword)
        {
            var filter = Builders<User>.Filter.Eq(user => user.Id, userId);
            var update = Builders<User>.Update
                            .Set(user => user.Password, hashedPassword)
                            .Set(user => user.UpdatedAt, DateTime.UtcNow);

            var result = await _userCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount == 1;
        }

        #endregion

        #region Confirm email's account

        // Método para confirmar el correo electrónico
        public async Task<bool> ConfirmEmailAsync(string token)
        {
            var user = await _userCollection.Find(u => u.VerificationToken == token).FirstOrDefaultAsync();

            if (user != null && !user.EmailConfirmed)
            {
                var update = Builders<User>.Update.Set(u => u.EmailConfirmed, true);
                await _userCollection.UpdateOneAsync(u => u.Id == user.Id, update);
                return true;
            }
            return false;
        }

        #endregion

        #endregion Methods for User
    }
}
