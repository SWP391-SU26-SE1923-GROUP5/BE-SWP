using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Users;
using AIStudyHub.Business.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Interfaces;
using AIStudyHub.Data.Repositories;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);

        var store = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _mapperMock = new Mock<IMapper>();

        _userService = new UserService(_unitOfWork, _userManagerMock.Object, _mapperMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), FullName = "User One", Email = "user1@test.com", PasswordHash = "hash" };
        var user2 = new User { Id = Guid.NewGuid(), FullName = "User Two", Email = "user2@test.com", PasswordHash = "hash" };
        
        _dbContext.Users.AddRange(user1, user2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _userService.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByIdAsync_UserExists_ReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FullName = "Test User", Email = "test@test.com", PasswordHash = "hash" };
        
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _userService.GetByIdAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("Test User", result.FullName);
    }

    [Fact]
    public async Task GetByIdAsync_UserDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _userService.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_EmailExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateUserRequestDto("New User", "existing@test.com", "Password123!", null, 0, 0, "active", "student");
        _userManagerMock.Setup(x => x.FindByEmailAsync("existing@test.com")).ReturnsAsync(new User());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _userService.CreateAsync(request));
        Assert.Equal("Email is already registered.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesUser()
    {
        // Arrange
        var request = new CreateUserRequestDto("New User", "new@test.com", "Password123!", null, 0, 0, "active", "student");
        _userManagerMock.Setup(x => x.FindByEmailAsync("new@test.com")).ReturnsAsync((User?)null);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), request.Password)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), "Student")).ReturnsAsync(IdentityResult.Success);
        
        var dto = new UserResponseDto(Guid.NewGuid(), "New User", "new@test.com", null, 0, 0, "active", "student", Guid.Empty, "Unknown", 0, 0, null, DateTime.UtcNow, null);
        _mapperMock.Setup(x => x.Map<UserResponseDto>(It.IsAny<User>())).Returns(dto);

        // Act
        var result = await _userService.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new@test.com", result.Email);
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<User>(), request.Password), Times.Once);
        _userManagerMock.Verify(x => x.AddToRoleAsync(It.IsAny<User>(), "Student"), Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
