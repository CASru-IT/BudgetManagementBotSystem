using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Exceptions;
using BudgetManagementBotSystem.Domain.ValueObjects;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class SubmitRequestPersistenceTests
{
    [Fact]
    public async Task StoredMembership_RejectsWrongGroupAndPersistsValidRequestWithEvidence()
    {
        var options = new DbContextOptionsBuilder<BudgetManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new BudgetManagementDbContext(options);
        var ownGroup = new Group("所属班");
        var otherGroup = new Group("他班");
        foreach (var group in new[] { ownGroup, otherGroup })
            group.AddBudgetTransaction(new BudgetTransaction(true, 1000, new FiscalYear(4)));
        db.Groups.AddRange(ownGroup, otherGroup);
        await db.SaveChangesAsync();
        var user = new User("Requester", 1, AccountRole.GroupLeader, ownGroup.Id);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var ownId = ownGroup.Id;
        var otherId = otherGroup.Id;
        var userId = user.Id;
        db.ChangeTracker.Clear();
        var files = new Mock<IFileStorage>();
        files.Setup(x => x.SaveFileAsync("receipt.pdf", It.IsAny<Stream>())).ReturnsAsync("stored/receipt.pdf");
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["FiscalYearStartMonth:Month"] = "4" }).Build();
        var useCase = new SubmitBudgetRequestUseCase(new EfCoreUserRepository(db), new EfCoreGroupRepository(db), config, new EfUnitOfWork(db), files.Object);
        var evidence = new[] { new UploadedEvidenceDto("receipt.pdf", new byte[] { 1, 2, 3 }) };

        await Assert.ThrowsAsync<BudgetRequestMembershipException>(() => useCase.ExecuteAsync(userId, otherId, 100, "備品購入", evidence));
        files.Verify(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
        Assert.Equal(0, await db.BudgetRequests.CountAsync());
        Assert.Equal(0, await db.RequestEvidences.CountAsync());

        var result = await useCase.ExecuteAsync(userId, ownId, 100, "備品購入", evidence);
        db.ChangeTracker.Clear();
        var saved = await db.BudgetRequests.Include(x => x.Evidences).Include(x => x.StatusHistory).SingleAsync();
        Assert.Equal(result.RequestId, saved.Id);
        Assert.Equal(userId, saved.UserId);
        Assert.Equal(ownId, db.Entry(saved).Property<int?>("GroupId").CurrentValue);
        Assert.Equal(RequestStatus.Pending, saved.GetCurrentStatus());
        Assert.Equal("stored/receipt.pdf", Assert.Single(saved.Evidences).FilePath);
        Assert.Equal(ownId, (await db.Users.SingleAsync()).GroupId);
        Assert.Equal(1, result.SavedEvidenceCount);
    }
}
