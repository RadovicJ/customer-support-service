namespace BlazorApp1.Services
{
    using Azure;
    using Azure.Search.Documents;
    using Azure.Search.Documents.Indexes;
    using Azure.Search.Documents.Models;
    using BlazorApp1.DTO;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Radzen;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Reflection.Metadata;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    public class SearchService
    {
        public readonly SearchClient searchClient;
        public SearchIndexerClient searchIndexerClient;
        List<DocumentDTO> documents = new List<DocumentDTO>();

        public SearchService()
        {
            string searchServiceEndpoint = "https://customer-service.search.windows.net";
            string apiKey = Environment.GetEnvironmentVariable("Azure");
            string indexName = "articles";

            searchClient = new SearchClient(new Uri(searchServiceEndpoint), indexName, new AzureKeyCredential(apiKey));
            searchIndexerClient = new SearchIndexerClient(new Uri(searchServiceEndpoint), new AzureKeyCredential(apiKey));
            InitialFill();
        }

        public async void InitialFill()
        {
            Response<SearchResults<SearchDocument>> searchResults = await searchClient.SearchAsync<SearchDocument>("*");

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                var tagsField = result.Document["tags"] as IList<object>;
                IEnumerable<string> tags = tagsField?.Select(tag => tag.ToString());
                documents.Add(new DocumentDTO {
                    Id = result.Document["id"].ToString(),
                    Title = result.Document["title"].ToString(),
                    PdfUrl = result.Document["url"].ToString(),
                    Tags = tags?.ToList() ?? new List<string>()
                });
            }
        }

        public async Task<IEnumerable<DocumentDTO>> GetAllDocumentsAsync()
        {
            return documents;
        }

        public async Task<IEnumerable<DocumentDTO>> SearchDocumentsAsync(string searchText)
        {
            var results = documents
                .Where(doc => doc.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        doc.Tags.Any(tag => tag.Contains(searchText, StringComparison.OrdinalIgnoreCase)));

            return await Task.FromResult(results);
        }

        public async Task UploadDocumentAsync(string id, string title, string url)
        {
            var searchDocument = new SearchDocument { ["id"] = id };

            searchDocument["title"] = title;
            searchDocument["url"] = url;

            var batch = IndexDocumentsBatch.MergeOrUpload(new[] { searchDocument });
            await searchClient.IndexDocumentsAsync(batch);
            documents.Add(new DocumentDTO { Id = id, Title = title, PdfUrl = url, Tags = new List<string>() });
        }

        public async Task UpdateDocumentTitleAsync(string documentId, string newTitle)
        {
            var updatedFields = new Dictionary<string, object>
            {
                { "id", documentId },
                { "title", newTitle }
            };

            var mergeDocument = new SearchDocument(updatedFields);
            var batch = new IndexDocumentsBatch<SearchDocument>();
            batch.Actions.Add(IndexDocumentsAction.MergeOrUpload(mergeDocument));

            await searchClient.IndexDocumentsAsync(batch);
            var updatedDoc = documents.Where(doc => doc.Id.Equals(documentId)).First();
            updatedDoc.Title = newTitle;
        }

        public async Task UpdateDocumentTagsAsync(string documentId, List<string> newTags)
        {
            var updatedDoc = documents.Where(doc => doc.Id.Equals(documentId)).First();
            foreach (var tag in newTags)
            {
                updatedDoc.Tags.Add(tag);
            }
            var searchResults = await searchClient.GetDocumentAsync<SearchDocument>(documentId);
            var searchDocument = searchResults.Value;

            if (searchDocument["tags"] == "")
            {
                foreach (var tag in searchDocument["tags"] as IEnumerable<string>)
                {
                    newTags.Add(tag);
                }
            }
            searchDocument["tags"] = newTags;

            var batch = IndexDocumentsBatch.Upload(new[] { searchDocument });
            await searchClient.IndexDocumentsAsync(batch);
        }
    }
}
