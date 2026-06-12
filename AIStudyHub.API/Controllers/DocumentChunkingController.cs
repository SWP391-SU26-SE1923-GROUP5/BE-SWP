using AIStudyHub.Business.Features.Documents;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.ultis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class DocumentChunkingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RagOptions _ragOptions;

    public DocumentChunkingController(
        IMediator mediator,
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> ragOptions)
    {
        _mediator = mediator;
        _httpClientFactory = httpClientFactory;
        _ragOptions = ragOptions.Value;
    }

    [HttpGet("document/{id:guid}")]
    public async Task<IActionResult> GetDocumentChunks(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDocumentChunksQuery(id), cancellationToken);
        return result is null ? NotFound("Document not found") : Ok(result);
    }

    [HttpPost("document/{id:guid}")]
    public async Task<IActionResult> CreateDocumentChunks(Guid id, CancellationToken cancellationToken)
    {
        var count = await _mediator.Send(new CreateDocumentChunksCommand(id), cancellationToken);
        return Ok(new { message = "Document chunks created successfully", count });
    }

    [HttpGet("document/{id:guid}/search")]
    public async Task<IActionResult> SearchDocumentChunks(
        Guid id,
        [FromQuery(Name = "q")] string q,
        [FromQuery] int top = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required");

        var results = await _mediator.Send(new SearchDocumentChunksQuery(id, q, top), cancellationToken);
        return Ok(results);
    }

    public class ChatRequest
    {
        public string Question { get; set; } = string.Empty;
    }

    [HttpPost("document/{id:guid}/chat")]
    public async Task<IActionResult> ChatDocument(Guid id, [FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
            return BadRequest("Question is required in request body");

        var topChunksObj = await _mediator.Send(new SearchDocumentChunksQuery(id, request.Question, _ragOptions.TopKChunks), cancellationToken);

        if (topChunksObj is not IEnumerable<ChunkingFile.RetrievedChunk> topChunks)
        {
            return BadRequest("Failed to retrieve document chunks from handler");
        }

        var context = string.Join("\n\n", topChunks.Select(c => c.Text));

        var prompt = $"""
You are a helpful assistant.

Use ONLY the context below to answer the question.
If the answer is not in the context, say "I have no information about this topic.".

---

CONTEXT:
{context}

---

QUESTION:
{request.Question}

ANSWER:
""";

        var answer = await CallLlmAsync(prompt, cancellationToken);
        return Ok(new { answer });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
            return BadRequest("Question is required");

        var answer = await CallLlmAsync(request.Question, cancellationToken);
        return Ok(new { answer });
    }

    private async Task<string> CallLlmAsync(string prompt, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("LlmClient");
        
        var baseUrl = _ragOptions.Gpt4AllUrl.TrimEnd('/');
        client.BaseAddress = new Uri(baseUrl);

        var payload = new
        {
            model = _ragOptions.Gpt4AllModel,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = _ragOptions.MaxTokens,
            temperature = _ragOptions.Temperature
        };

        var resp = await client.PostAsJsonAsync("v1/chat/completions", payload, cancellationToken);
        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"LLM request failed: {raw}");

        using var doc = JsonDocument.Parse(raw);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("Failed to parse LLM response");
    }
}
