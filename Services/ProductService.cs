using Alejandria.Server.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Alejandria.Server.Services
{
    public class ProductService
    {
        private readonly IMongoCollection<Product> _productCollection;

        public ProductService(IOptions<MongoDBSettings> mongoDatabaseSettings)
        {
            var mongoClient = new MongoClient(mongoDatabaseSettings.Value.ConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseSettings.Value.DatabaseName);

            _productCollection = mongoDatabase.GetCollection<Product>(mongoDatabaseSettings.Value.ProductCollectionName);
        }

        #region Methods for Products

        // Guardar un producto
        public async Task SaveProductPostAsync(Product product) =>
            await _productCollection.InsertOneAsync(product);

        // Encontrar un producto por id
        public async Task<Product> FindProductByIdAsync(string id)
        {
            return await _productCollection.Find(product => product.Id == id).FirstOrDefaultAsync();
        }

        // Borrar un producto por id
        public async Task DeleteProductAsync(string id)
        {
            await _productCollection.DeleteOneAsync(product => product.Id == id);
        }

        // Like a product
        public async Task LikeProductAsync(string productId, string userId)
        {
            var update = Builders<Product>.Update.AddToSet(product => product.Likes, userId);
            await _productCollection.UpdateOneAsync(product => product.Id == productId, update);
        }

        // Unlike a product
        public async Task UnlikeProductAsync(string productId, string userId)
        {
            var update = Builders<Product>.Update.Pull(product => product.Likes, userId);
            await _productCollection.UpdateOneAsync(product => product.Id == productId, update);
        }

        #region FEED Products advanced (currently out of use)

        // Get all products for a feed including the user's own posts and now random post
        public async Task<List<Product>> GetFeedAdvancedProductsAsync(List<string> following, string currentUserId, int page, int pageSize)
        {
            following.Add(currentUserId);

            List<Product> followedPosts = new List<Product>();
            if (following.Count > 1)
            {
                followedPosts = await _productCollection.Find(post => following.Contains(post.PostedBy))
                                                     .SortByDescending(post => post.CreatedAt)
                                                     .ToListAsync();
            }

            int randomPostsCount = followedPosts.Any() ? (int)Math.Ceiling(followedPosts.Count * 0.7) : 40;

            var allPostUserIds = await _productCollection.Distinct<string>("PostedBy", Builders<Product>.Filter.Empty).ToListAsync();
            var notFollowedUserIds = allPostUserIds.Where(userId => !following.Contains(userId) && userId != currentUserId).ToList();

            Random rnd = new Random();
            var randomUserIds = notFollowedUserIds.OrderBy(x => rnd.Next()).Take(randomPostsCount).ToList();

            var randomPosts = new List<Product>();
            if (randomUserIds.Any())
            {
                randomPosts = await _productCollection.Find(produ => randomUserIds.Contains(produ.PostedBy))
                                                   .SortByDescending(produ => produ.CreatedAt)
                                                   .Limit(randomPostsCount)
                                                   .ToListAsync();
            }

            var combinedPosts = followedPosts.Concat(randomPosts).ToList();

            // Agrupar los posts por usuario y tomar solo los 2 más recientes de cada grupo
            combinedPosts = combinedPosts.GroupBy(post => post.PostedBy)
                                         .SelectMany(group => group.OrderByDescending(post => post.CreatedAt).Take(6))
                                         .OrderByDescending(post => post.CreatedAt)
                                         .ToList();
            // Aplicar la paginación
            var paginatedPosts = combinedPosts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return paginatedPosts;
        }

        #endregion FEED Products advanced (currently out of use)

        public async Task<List<Product>> GetFeedProductsAsync(int page, int pageSize)
        {
            // Obtener todos los productos y ordenarlos por fecha de creación
            List<Product> allProducts = await _productCollection.Find(_ => true)
                                                                .SortByDescending(product => product.CreatedAt)
                                                                .ToListAsync();

            // Reorganizar los productos sin permitir no más de dos posts del mismo usuario seguidos
            List<Product> reorderedProducts = ReorderProducts(allProducts);

            // Aplicar la paginación
            List<Product> paginatedProducts = reorderedProducts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return paginatedProducts;
        }

        private List<Product> ReorderProducts(List<Product> products)
        {
            var reordered = new List<Product>();
            var tempQueue = new Queue<Product>();

            // Contar apariciones consecutivas de cada usuario
            string lastUserId = null;
            int count = 0;

            foreach (var product in products)
            {
                if (product.UserAutor == lastUserId && count < 2)
                {
                    reordered.Add(product);
                    count++;
                }
                else if (product.UserAutor == lastUserId && count >= 2)
                {
                    tempQueue.Enqueue(product);
                }
                else
                {
                    if (tempQueue.Count > 0 && count >= 2)
                    {
                        // Intercalar tres posts de otros usuarios
                        int intersperseCount = 0;
                        while (intersperseCount < 3 && tempQueue.Count > 0)
                        {
                            reordered.Add(tempQueue.Dequeue());
                            intersperseCount++;
                        }
                    }
                    reordered.Add(product);
                    count = 1;  // Reiniciar conteo para un nuevo usuario
                }
                lastUserId = product.UserAutor;
            }

            // Añadir los productos restantes en la cola
            while (tempQueue.Count > 0)
            {
                reordered.Add(tempQueue.Dequeue());
            }

            return reordered;
        }

        // Obtener lisa de productos de un usuario especifico
        public async Task<List<Product>> GetUserProductsAsync(string userId)
        {
            return await _productCollection.Find(product => product.PostedBy == userId)
                                           .SortByDescending(product => product.CreatedAt)
                                           .ToListAsync();
        }

        // Add reply to a post
        public async Task AddReplyToProductAsync(string productId, Reply reply)
        {
            var update = Builders<Product>.Update.Push(product => product.Replies, reply);
            await _productCollection.UpdateOneAsync(product => product.Id == productId, update);
        }

        // Método para encontrar posts con respuestas de un usuario específico
        public async Task<List<Product>> FindProducsWithRepliesByUser(string userId)
        {
            var filter = Builders<Product>.Filter.ElemMatch(p => p.Replies, r => r.UserId == userId);
            return await _productCollection.Find(filter).ToListAsync();
        }

        // Método para actualizar un post tipo producto
        public async Task UpdateProductAsync(Product product)
        {
            await _productCollection.ReplaceOneAsync(p => p.Id == product.Id, product);
        }

        #endregion
    }
}
