using AIStudyHub.Business.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIStudyHub.API.Controllers;

[ApiController]
[Authorize]
public abstract class CrudControllerBase<TResponse, TCreateRequest, TUpdateRequest> : ControllerBase
{
    private readonly ICrudService<TResponse, TCreateRequest, TUpdateRequest> _service;

    protected CrudControllerBase(ICrudService<TResponse, TCreateRequest, TUpdateRequest> service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TResponse>> Create(TCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = GetResponseId(result) }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TResponse>> Update(Guid id, TUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private static Guid GetResponseId(TResponse response)
    {
        var property = typeof(TResponse).GetProperty("Id");
        return property?.GetValue(response) is Guid id ? id : Guid.Empty;
    }
}
