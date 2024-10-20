using Alejandria.Server.Models;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.Text;

namespace Alejandria.Server.Services
{
    public class ChatService
    {
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient _openAIClient;
        private readonly EmbedService _embedService;
        private readonly SearchClient _searchClient;
        private readonly SearchClient _searchProduClient;
        private readonly SearchClient _searchLawsClient;
        private readonly string _openAIDeploymentModel;


        public ChatService(IConfiguration configuration, EmbedService embedService)
        {
            _configuration = configuration;

            // Configuración del cliente OpenAI
            string openAIEndpoint = _configuration.GetSection("AzureOpenAI")["OpenAIUrl"]!;
            string openAIKey = _configuration.GetSection("AzureOpenAI")["OpenAIKey"]!;
            _openAIDeploymentModel = _configuration.GetSection("AzureOpenAI")["OpenAIDeploymentModel"]!;

            _openAIClient = new OpenAIClient(
                new Uri(openAIEndpoint),
                new AzureKeyCredential(openAIKey));

            _embedService = embedService;

            // Configuración del cliente de Azure AI Search
            string searchEndpoint = _configuration.GetSection("AzureAISearch")["SearchEndpoint"]!;
            string searchKey = _configuration.GetSection("AzureAISearch")["SearchKey"]!;
            string searchIndexName = _configuration.GetSection("AzureAISearch")["SearchIndexName"]!;
            string searchIndexNameProdu = _configuration.GetSection("AzureAISearch")["SearchIndexNameProdu"]!;
            string searchIndexNameLaws = _configuration.GetSection("AzureAISearch")["SearchIndexNameLaws"]!;

            // Search in normal index
            _searchClient = new SearchClient(
                new Uri(searchEndpoint),
                searchIndexName,
                new AzureKeyCredential(searchKey));

            // search in products index
            _searchProduClient = new SearchClient(
                new Uri(searchEndpoint),
                searchIndexNameProdu,
                new AzureKeyCredential(searchKey));

            // search in products index
            _searchLawsClient = new SearchClient(
                new Uri(searchEndpoint),
                searchIndexNameLaws,
                new AzureKeyCredential(searchKey));
        }

        private readonly string systemMessage = @$"Eres un útil asistente de AI dentro de Alejandría, una plataforma donde los usuarios realizan públicaciones para alimentar una inteligencia artificial.
Como asistente, saludas a las personas, sigues la conversación de manera natural y respondes a las preguntas.
Se te proporcionarán las publicaciones, productos y servicios en Alejandría para que des una mejor respuesta.
En tus respuestas menciona las publicaciones, productos o leyes relacionadas solo si las hay, y siempre agrega las referencias a esas publicaciones relacionadas como notas al pie. Si no hay publicaciones relacionadas, da tu propia respuesta.
El formato de las referencias debe ser la forma [^n]
Has sido creada por 3 ingenieros y científicos de datos de El Salvador, y usas modelo de inteligencia artificial personalizado.
Tus respuestas deben ser en formato Markdown. Hoy es {DateTime.UtcNow:dd/MMMM/yyyy HH:mm} UTC.
";

        private readonly string searchSystemMessage = @"Eres un útil asistente de AI dentro de Alejandría, una plataforma donde los usuarios realizan públicaciones para alimentar una inteligencia artificial.
Generas una consulta de búsqueda para preguntas de seguimiento acerca de publicaciones en Alejandría.
Esta consulta sirve para buscar publicaciones relacionadas, por lo que debe ser simple, precisa y sin formato. También debe e incluir los mismos nombres o palabras claves con tilde y sin tilde, en singular y en plural, separados por coma.
Devuelve únicamente la consulta, no devuelvas ningún otro texto.
";

        public async Task<Message> GetResponse(List<Message> messageHistory, string query)
        {
            string response;
            try
            {
                #region Generate Search Query with OpenAI
                var searchQueryOptions = new ChatCompletionsOptions
                {
                    Temperature = (float)0.7,
                    MaxTokens = 200,
                    NucleusSamplingFactor = (float)0.95,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    DeploymentName = _openAIDeploymentModel
                };
                searchQueryOptions.Messages.Add(new ChatMessage(ChatRole.System, searchSystemMessage));

                // Añadir el historial de mensajes
                foreach (var msg in messageHistory.Skip(Math.Max(0, messageHistory.Count - 6)))
                {
                    ChatRole role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant;
                    searchQueryOptions.Messages.Add(new ChatMessage(role, msg.Content));
                }

                // Añadir la consulta actual como mensaje del usuario
                searchQueryOptions.Messages.Add(new ChatMessage(ChatRole.User, query));

                // Obtener consultas de búsqueda de OpenAI
                var searchQueryResult = await _openAIClient.GetChatCompletionsAsync(searchQueryOptions);
                var searchQuery = searchQueryResult.Value.Choices[0].Message.Content.Split('\n')[0];
                #endregion

                #region Retrieve Documents with Azure AI Search
                var sb = new StringBuilder();
                var sbProducts = new StringBuilder();
                var sbLaws = new StringBuilder();

                var searchOptions = new SearchOptions
                {
                    Filter = "",
                    QueryType = SearchQueryType.Semantic,
                    SemanticSearch = new()
                    {
                        SemanticConfigurationName = "alejandria-semantic-2",
                        QueryCaption = new(QueryCaptionType.Extractive),
                    },
                    /*
                    QueryLanguage = "es-ES",
                    QuerySpeller = "lexicon",
                    */
                    Size = 8,
                };

                var searchOptionsProduct = new SearchOptions
                {
                    Filter = "",
                    QueryType = SearchQueryType.Semantic,
                    SemanticSearch = new()
                    {
                        SemanticConfigurationName = "alejandria-semantic-product",
                        QueryCaption = new(QueryCaptionType.Extractive),
                    },
                    Size = 5,
                };

                // Búsqueda en MyAzureSearchDocument
                var searchResultResponse = await _searchClient.SearchAsync<MyAzureSearchDocument>(searchQuery, searchOptions, default);
                SearchResults<MyAzureSearchDocument> searchResult = searchResultResponse.Value;

                foreach (var result in searchResult.GetResults())
                {
                    string docId = result.Document.DocId;
                    string text = result.Document.Text;
                    string userautor = result.Document.UserAutor.ToString();
                    string createdat = result.Document.CreatedAt.ToString();
                    // Formateo básico para Markdown
                    text = text.Replace("\r\n", "\n\n").Replace("\n", "\n\n");
                    sb.AppendLine(@$"
**[Publicación de {userautor}](/{userautor}/Post/{docId})**
{text}
Publicado por: [**{userautor}**](/{userautor})
Fecha de publicación: {createdat}
");
                }

                // Búsqueda en MyAzureProductDocument
                var searchResultProductResponse = await _searchProduClient.SearchAsync<MyAzureProductDocument>(searchQuery, searchOptionsProduct, default);
                SearchResults<MyAzureProductDocument> searchProduResult = searchResultProductResponse.Value;

                foreach (var result in searchProduResult.GetResults())
                {
                    string docId = result.Document.DocId;
                    string category = result.Document.Category;
                    string title = result.Document.Title;
                    string description = result.Document.DetailedDescription;
                    string price = result.Document.Price.ToString();
                    string userautor = result.Document.UserAutor.ToString();
                    string createdat = result.Document.CreatedAt.ToString();
                    // Formateo básico para Markdown
                    title = title.Replace("\r\n", "\n\n").Replace("\n", "\n\n");
                    sbProducts.AppendLine(@$"
{category}: [{title}](/{userautor}/Product/{docId})
Descripción: {description}
Precio: *{price}*
Publicado por: [**{userautor}**](/{userautor})
Fecha de publicación: *{createdat}*
");
                }


                // Opciones de búsqueda de leyes
                var embeddingsResponse = await _embedService.GetEmbeddingsAsync(query);
                float[]? embeddings = embeddingsResponse.Value.Data.FirstOrDefault()?.Embedding.ToArray();

                var searchOptionsLaws = new SearchOptions
                {
                    Filter = "",
                    QueryType = SearchQueryType.Semantic,
                    SemanticSearch = new()
                    {
                        SemanticConfigurationName = "vectorleyes-1712976507903-semantic-configuration",
                        QueryCaption = new(QueryCaptionType.Extractive),
                    },
                    VectorSearch = new VectorSearchOptions(

                    ),
                    Size = 3,
                };

                var vectorQuery = new VectorizedQuery(embeddings)
                {
                    // if semantic ranker is enabled, we need to set the rank to a large number to get more
                    // candidates for semantic reranking
                    KNearestNeighborsCount = 50,
                };
                vectorQuery.Fields.Add("vector");
                searchOptionsLaws.VectorSearch = new();
                searchOptionsLaws.VectorSearch.Queries.Add(vectorQuery);

                // Búsqueda de leyes
                var searchLawsResult = await _searchLawsClient.SearchAsync<MyAzureLawDocument>(searchQuery, searchOptionsLaws, default);

                foreach (var result in searchLawsResult.Value.GetResults())
                {
                    string title = result.Document.Title;
                    string chunk = result.Document.Chunk;
                    string chunkId = result.Document.ChunkId;
                    string parentId = result.Document.ParentId;
                    sbLaws.AppendLine(@$"## {title} ##
{chunk}");
                }

                #endregion

                #region Generate Final Response with OpenAI

                var finalResponseOptions = new ChatCompletionsOptions
                {
                    Temperature = (float)0.7,
                    MaxTokens = 800,
                    NucleusSamplingFactor = (float)0.95,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    DeploymentName = _openAIDeploymentModel
                };

                finalResponseOptions.Messages.Add(new ChatMessage(ChatRole.System, systemMessage));

                // Añadir el historial de mensajes y la consulta actual para el contexto completo
                foreach (var msg in messageHistory.Skip(Math.Max(0, messageHistory.Count - 6)))
                {
                    ChatRole role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant;
                    finalResponseOptions.Messages.Add(new ChatMessage(role, msg.Content));
                }

                // Añadir la información recuperada como contexto adicional
                finalResponseOptions.Messages.Add(new ChatMessage(ChatRole.User, @$"
# Publicaciones en Alejandría #
{sb}
# Productos o servicios publicados en Alejandría #
{sbProducts}
# Leyes Salvadoreñas #
{sbLaws}
# Fin de leyes #
Hoy es {DateTime.UtcNow:dd/MMMM/yyyy HH:mm} UTC.
"));

                finalResponseOptions.Messages.Add(new ChatMessage(ChatRole.User, query));

                var finalResponseResult = await _openAIClient.GetChatCompletionsAsync(finalResponseOptions);
                response = finalResponseResult.Value.Choices[0].Message.Content;
                #endregion
            }
            catch (RequestFailedException e)
            {
                if (e.Status == 400)
                {
                    if (e.ErrorCode == "context_length_exceeded")
                    {
                        response = "Se ha excedido el límite de mensajes. Intenta borrar el historial de mensajes y haz tu consulta de nuevo.";
                    }
                    else
                    {
                        response = "Lo siento, no puedo proporcionar una respuesta para esa pregunta.";
                    }
                }
                else
                {
                    response = "Parece que hay un problema. Intenta preguntar de otra manera o vuelve a intentarlo más tarde.";
                }
                ErrorEmailService.Send(e, query);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                response = "Parece que hay un problema. Intenta preguntar de otra manera o vuelve a intentarlo más tarde.";
                ErrorEmailService.Send(e, query);
            }
            return new Message("assistant", response);
        }
    }
}
