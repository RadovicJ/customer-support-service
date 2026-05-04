namespace BlazorApp1.DTO
{
    public class DocumentDTO
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string PdfUrl { get; set; }
        public List<string> Tags { get; set; }
    }
}
