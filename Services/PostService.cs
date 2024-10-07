using Alejandria.Server.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Alejandria.Server.Services
{
    public class PostService
    {
        private readonly IMongoCollection<Post> _postCollection;
        private readonly IMongoCollection<User> _userCollection;

        public PostService(IOptions<MongoDBSettings> mongoDatabaseSettings)
        {
            var mongoClient = new MongoClient(mongoDatabaseSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseSettings.Value.DatabaseName);

            _postCollection = mongoDatabase.GetCollection<Post>(mongoDatabaseSettings.Value.PostCollectionName);
            _userCollection = mongoDatabase.GetCollection<User>(mongoDatabaseSettings.Value.UserCollectionName);
        }

        #region Methods for Post

        // Save a post
        public async Task SavePostAsync(Post post) =>
            await _postCollection.InsertOneAsync(post);

        // Find a post by id
        public async Task<Post> FindPostByIdAsync(string id)
        {
            return await _postCollection.Find(post => post.Id == id).FirstOrDefaultAsync();
        }

        // Delete a post by id
        public async Task DeletePostAsync(string id)
        {
            await _postCollection.DeleteOneAsync(post => post.Id == id);
        }

        // Like a post
        public async Task LikePostAsync(string postId, string userId)
        {
            var update = Builders<Post>.Update.AddToSet(post => post.Likes, userId);
            await _postCollection.UpdateOneAsync(post => post.Id == postId, update);
        }

        // Unlike a post
        public async Task UnlikePostAsync(string postId, string userId)
        {
            var update = Builders<Post>.Update.Pull(post => post.Likes, userId);
            await _postCollection.UpdateOneAsync(post => post.Id == postId, update);
        }

        // Add reply to a post
        public async Task AddReplyToPostAsync(string postId, Reply reply)
        {
            var update = Builders<Post>.Update.Push(post => post.Replies, reply);
            await _postCollection.UpdateOneAsync(post => post.Id == postId, update);
        }

        #region FEED

        // Get all posts for a feed including the user's own posts and now random post
        public async Task<List<Post>> GetFeedPostsAsync(List<string> following, string currentUserId, int page, int pageSize)
        {
            following.Add(currentUserId);

            List<Post> followedPosts = new List<Post>();
            if (following.Count > 1)
            {
                followedPosts = await _postCollection.Find(post => following.Contains(post.PostedBy))
                                                     .SortByDescending(post => post.CreatedAt)
                                                     .ToListAsync();
            }

            int randomPostsCount = followedPosts.Any() ? (int)Math.Ceiling(followedPosts.Count * 0.7) : 40;

            var allPostUserIds = await _postCollection.Distinct<string>("PostedBy", Builders<Post>.Filter.Empty).ToListAsync();
            var notFollowedUserIds = allPostUserIds.Where(userId => !following.Contains(userId) && userId != currentUserId).ToList();

            Random rnd = new Random();
            var randomUserIds = notFollowedUserIds.OrderBy(x => rnd.Next()).Take(randomPostsCount).ToList();

            var randomPosts = new List<Post>();
            if (randomUserIds.Any())
            {
                randomPosts = await _postCollection.Find(post => randomUserIds.Contains(post.PostedBy))
                                                   .SortByDescending(post => post.CreatedAt)
                                                   .Limit(randomPostsCount)
                                                   .ToListAsync();
            }

            var combinedPosts = followedPosts.Concat(randomPosts).ToList();

            // Agrupar los posts por usuario y tomar solo los 2 más recientes de cada grupo
            combinedPosts = combinedPosts.GroupBy(post => post.PostedBy)
                                         .SelectMany(group => group.OrderByDescending(post => post.CreatedAt).Take(2))
                                         .OrderByDescending(post => post.CreatedAt)
                                         .ToList();
            // Aplicar la paginación
            var paginatedPosts = combinedPosts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return paginatedPosts;
        }

        #endregion FEED

        // Get all posts for a profile
        public async Task<List<Post>> GetUserPostsAsync(string userId)
        {
            return await _postCollection.Find(post => post.PostedBy == userId)
                                        .SortByDescending(post => post.CreatedAt)
                                        .ToListAsync();
        }

        // Método para encontrar posts con respuestas de un usuario específico
        public async Task<List<Post>> FindPostsWithRepliesByUser(string userId)
        {
            var filter = Builders<Post>.Filter.ElemMatch(p => p.Replies, r => r.UserId == userId);
            return await _postCollection.Find(filter).ToListAsync();
        }

        // Método para actualizar un post
        public async Task UpdatePostAsync(Post post)
        {
            await _postCollection.ReplaceOneAsync(p => p.Id == post.Id, post);
        }

        #endregion Methods for Post
    }
}