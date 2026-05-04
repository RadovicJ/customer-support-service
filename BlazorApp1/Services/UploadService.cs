namespace BlazorApp1.Services
{
    using Azure.Storage.Blobs;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Services;
    using Azure.Search.Documents.Models;
    using Azure.Search.Documents;

    public class UploadService
    {
        private readonly string _storageConnectionString;
        private readonly string _containerName;

        public UploadService(IConfiguration configuration)
        {
            _storageConnectionString = configuration["AzureBlobStorage:ConnectionString"];
            _containerName = configuration["AzureBlobStorage:ContainerName"];
        }

        public async void UploadFileAsync(Stream fileStream, string fileName, SearchService searchService)
        {
            var blobServiceClient = new BlobServiceClient(_storageConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(fileStream, true);

            await searchService.searchIndexerClient.RunIndexerAsync("indexer1705615266916");

            await Task.Delay(TimeSpan.FromSeconds(5));

            SearchResults<SearchDocument> searchResults;
            SearchResult<SearchDocument> result;

            do
            {
                searchResults = await searchService.searchClient.SearchAsync<SearchDocument>("");
                result = searchResults.GetResults().Last();

                if (result.Document["title"] != null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

            } while (result.Document["title"] != null);

            var id = result.Document["id"].ToString();
            var title = fileName.Replace(".pdf", "");

            await searchService.UploadDocumentAsync(id, fileName, blobClient.Uri.ToString());
        }
    }
}
