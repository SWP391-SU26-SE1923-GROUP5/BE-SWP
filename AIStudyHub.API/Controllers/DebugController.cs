using AIStudyHub.Business.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QdrantOptions _qdrantOptions;

    public DebugController(IHttpClientFactory httpClientFactory, IOptions<QdrantOptions> qdrantOptions)
    {
        _httpClientFactory = httpClientFactory;
        _qdrantOptions = qdrantOptions.Value;
    }

    /// <summary>List all Qdrant collections + point count for each.</summary>
    [HttpGet("qdrant/collections")]
    public async Task<IActionResult> GetQdrantCollections()
    {
        var url = $"{_qdrantOptions.Url}/collections";
        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    /// <summary>Count points in collection 'aistudyhub-docs' for a given documentId.</summary>
    [HttpGet("qdrant/count/{documentId}")]
    public async Task<IActionResult> CountChunksForDocument(Guid documentId)
    {
        var url = $"{_qdrantOptions.Url}/collections/{_qdrantOptions.CollectionName}/points/count";
        var payload = new
        {
            filter = new
            {
                must = new[]
                {
                    new { key = "documentId", match = new { value = documentId.ToString() } }
                }
            },
            exact = true
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var client = _httpClientFactory.CreateClient();
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        var resultJson = await response.Content.ReadAsStringAsync();
        return Content(resultJson, "application/json");
    }

    /// <summary>Count points in 'aistudyhub' (Kernel Memory collection) for a given documentId.</summary>
    [HttpGet("qdrant/km-count/{documentId}")]
    public async Task<IActionResult> CountKmChunksForDocument(Guid documentId)
    {
        var url = $"{_qdrantOptions.Url}/collections/aistudyhub/points/count";
        var payload = new
        {
            filter = new
            {
                must = new[]
                {
                    new { key = "documentId", match = new { value = documentId.ToString() } }
                }
            },
            exact = true
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var client = _httpClientFactory.CreateClient();
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        var resultJson = await response.Content.ReadAsStringAsync();
        return Content(resultJson, "application/json");
    }

    /// <summary>Count points with user_id tag in 'aistudyhub' (Kernel Memory) for a given userId.</summary>
    [HttpGet("qdrant/km-user/{userId}")]
    public async Task<IActionResult> CountKmChunksForUser(Guid userId)
    {
        var url = $"{_qdrantOptions.Url}/collections/aistudyhub/points/count";
        var payload = new
        {
            filter = new
            {
                must = new[]
                {
                    new { key = "user_id", match = new { value = userId.ToString() } }
                }
            },
            exact = true
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var client = _httpClientFactory.CreateClient();
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content);
        var resultJson = await response.Content.ReadAsStringAsync();
        return Content(resultJson, "application/json");
    }
}
