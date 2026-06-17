using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Business.Services;

public sealed class UserService : CrudService<UserResponseDto, CreateUserRequestDto, UpdateUserRequestDto>, IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<User> _userManager;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateUserRequestDto> _createValidator;
    private readonly IValidator<UpdateUserRequestDto> _updateValidator;

    public UserService(
        IUnitOfWork unitOfWork,
        UserManager<User> userManager,
        IMapper mapper,
        IValidator<CreateUserRequestDto> createValidator,
        IValidator<UpdateUserRequestDto> updateValidator)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public override async Task<IReadOnlyList<UserResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return users.Select(MapToDto).ToList();
    }

    public override async Task<UserResponseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserTierInfoDto?> GetUserTierInfoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users
            .Query()
            .Include(u => u.TierMembership)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null) return null;

        return new UserTierInfoDto(
            user.TierId,
            user.TierMembership?.TierName ?? "Unknown",
            user.TierMembership?.StorageLimitMb ?? 0,
            user.TierMembership?.AiTokens ?? 0,
            user.TierExpireAt,
            user.CurrentStorageCapacity,
            user.CurrentAiTokenUsage);
    }

    public async Task UpdateUserTierAsync(Guid userId, UpdateUserTierRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException("User not found.");

        var tier = await _unitOfWork.TierMemberships.GetByIdAsync(request.TierId, cancellationToken)
            ?? throw new KeyNotFoundException("Tier not found.");

        user.TierId = request.TierId;
        user.TierExpireAt = request.TierExpireAt;
        await _userManager.UpdateAsync(user);
    }

    private UserResponseDto MapToDto(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            DateOfBirth = user.DateOfBirth,
            CurrentStorageCapacity = user.CurrentStorageCapacity,
            CurrentAiTokenUsage = user.CurrentAiTokenUsage,
            Status = user.Status,
            Role = user.Role,
            TierId = user.TierId,
            TierName = user.TierMembership?.TierName ?? "Unknown",
            TierStorageLimitMb = user.TierMembership?.StorageLimitMb ?? 0,
            TierAiTokens = user.TierMembership?.AiTokens ?? 0,
            TierExpireAt = user.TierExpireAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public override async Task<UserResponseDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = NormalizeEmail(request.Email);
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var normalizedRole = request.Role.Trim().ToLowerInvariant();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DateOfBirth = request.DateOfBirth,
            CurrentStorageCapacity = request.CurrentStorageCapacity,
            CurrentAiTokenUsage = request.CurrentAiTokenUsage,
            Status = request.Status.Trim().ToLowerInvariant(),
            Role = normalizedRole,
            IsActive = string.Equals(request.Status, "active", StringComparison.OrdinalIgnoreCase),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        EnsureIdentitySucceeded(createResult);

        var roleResult = await _userManager.AddToRoleAsync(user, CapitalizeRole(normalizedRole));
        EnsureIdentitySucceeded(roleResult);

        return _mapper.Map<UserResponseDto>(user);
    }

    public override async Task<UserResponseDto> UpdateAsync(Guid id, UpdateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new KeyNotFoundException("User not found.");

        var previousRole = user.Role;
        var normalizedRole = request.Role.Trim().ToLowerInvariant();

        user.FullName = request.FullName.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.CurrentStorageCapacity = request.CurrentStorageCapacity;
        user.CurrentAiTokenUsage = request.CurrentAiTokenUsage;
        user.Status = request.Status.Trim().ToLowerInvariant();
        user.Role = normalizedRole;
        user.IsActive = string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase);

        var updateResult = await _userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(updateResult);

        if (!string.Equals(previousRole, normalizedRole, StringComparison.OrdinalIgnoreCase))
        {
            var previousRoleName = CapitalizeRole(previousRole);
            var newRoleName = CapitalizeRole(normalizedRole);

            if (await _userManager.IsInRoleAsync(user, previousRoleName))
            {
                var removeResult = await _userManager.RemoveFromRoleAsync(user, previousRoleName);
                EnsureIdentitySucceeded(removeResult);
            }

            if (!await _userManager.IsInRoleAsync(user, newRoleName))
            {
                var addResult = await _userManager.AddToRoleAsync(user, newRoleName);
                EnsureIdentitySucceeded(addResult);
            }
        }

        return _mapper.Map<UserResponseDto>(user);
    }

    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new KeyNotFoundException("User not found.");

        var result = await _userManager.DeleteAsync(user);
        EnsureIdentitySucceeded(result);
    }

    private static void EnsureIdentitySucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException(errors);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string CapitalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "Student";
        }

        return char.ToUpperInvariant(role[0]) + role[1..].ToLowerInvariant();
    }
}
