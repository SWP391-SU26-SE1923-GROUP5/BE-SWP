using AIStudyHub.Data.Entities;
using AIStudyHub.Business.Interfaces.Services;
using System.Text;
using System.IO;
using UglyToad.PdfPig;
using System.Text.Json;
using System.Net.Http;
using System.Linq;

namespace AIStudyHub.Business.ultis;

public class ChunkingFile
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmbeddingService _embeddingService;

    public ChunkingFile(IHttpClientFactory httpClientFactory, IEmbeddingService embeddingService)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingService = embeddingService;
    }

    public class RetrievedChunk
    {
        public Guid Id { get; set; }
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public float Score { get; set; }
    }

    private List<string> SplitSentences(string text)
    {
        return text
            .Split(new[] { ". ", "? ", "! " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null) return 0f;
        if (a.Length != b.Length) return 0f;

        double dot = 0;
        double na = 0;
        double nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0f;
        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }

    public async Task<List<RetrievedChunk>> RetrieveRelevantChunksAsync(IEnumerable<DocumentChunk> chunks, string query, int top = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentNullException(nameof(query));

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        var results = new List<RetrievedChunk>();

        foreach (var ch in chunks.Where(c => c.EmbeddingJson != null))
        {
            try
            {
                float[] emb;
                using (var doc = JsonDocument.Parse(ch.EmbeddingJson!))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<float>(root.GetArrayLength());
                        foreach (var item in root.EnumerateArray())
                            list.Add((float)item.GetDouble());
                        emb = list.ToArray();
                    }
                    else
                    {
                        continue;
                    }
                }

                var score = CosineSimilarity(queryEmbedding, emb);

                string text = string.Empty;
                int index = -1;
                using (var doc = JsonDocument.Parse(ch.ChunkJson!))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("text", out var t))
                        text = t.GetString() ?? string.Empty;
                    if (root.TryGetProperty("index", out var ix) && ix.ValueKind == JsonValueKind.Number)
                        index = ix.GetInt32();
                }

                results.Add(new RetrievedChunk
                {
                    Id = ch.Id,
                    Index = index,
                    Text = text,
                    Score = score
                });
            }
            catch
            {
            }
        }

        return results.OrderByDescending(r => r.Score).Take(top).ToList();
    }

    private List<string> BuildChunks(string text, int maxWords = 200)
    {
        var sentences = SplitSentences(text);

        var chunks = new List<string>();
        var currentChunk = new List<string>();
        int wordCount = 0;

        foreach (var sentence in sentences)
        {
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (wordCount + words.Length > maxWords)
            {
                chunks.Add(string.Join(" ", currentChunk));
                currentChunk.Clear();
                wordCount = 0;
            }

            currentChunk.Add(sentence);
            wordCount += words.Length;
        }

        if (currentChunk.Any())
            chunks.Add(string.Join(" ", currentChunk));

        return chunks;
    }

    public async Task<List<DocumentChunk>> CreateChunksAsync(Document doc)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        if (string.IsNullOrWhiteSpace(doc.FileLink))
            throw new Exception("FileLink is null or empty");

        var text = await ExtractTextAsync(doc.FileLink);

        if (string.IsNullOrWhiteSpace(text))
            throw new Exception("ExtractText returned empty text");

        var chunks = BuildChunks(text);

        if (chunks == null)
            throw new Exception("BuildChunks returned null");

        int index = 0;
        var documentChunks = new List<DocumentChunk>();

        foreach (var chunk in chunks)
        {
            string? embeddingJson = null;

            try
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
                embeddingJson = JsonSerializer.Serialize(embedding);
            }
            catch
            {
                embeddingJson = null;
            }

            var entity = new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                ChunkJson = JsonSerializer.Serialize(new
                {
                    text = chunk,
                    index = index++
                }),
                EmbeddingJson = embeddingJson
            };

            documentChunks.Add(entity);
        }

        return documentChunks;
    }

    private async Task<string> ExtractTextAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // If the path is an HTTP/HTTPS URL, fetch and handle by content or extension
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            using var httpClient = _httpClientFactory.CreateClient();

            // If extension indicates PDF, download bytes and parse PDF
            if (uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await httpClient.GetByteArrayAsync(uri);
                using var ms = new MemoryStream(bytes);
                try
                {
                    using var pdf = PdfDocument.Open(ms);
                    var sb = new StringBuilder();
                    foreach (var page in pdf.GetPages())
                        sb.AppendLine(page.Text);
                    return sb.ToString();
                }
                catch
                {
                    // Fallback to raw string if parsing fails
                    return await httpClient.GetStringAsync(uri);
                }
            }

            // Otherwise try to detect content type first
            using var response = await httpClient.GetAsync(uri);
            if (response.IsSuccessStatusCode &&
                response.Content.Headers.ContentType?.MediaType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) == true)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                using var ms = new MemoryStream(bytes);
                try
                {
                    using var pdf = PdfDocument.Open(ms);
                    var sb = new StringBuilder();
                    foreach (var page in pdf.GetPages())
                        sb.AppendLine(page.Text);
                    return sb.ToString();
                }
                catch
                {
                    // ignore and fallthrough to return text
                }
            }

            // Default: treat as plain text
            return await response.Content.ReadAsStringAsync();
        }

        // Otherwise treat as a local file and read its text content.
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        if (ext == ".pdf")
        {
            await using var fs = File.OpenRead(path);
            using var pdf = PdfDocument.Open(fs);
            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }

        // Non-PDF local files: read as plain text
        return await File.ReadAllTextAsync(path);
    }
}
