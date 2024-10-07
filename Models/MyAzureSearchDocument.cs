using Azure.Search.Documents.Indexes;
using System.Text.Json.Serialization;

namespace Alejandria.Server.Models
{
    public class MyAzureSearchDocument
    {
        [JsonPropertyName("doc_id")]
        [SimpleField(IsKey = true, IsFilterable = false, IsSortable = false)]
        public string DocId { get; set; }

        [JsonPropertyName("postedBy")]
        [SimpleField(IsFilterable = false, IsSortable = false)]
        public string PostedBy { get; set; }

        [JsonPropertyName("userAutor")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string UserAutor { get; set; }

        [JsonPropertyName("text")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Text { get; set; }

        [JsonPropertyName("img")]
        [SimpleField(IsFilterable = false, IsSortable = true)]
        public string Img { get; set; }

        [JsonPropertyName("createdAt")]
        [SimpleField(IsFilterable = true, IsSortable = true)]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("postResume")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string PostResume { get; set; }
    }

    public class MyAzureProductDocument
    {
        [JsonPropertyName("doc_id")]
        [SimpleField(IsKey = true, IsFilterable = false, IsSortable = false)]
        public string DocId { get; set; }

        [JsonPropertyName("postedBy")]
        [SimpleField(IsFilterable = false, IsSortable = false)]
        public string PostedBy { get; set; }

        [JsonPropertyName("userAutor")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string UserAutor { get; set; }

        [JsonPropertyName("category")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Category { get; set; }

        [JsonPropertyName("title")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Title { get; set; }

        [JsonPropertyName("detailedDescription")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string DetailedDescription { get; set; }

        [JsonPropertyName("price")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public Double Price { get; set; }

        [JsonPropertyName("available")]
        [SimpleField(IsFilterable = true, IsSortable = true)]
        public bool Available { get; set; }

        [JsonPropertyName("rating")]
        [SimpleField(IsFilterable = false, IsSortable = true)]
        public Int64 Rating { get; set; }

        [JsonPropertyName("SupportsInAppPayment")]
        [SimpleField(IsFilterable = false, IsSortable = true)]
        public bool SupportsInAppPayment { get; set; } = false;

        [JsonPropertyName("createdAt")]
        [SimpleField(IsFilterable = true, IsSortable = true)]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("postResume")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string PostResume { get; set; }
    }

    public class MyAzureLawDocument
    {
        [JsonPropertyName("chunk_id")]
        [SimpleField(IsKey = true, IsFilterable = true, IsSortable = true)]
        public string ChunkId { get; set; }

        [JsonPropertyName("parent_id")]
        [SimpleField(IsFilterable = true, IsSortable = true)]
        public string ParentId { get; set; }

        [JsonPropertyName("chunk")]
        [SearchableField(IsFilterable = false, IsSortable = false)]
        public string Chunk { get; set; }

        [JsonPropertyName("title")]
        [SearchableField(IsFilterable = true, IsSortable = false)]
        public string Title { get; set; }
    }
}
