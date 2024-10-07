using Azure.Storage.Blobs;
using System.Text.RegularExpressions;

namespace Alejandria.Server.Services
{
    public class PostContentBlobConfiguration : IPostContentBlobConfiguration
    {
        private readonly BlobServiceClient _blobClient;

        public PostContentBlobConfiguration(IConfiguration config)
        {
            string keys = config["BlobStorage:ConnectionString"];
            _blobClient = new BlobServiceClient(keys);
        }

        public BlobContainerClient GetContainer(string containerName)
        {
            BlobContainerClient container = _blobClient.GetBlobContainerClient(containerName);
            return container;
        }

        public async Task<string> UploadBlobAsync(string base64Image, string containerName)
        {
            // Eliminar el prefijo y extraer la extensión del archivo
            string imageData = base64Image.Substring(base64Image.IndexOf(',') + 1);
            string extension = ExtractExtension(base64Image);

            byte[] imageBytes = Convert.FromBase64String(imageData);
            using var stream = new MemoryStream(imageBytes);

            // Generar un nombre único para el archivo
            string fileName = Guid.NewGuid().ToString() + extension;

            BlobContainerClient container = GetContainer(containerName);
            BlobClient blobClient = container.GetBlobClient(fileName);

            // Subir el archivo
            await blobClient.UploadAsync(stream);

            // Retornar la URL de la imagen subida
            return blobClient.Uri.AbsoluteUri;
        }

        private string ExtractExtension(string base64Image)
        {
            var match = Regex.Match(base64Image, @"data:image/(?<type>.+?);base64,");
            if (match.Success)
            {
                string extension = match.Groups["type"].Value;
                switch (extension.ToLower())
                {
                    case "jpeg":
                    case "jpg":
                        return ".jpg";
                    case "png":
                        return ".png";
                    // Añadir más casos si es necesario
                    default:
                        throw new ArgumentException("Formato de imagen no soportado.");
                }
            }

            throw new ArgumentException("No se puede extraer la extensión de la imagen.");
        }

        public bool DeleteBlob(string blobUrl, string containerName)
        {
            try
            {
                // Extraer el nombre del blob de la URL
                string blobName = ExtractBlobNameFromUrl(blobUrl);

                BlobContainerClient container = GetContainer(containerName);
                BlobClient blobClient = container.GetBlobClient(blobName);

                var response = blobClient.DeleteIfExists();
                return response.Value; // Retorna 'true' si el blob fue eliminado con éxito, 'false' si no existía.
            }
            catch (Exception ex)
            {
                // Aquí podría registrar el error con algún sistema de logging
                // Por ejemplo: _logger.LogError(ex, "Error al eliminar el blob: {BlobName}", blobName);
                return false;
            }
        }

        private string ExtractBlobNameFromUrl(string blobUrl)
        {
            var uri = new Uri(blobUrl);
            string blobName = uri.Segments.Last(); // Obtener la última parte de la URL, que es el nombre del blob
            return blobName;
        }
    }
}
