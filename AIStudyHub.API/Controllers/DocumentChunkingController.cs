using AIStudyHub.Business.Features.Documents;
using AIStudyHub.Business.ultis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AIStudyHub.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class DocumentChunkingController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentChunkingController(IMediator mediator)
    {
        _mediator = mediator;
        
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

        // Retrieve top chunks via MediatR. Handler should return IEnumerable<ChunkingFile.RetrievedChunk> or compatible type.
        var topChunksObj = await _mediator.Send(new SearchDocumentChunksQuery(id, request.Question, 5), cancellationToken);

        if (topChunksObj is not IEnumerable<ChunkingFile.RetrievedChunk> topChunks)
        {
            // try to handle when handler returns a raw object (best-effort)
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
        using var client = new System.Net.Http.HttpClient();
        var baseUrl = Environment.GetEnvironmentVariable("LOCAL_LLM_URL") ?? "http://localhost:6768/";
        client.BaseAddress = new Uri(baseUrl);

        var model = "Llama 3.2 1B Instruct";

        var payload = new
        {
            model = model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 1000,
            temperature = 0.29
        };

        var resp = await client.PostAsJsonAsync("v1/chat/completions", payload, cancellationToken);
        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, raw);

        using var doc = System.Text.Json.JsonDocument.Parse(raw);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out var content))
            {
                return Ok(new { answer = content.GetString() });
            }
        }

        return Ok(new { raw });
    }
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
    [FromBody] ChatRequest request,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
            return BadRequest("Question is required");

        using var client = new HttpClient();

        var baseUrl =
            Environment.GetEnvironmentVariable("LOCAL_LLM_URL")
            ?? "http://localhost:6768/";

        client.BaseAddress = new Uri(baseUrl);

        var model ="Llama 3.2 1B Instruct";

        var payload = new
        {
            model,
            messages = new[]
            {
            new
            {
                role = "user",
                content = request.Question
            }
        },
            max_tokens = 1000,
            temperature = 0.29
        };

        var resp = await client.PostAsJsonAsync(
            "v1/chat/completions",
            payload,
            cancellationToken);

        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, raw);

        using var doc = JsonDocument.Parse(raw);

        var answer =
            doc.RootElement
               .GetProperty("choices")[0]
               .GetProperty("message")
               .GetProperty("content")
               .GetString();

        return Ok(new { answer });
    }
}
