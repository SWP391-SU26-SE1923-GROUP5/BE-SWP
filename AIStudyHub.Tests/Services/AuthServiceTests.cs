using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Authentication;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Mappings;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Interfaces;
using AIStudyHub.Data.Repositories;
using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

/// <summary>
/// Integration-style tests for <see cref="AuthService"/> that exercise the
/// real EF Core context, AutoMapper configuration, and navigation property
/// loading paths so that regressions like a <c>TierName</c> falling back to
/// <c>"Unknown"</c> are caught before manual testing.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private const string FreeTierName = "Free";
    private const string ProTierName = "Pro";
    private const string Password = "Password123!";

    private readonly ApplicationDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly IMapper _mapper;
    private readonly JwtOptions _jwtOptions;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly EmailVerificationOptions _emailVerificationOptions;
    private readonly OtpOptions _otpOptions;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _authService;

    private TierMembership _freeTier = null!;
    private TierMembership _proTier = null!;

    public AuthServiceTests()
    {
        var store = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .Returns(async (string email) =>
            {
                var normalized = email.ToUpperInvariant();
                // Simulate the real ASP.NET Identity behaviour: load the user via
                // an independent query that does NOT prime navigation properties
                // and does NOT attach the entity to the change tracker. The
                // service under test is responsible for loading anything it needs.
                var store = _dbContext.Users.AsNoTracking()
                    .FirstOrDefault(u => u.NormalizedEmail == normalized);
                return store!;
            });
        _userManagerMock.Setup(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GetRolesAsync(It.IsAny<User>()))
            .ReturnsAsync(new List<string> { "Student" });

        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        SeedTiers();

        _unitOfWork = new UnitOfWork(_dbContext);
        var mapperCfg = new MapperConfigurationExpression();
        mapperCfg.AddProfile<ApplicationMappingProfile>();
        _mapper = new MapperConfiguration(mapperCfg, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
            .CreateMapper();

        _jwtOptions = new JwtOptions
        {
            SecretKey = "super_secret_key_for_testing_purposes_only_which_is_long_enough",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        _emailServiceMock = new Mock<IEmailService>();
        _emailServiceMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _emailVerificationOptions = new EmailVerificationOptions();
        _otpOptions = new OtpOptions { ExpiryMinutes = 5, SendWindowMinutes = 1, MaxSendAttemptsPerWindow = 3 };
        _loggerMock = new Mock<ILogger<AuthService>>();

        _authService = new AuthService(
            _userManagerMock.Object,
            _dbContext,
            _unitOfWork,
            _mapper,
            _jwtOptions,
            _emailServiceMock.Object,
            _emailVerificationOptions,
            _otpOptions,
            null,
            _loggerMock.Object);
    }

    private void SeedTiers()
    {
        _freeTier = new TierMembership
        {
            Id = Guid.NewGuid(),
            TierName = FreeTierName,
            Price = 0,
            StorageLimitMb = 1024,
            AiTokens = 10000,
            CreatedAt = DateTime.UtcNow
        };
        _proTier = new TierMembership
        {
            Id = Guid.NewGuid(),
            TierName = ProTierName,
            Price = 99000,
            StorageLimitMb = 5120,
            AiTokens = 100000,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.TierMemberships.AddRange(_freeTier, _proTier);
        _dbContext.SaveChanges();
    }

    private User AddUserToTier(TierMembership tier, string email, bool emailConfirmed = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            FullName = email.Split('@')[0],
            EmailConfirmed = emailConfirmed,
            IsActive = true,
            Status = "active",
            Role = "student",
            TierId = tier.Id,
            CurrentStorageCapacity = 0,
            CurrentAiTokenUsage = 0,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = "TEST-HASH-NOT-VERIFIED",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();

        // Detach so the User entity returned by FindByEmailAsync behaves like a
        // fresh load (navigation properties are NOT eagerly populated). This
        // mirrors SQL Server reality where FindByEmailAsync ignores
        // navigation loads.
        _dbContext.Entry(user).State = EntityState.Detached;
        return user;
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var request = new RegisterRequestDto("Test User", "dup@test.com", Password, null);
        _userManagerMock.Setup(x => x.FindByEmailAsync("dup@test.com"))
            .ReturnsAsync(new User { Email = "dup@test.com" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request));

        Assert.Equal("Email is already registered.", ex.Message);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUserAndDispatchesOtp()
    {
        const string email = "register-success@test.com";
        var request = new RegisterRequestDto("Test User", email, Password, null);
        _userManagerMock.Setup(x => x.FindByEmailAsync(email)).ReturnsAsync((User?)null);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), Password))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _authService.RegisterAsync(request);

        Assert.Equal(email, result.Email);
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<User>(), Password), Times.Once);
        _emailServiceMock.Verify(
            x => x.SendAsync(email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var otp = await _dbContext.OtpRecords.FirstAsync(o => o.Email == email);
        Assert.Equal(OtpType.EmailVerification, otp.Type);
        Assert.False(otp.UsedAt.HasValue);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedAccessException()
    {
        _userManagerMock.Setup(x => x.FindByEmailAsync("nobody@test.com")).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.LoginAsync(new LoginRequestDto("nobody@test.com", Password)));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_UnconfirmedEmail_ThrowsInvalidOperationException()
    {
        var user = AddUserToTier(_freeTier, "unconfirmed@test.com", emailConfirmed: false);
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, Password)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LoginAsync(new LoginRequestDto(user.Email, Password)));

        Assert.Equal("Please verify your email before logging in.", ex.Message);
    }

    /// <summary>
    /// Regression test for the bug where freshly-registered users had a
    /// <c>TierName</c> of <c>"Unknown"</c> on the login response because
    /// <see cref="AuthService.LoginAsync"/> did not load the
    /// <see cref="User.TierMembership"/> navigation property before mapping.
    /// </summary>
    [Fact]
    public async Task LoginAsync_ValidCredentials_FreeTierMapsToFreeTierName()
    {
        var user = AddUserToTier(_freeTier, "fresh@test.com");
        // Force navigation to null so we can prove the service loads it explicitly.
        user.TierMembership = null!;
        _dbContext.Entry(user).State = EntityState.Detached;

        var result = await _authService.LoginAsync(new LoginRequestDto(user.Email, Password));

        Assert.Equal(FreeTierName, result.User.TierName);
        Assert.NotEqual("Unknown", result.User.TierName);
        Assert.Equal(_freeTier.Id, result.User.TierId);
        Assert.Equal(_freeTier.StorageLimitMb, result.User.TierStorageLimitMb);
        Assert.Equal(_freeTier.AiTokens, result.User.TierAiTokens);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));

        // Refresh token row should now exist in DB.
        var refreshRow = await _dbContext.RefreshTokens.SingleAsync(rt => rt.UserId == user.Id);
        Assert.True(refreshRow.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_UserOnProTier_ReturnsProTierMetadata()
    {
        var user = AddUserToTier(_proTier, "pro@test.com");
        user.TierExpireAt = DateTime.UtcNow.AddDays(30);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, Password)).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Student" });

        var result = await _authService.LoginAsync(new LoginRequestDto(user.Email, Password));

        Assert.Equal(ProTierName, result.User.TierName);
        Assert.Equal(_proTier.AiTokens, result.User.TierAiTokens);
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidTokenString_ThrowsUnauthorized()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.RefreshTokenAsync(new RefreshTokenRequestDto("definitely-not-a-real-token")));

        Assert.Equal("Invalid refresh token.", ex.Message);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
