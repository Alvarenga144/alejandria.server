using Azure;
using Azure.AI.OpenAI;

namespace Alejandria.Server.Services
{

    public class EmbedService
    {

        private readonly string _embedingModel;
        private readonly OpenAIClient _openAIClient;

        public EmbedService(IConfiguration configuration){
            _embedingModel = configuration.GetSection("AzureOpenAI")["EmbeddingModel"]!;
            string openAIEndpoint = configuration.GetSection("AzureOpenAI")["OpenAIUrl"]!;
            string openAIKey = configuration.GetSection("AzureOpenAI")["OpenAIKey"]!;

            _openAIClient = new OpenAIClient(
                new Uri(openAIEndpoint),
                new AzureKeyCredential(openAIKey));
        }

        public Task<Response<Embeddings>> GetEmbeddingsAsync(string input){
            var options = new EmbeddingsOptions(_embedingModel, [input.Replace('\r', ' ')]);
            return _openAIClient.GetEmbeddingsAsync(options);
        }
    }

}
