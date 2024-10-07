using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure;
using Alejandria.Server.Models;
using Azure.Search.Documents.Models;
using System.Text;

namespace Alejandria.Server.Services
{
    public class VoiceChatService
    {
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient _openAIClient;
        private readonly SearchClient _searchClient;
        private readonly SearchClient _searchProduClient;
        private readonly string _openAIDeploymentModel;

        public VoiceChatService(IConfiguration configuration)
        {
            _configuration = configuration;

            // Configuración del cliente OpenAI
            string openAIEndpoint = _configuration.GetSection("AzureOpenAI")["OpenAIUrl"]!;
            string openAIKey = _configuration.GetSection("AzureOpenAI")["OpenAIKey"]!;
            _openAIDeploymentModel = _configuration.GetSection("AzureOpenAI")["OpenAIDeploymentModel"]!;

            _openAIClient = new OpenAIClient(
                new Uri(openAIEndpoint),
                new AzureKeyCredential(openAIKey));

            // Configuración del cliente de Azure AI Search
            string searchEndpoint = _configuration.GetSection("AzureAISearch")["SearchEndpoint"]!;
            string searchKey = _configuration.GetSection("AzureAISearch")["SearchKey"]!;
            string searchIndexName = _configuration.GetSection("AzureAISearch")["SearchIndexName"]!;
            string searchIndexNameProdu = _configuration.GetSection("AzureAISearch")["SearchIndexNameProdu"]!;

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
        }

        private readonly string systemMessage = @$"Te llamas Alejandría y eres un útil asistente AI.
Como asistente, saludas a las personas y sigues la conversación de manera natural, respondes a las preguntas y además consultas las publicaciones, productos y servicios en Alejandría para dar una mejor respuesta.
Has sido creada por 3 ingenieros y científicos de datos de El Salvador, y usas un modelo de inteligencia artificial personalizado.
Tus respuestas deben ser sin formato Markdown. Hoy es {DateTime.Today:dd/MMMM/yyyy HH:mm} UTC.
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
                #region Generate Search Queries with OpenAI
                var searchQueryOptions = new ChatCompletionsOptions
                {
                    Temperature = (float)0.7,
                    MaxTokens = 500,
                    NucleusSamplingFactor = (float)0.95,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    DeploymentName = _openAIDeploymentModel
                };
                searchQueryOptions.Messages.Add(new ChatMessage(ChatRole.System, searchSystemMessage));

                // Añadir el historial de mensajes
                foreach (var msg in messageHistory)
                {
                    ChatRole role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant;
                    searchQueryOptions.Messages.Add(new ChatMessage(role, msg.Content));
                }

                // Añadir la consulta actual como mensaje del usuario
                searchQueryOptions.Messages.Add(new ChatMessage(ChatRole.User, query));

                // Obtener consultas de búsqueda de OpenAI
                var searchQueryResult = await _openAIClient.GetChatCompletionsAsync(searchQueryOptions);
                var searchQueries = searchQueryResult.Value.Choices[0].Message.Content.Split('\n');
                #endregion

                #region Retrieve Documents with Azure AI Search
                var sb = new StringBuilder();
                string searchResponse = "";
                foreach (var searchQuery in searchQueries)
                {
                    var searchOptions = new SearchOptions
                    {
                        Filter = "",
                        QueryType = SearchQueryType.Semantic,
                        SemanticSearch = new()
                        {
                            SemanticConfigurationName = "alejandria-semantic-2",
                            QueryCaption = new(QueryCaptionType.Extractive),
                        },
                        Size = 5,
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
                        string text = result.Document.Text;
                        string userautor = result.Document.UserAutor.ToString();
                        string createdat = result.Document.CreatedAt.ToString();
                        // Formateo básico para Markdown
                        text = text.Replace("\r\n", "\n\n").Replace("\n", "\n\n");
                        sb.AppendLine($"Dato: {text}\nPublicado por: {userautor}\nFecha de publicación: {createdat}\n");
                    }

                    // Búsqueda en MyAzureProductDocument
                    var searchResultProductResponse = await _searchProduClient.SearchAsync<MyAzureProductDocument>(searchQuery, searchOptionsProduct, default);
                    SearchResults<MyAzureProductDocument> searchProduResult = searchResultProductResponse.Value;

                    foreach (var result in searchProduResult.GetResults())
                    {
                        string category = result.Document.Category;
                        string title = result.Document.Title;
                        string description = result.Document.DetailedDescription;
                        string price = result.Document.Price.ToString();
                        string userautor = result.Document.UserAutor.ToString();
                        string createdat = result.Document.CreatedAt.ToString();
                        // Formateo básico para Markdown
                        title = title.Replace("\r\n", "\n\n").Replace("\n", "\n\n");
                        sb.AppendLine($"{category}: Título: {title}\nDescripción: {description}\nPrecio: {price}\nPublicado por: {userautor}\nFecha de publicación: {createdat}\n");
                    }
                }
                searchResponse = sb.ToString();
                #endregion

                #region Generate Final Response with OpenAI

                var finalResponseOptions = new ChatCompletionsOptions
                {
                    Temperature = (float)1.0,
                    MaxTokens = 500,
                    NucleusSamplingFactor = (float)0.95,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    DeploymentName = _openAIDeploymentModel
                }; 

                finalResponseOptions.Messages.Add(new ChatMessage(ChatRole.System, systemMessage));

                // Añadir el historial de mensajes y la consulta actual para el contexto completo
                foreach (var msg in messageHistory)
                {
                    ChatRole role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant;
                    finalResponseOptions.Messages.Add(new ChatMessage(role, msg.Content));
                }

                // Añadir la información recuperada como contexto adicional
                finalResponseOptions.Messages.Add(new ChatMessage(ChatRole.User, @$" ## Publicaciones ##
{searchResponse}
## Fin de publicaciones ##
Hoy es {DateTime.Today:dd/MMMM/yyyy}.
Sigue la conversación naturalmente, saluda y menciona las publicaciones relacionadas solo si las hay. Si no hay publicaciones relacionadas, da tu propia respuesta.
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
                    response = "Lo siento, no puedo proporcionar una respuesta para esa pregunta.";
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
            Message responseMessage = new("assistant", response);
            return responseMessage;
        }
    }
}
