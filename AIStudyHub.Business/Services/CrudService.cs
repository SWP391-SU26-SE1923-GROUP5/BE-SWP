using AIStudyHub.Business.Interfaces.Services;

namespace AIStudyHub.Business.Services;

public abstract class CrudService<TResponse, TCreateRequest, TUpdateRequest> : ICrudService<TResponse, TCreateRequest, TUpdateRequest>
{
    public virtual Task<IReadOnlyList<TResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<TResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<TResponse> CreateAsync(TCreateRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<TResponse> UpdateAsync(Guid id, TUpdateRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
