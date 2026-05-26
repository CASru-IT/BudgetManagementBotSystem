using BudgetManagementBotSystem.Application.UseCases.Groups;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Repository;
using Moq;

namespace BudgetManagementBotSystem.Tests.Application.UseCases;

public class RegisterGroupUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidName_AddsGroupAndSaves()
    {
        // Arrange
        var mockGroupRepository = new Mock<IGroupRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var useCase = new RegisterGroupUseCase(mockGroupRepository.Object, mockUnitOfWork.Object);

        // Act
        await useCase.ExecuteAsync("New Group");

        // Assert
        mockGroupRepository.Verify(r => r.AddAsync(It.IsAny<BudgetManagementBotSystem.Domain.Entities.Group>()), Times.Once);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var mockGroupRepository = new Mock<IGroupRepository>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var useCase = new RegisterGroupUseCase(mockGroupRepository.Object, mockUnitOfWork.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync("  "));
        mockGroupRepository.Verify(r => r.AddAsync(It.IsAny<BudgetManagementBotSystem.Domain.Entities.Group>()), Times.Never);
        mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
