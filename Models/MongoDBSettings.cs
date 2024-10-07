namespace Alejandria.Server.Models
{
    public class MongoDBSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string UserCollectionName { get; set; } = null!;
        public string PostCollectionName { get; set; } = null!;
        public string NotiCollectionName { get; set; } = null!;
        public string ProductCollectionName { get; set; } = null!;
    }
}
