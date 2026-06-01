namespace AIStudyHub.Business.Interfaces.Services;

public interface ICrudService<TResponse, in TCreateRequest, in TUpdateRequest>
{
    Task<IReadOnlyList<TResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TResponse> CreateAsync(TCreateRequest request, CancellationToken cancellationToken = default);
    Task<TResponse> UpdateAsync(Guid id, TUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
