using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.Options;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using Microsoft.Extensions.Options;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class BootstrapAdminUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidPassword_ChangesRoleToAdminAndSaves()
    {
        var user = new User("Test User", 12345UL, AccountRole.GroupLeader);
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(12345UL)).ReturnsAsync(user);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var options = Options.Create(new AdminBootstrapOptions { Password = "secret" });

        var useCase = new BootstrapAdminUseCase(mockUserRepository.Object, mockUnitOfWork.Object, options);

        await useCase.ExecuteAsync(12345UL, "Test User", "secret");

        Assert.Equal(AccountRole.Admin, user.Role);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPassword_ThrowsUnauthorizedAccessException()
    {
        var user = new User("Test User", 12345UL, AccountRole.GroupLeader);
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(12345UL)).ReturnsAsync(user);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var options = Options.Create(new AdminBootstrapOptions { Password = "secret" });

        var useCase = new BootstrapAdminUseCase(mockUserRepository.Object, mockUnitOfWork.Object, options);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => useCase.ExecuteAsync(12345UL, "Test User", "wrong"));

        Assert.Equal(AccountRole.GroupLeader, user.Role);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_AddsAdminUserAndSaves()
    {
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByDiscordUserIdAsync(12345UL)).ReturnsAsync((User?)null);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var options = Options.Create(new AdminBootstrapOptions { Password = "secret" });

        var useCase = new BootstrapAdminUseCase(mockUserRepository.Object, mockUnitOfWork.Object, options);

        User? addedUser = null;
        mockUserRepository.Setup(r => r.AddAsync(It.IsAny<User>())).Callback<User>(u => addedUser = u).Returns(Task.CompletedTask);

        await useCase.ExecuteAsync(12345UL, "New User", "secret");

        Assert.NotNull(addedUser);
        Assert.Equal("New User", addedUser!.Name);
        Assert.Equal(12345UL, addedUser.DiscordUserId);
        Assert.Equal(AccountRole.Admin, addedUser.Role);
        mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}