using Alejandria.Server.Models;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

namespace Alejandria.Server.Services
{
    public class DocumentService
    {
        private readonly IConfiguration _configuration;
        private readonly SearchClient _searchClient;

        public DocumentService(IConfiguration configuration)
        {
            _configuration = configuration;

            // Configuración del cliente de Azure AI Search
            string searchEndpoint = _configuration.GetSection("AzureAISearch")["SearchEndpoint"]!;
            string searchKey = _configuration.GetSection("AzureAISearch")["SearchKey"]!;
            string searchIndexName = _configuration.GetSection("AzureAISearch")["SearchIndexName"]!;

            _searchClient = new SearchClient(
                new Uri(searchEndpoint),
                searchIndexName,
                new AzureKeyCredential(searchKey));
        }

        public async Task RemoveDocumentFromIndex(string docID)
        {
            //Remove a documents from index
            var responseOptions = new IndexDocumentsOptions
            {
                ThrowOnAnyError = true,
            };

            await _searchClient.DeleteDocumentsAsync("doc_id", [docID], responseOptions);
        }
    }
}
