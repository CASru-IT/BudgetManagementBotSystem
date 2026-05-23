using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class RegisterUserUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidInput_AddsUserAndSaves()
    {
        // Arrange
        var mockUserRepository = new Mock<IUserRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new RegisterUserUseCase(mockUserRepository.Object, mockUnitOfWork.Object);

        // Act
        await useCase.ExecuteAsync("Test User", 12345UL, AccountRole.Member);

        // Assert
        mockUserRepository.Verify(r => r.AddAsync(It.IsAny<BudgetManagementBotSystem.Domain.Entities.User>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var mockUserRepository = new Mock<IUserRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var useCase = new RegisterUserUseCase(mockUserRepository.Object, mockUnitOfWork.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync("   ", 12345UL, AccountRole.Member));
        mockUserRepository.Verify(r => r.AddAsync(It.IsAny<BudgetManagementBotSystem.Domain.Entities.User>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
