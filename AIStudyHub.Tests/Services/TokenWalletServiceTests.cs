using System;
using System.Threading.Tasks;
using AIStudyHub.Business.Exceptions;
using AIStudyHub.Business.Services;
using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;
using AIStudyHub.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class TokenWalletServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly TokenWalletService _service;

    public TokenWalletServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new UnitOfWork(_dbContext);
        _service = new TokenWalletService(_unitOfWork);
    }

    private async Task<Guid> SeedUser(int aiTokens = 1000)
    {
        var userId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        _dbContext.TierMemberships.Add(new TierMembership
        {
            Id = tierId,
            TierName = "Pro",
            StorageLimitMb = 5000,
            AiTokens = aiTokens
        });
        _dbContext.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId}@test.com",
            FullName = "Test User",
            PasswordHash = "hash",
            TierId = tierId,
            CurrentAiTokenUsage = 0
        });
        await _dbContext.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task ReserveAsync_DeductsFromBalance()
    {
        var userId = await SeedUser();

        var reservation = await _service.ReserveAsync(userId, "GenerateQuiz", 100, null);

        Assert.Equal(100, reservation.EstimatedTokens);
        var user = await _dbContext.Users.FindAsync(userId);
        Assert.Equal(100, user!.CurrentAiTokenUsage);
    }

    [Fact]
    public async Task ReserveAsync_ThrowsWhenQuotaExceeded()
    {
        var userId = await SeedUser(aiTokens: 100);

        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            _service.ReserveAsync(userId, "GenerateQuiz", 150, null));
    }

    [Fact]
    public async Task SettleAsync_RefundsOverestimation()
    {
        var userId = await SeedUser();
        var reservation = await _service.ReserveAsync(userId, "GenerateQuiz", 100, null);

        await _service.SettleAsync(reservation.LedgerId, 80);

        var user = await _dbContext.Users.FindAsync(userId);
        Assert.Equal(80, user!.CurrentAiTokenUsage); // 100 - 20 refund

        var ledger = await _dbContext.TokenLedgers.FindAsync(reservation.LedgerId);
        Assert.Equal(TokenLedgerStatus.Committed, ledger!.Status);
        Assert.Equal(80, ledger.ActualTokens);
    }

    [Fact]
    public async Task SettleAsync_NoRefund_WhenExactMatch()
    {
        var userId = await SeedUser();
        var reservation = await _service.ReserveAsync(userId, "GenerateQuiz", 100, null);

        await _service.SettleAsync(reservation.LedgerId, 100);

        var user = await _dbContext.Users.FindAsync(userId);
        Assert.Equal(100, user!.CurrentAiTokenUsage);
    }

    [Fact]
    public async Task RefundAsync_ReturnsAllTokens()
    {
        var userId = await SeedUser();
        var reservation = await _service.ReserveAsync(userId, "GenerateQuiz", 100, null);

        await _service.RefundAsync(reservation.LedgerId, "User cancelled");

        var user = await _dbContext.Users.FindAsync(userId);
        Assert.Equal(0, user!.CurrentAiTokenUsage);

        var ledger = await _dbContext.TokenLedgers.FindAsync(reservation.LedgerId);
        Assert.Equal(TokenLedgerStatus.Refunded, ledger!.Status);
        Assert.Equal("User cancelled", ledger.FailureReason);
    }

    [Fact]
    public async Task GetBalanceAsync_ReturnsCorrectBalance()
    {
        var userId = await SeedUser(aiTokens: 1000);
        await _service.ReserveAsync(userId, "GenerateQuiz", 300, null);

        var result = await _service.GetBalanceAsync(userId);

        Assert.Equal(300, result.Balance.CurrentUsage);
        Assert.Equal(1000, result.Balance.MaxQuota);
        Assert.Equal(700, result.Balance.Available);
    }

    [Fact]
    public async Task ReserveAsync_AllowsExactQuota()
    {
        var userId = await SeedUser(aiTokens: 100);

        var reservation = await _service.ReserveAsync(userId, "GenerateQuiz", 100, null);

        Assert.NotNull(reservation);
        var user = await _dbContext.Users.FindAsync(userId);
        Assert.Equal(100, user!.CurrentAiTokenUsage);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _connection.Dispose();
        _unitOfWork.Dispose();
    }
}
