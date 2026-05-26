using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BudgetManagementBotSystem.Tests.Application.UseCases
{
    public class SubmitBudgetRequestUseCaseTests
    {
        [Fact]
        public async Task ExecuteAsync_WithValidInputAndWithinBudget_AddsPendingRequestAndUpdatesGroup()
        {
            // Arrange
            const int userId = 1;
            const int groupId = 1;
            const decimal amount = 50_000m;
            const string description = "備品購入";

            var user = new User("Test User", 12345UL, AccountRole.Accountant);
                user.ChangeGroupId(0);
            var group = new Group("Test Group");
            group.AddBudgetTransaction(new BudgetTransaction(true, 200_000m, new FiscalYear(4)));

            var mockUserRepository = new Mock<IUserRepository>();
            mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var mockGroupRepository = new Mock<IGroupRepository>();
            mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockFileStorage = CreateFileStorageMock();

            var configuration = CreateConfiguration(4);
            var useCase = new SubmitBudgetRequestUseCase(
                mockUserRepository.Object,
                mockGroupRepository.Object,
                configuration,
                mockUnitOfWork.Object,
                mockFileStorage.Object);

            // Act
            var savedCount = await useCase.ExecuteAsync(userId, groupId, amount, description, Array.Empty<UploadedEvidenceDto>());

            // Assert
            Assert.Single(group.Requests);
            var request = group.Requests.Single();
            Assert.Equal(amount, request.Amount.Value);
            Assert.Equal(description, request.Description);
            Assert.Empty(request.Evidences);
            Assert.Equal(RequestStatus.Pending, request.StatusHistory.Last().ChangedStatus);
            Assert.Equal(0, savedCount);

            mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenBudgetIsInsufficient_ThrowsBudgetLimitExceededException()
        {
            // Arrange
            const int userId = 1;
            const int groupId = 1;
            const decimal amount = 10_000m;

            var user = new User("Test User", 12345UL, AccountRole.Accountant);
                user.ChangeGroupId(0);
            var group = new Group("Test Group"); // 予算0

            var mockUserRepository = new Mock<IUserRepository>();
            mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var mockGroupRepository = new Mock<IGroupRepository>();
            mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockFileStorage = CreateFileStorageMock();

            var configuration = CreateConfiguration(4);
            var useCase = new SubmitBudgetRequestUseCase(
                mockUserRepository.Object,
                mockGroupRepository.Object,
                configuration,
                mockUnitOfWork.Object,
                mockFileStorage.Object);

            // Act
            var ex = await Assert.ThrowsAsync<BudgetLimitExceededException>(
                () => useCase.ExecuteAsync(userId, groupId, amount, "予算超過テスト", Array.Empty<UploadedEvidenceDto>()));

            // Assert
            Assert.Equal("現在の予算上限を超えています。申請は作成されませんでした。", ex.Message);
            Assert.Empty(group.Requests);

            mockFileStorage.Verify(r => r.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
            mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenUserNotFound_ThrowsArgumentNullException()
        {
            // Arrange
            const int userId = 999;
            const int groupId = 1;

            var mockUserRepository = new Mock<IUserRepository>();
            mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            var mockGroupRepository = new Mock<IGroupRepository>();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockFileStorage = CreateFileStorageMock();
            var configuration = CreateConfiguration(4);
            var useCase = new SubmitBudgetRequestUseCase(
                mockUserRepository.Object,
                mockGroupRepository.Object,
                configuration,
                mockUnitOfWork.Object,
                mockFileStorage.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                () => useCase.ExecuteAsync(userId, groupId, 1_000m, "test", Array.Empty<UploadedEvidenceDto>()));

            Assert.Equal("userId", ex.ParamName);
            mockGroupRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenGroupNotFound_ThrowsArgumentNullException()
        {
            // Arrange
            const int userId = 1;
            const int groupId = 999;

            var user = new User("Test User", 12345UL, AccountRole.Accountant);
                user.ChangeGroupId(0);

            var mockUserRepository = new Mock<IUserRepository>();
            mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var mockGroupRepository = new Mock<IGroupRepository>();
            mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync((Group?)null);
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockFileStorage = CreateFileStorageMock();
            var configuration = CreateConfiguration(4);
            var useCase = new SubmitBudgetRequestUseCase(
                mockUserRepository.Object,
                mockGroupRepository.Object,
                configuration,
                mockUnitOfWork.Object,
                mockFileStorage.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                () => useCase.ExecuteAsync(userId, groupId, 1_000m, "test", Array.Empty<UploadedEvidenceDto>()));

            Assert.Equal("groupId", ex.ParamName);
            mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenAmountIsNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            const int userId = 1;
            const int groupId = 1;

            var user = new User("Test User", 12345UL, AccountRole.Accountant);
                user.ChangeGroupId(0);
            var group = new Group("Test Group");

            var mockUserRepository = new Mock<IUserRepository>();
            mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var mockGroupRepository = new Mock<IGroupRepository>();
            mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockFileStorage = CreateFileStorageMock();

            var configuration = CreateConfiguration(4);
            var useCase = new SubmitBudgetRequestUseCase(
                mockUserRepository.Object,
                mockGroupRepository.Object,
                configuration,
                mockUnitOfWork.Object,
                mockFileStorage.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => useCase.ExecuteAsync(userId, groupId, -1m, "test", Array.Empty<UploadedEvidenceDto>()));

            Assert.Equal("amount", ex.ParamName);
            mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_UsesFiscalYearStartMonth_FromConfiguration()
        {
            // Arrange
            const int userId = 1;
            const int groupId = 1;
            const int fiscalYearStartMonth = 7;

            var user = new User("Test User", 12345UL, AccountRole.Accountant);
            user.ChangeGroupId(0);
            var group = new Group("Test Group");
            group.AddBudgetTransaction(new BudgetTransaction(true, 100_000m, new FiscalYear(fiscalYearStartMonth)));

            var mockUserRepository = new Mock<IUserRepository>();
            mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var mockGroupRepository = new Mock<IGroupRepository>();
            mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockFileStorage = CreateFileStorageMock();

            var configuration = CreateConfiguration(fiscalYearStartMonth);
            var useCase = new SubmitBudgetRequestUseCase(
                mockUserRepository.Object,
                mockGroupRepository.Object,
                configuration,
                mockUnitOfWork.Object,
                mockFileStorage.Object);

            // Act
            var savedCount = await useCase.ExecuteAsync(userId, groupId, 10_000m, "fiscal year test", Array.Empty<UploadedEvidenceDto>());

            // Assert
            Assert.Single(group.Requests);
            var request = group.Requests.Single();
            Assert.Equal(fiscalYearStartMonth, request.FiscalYear.StartMonth);
            Assert.Equal(0, savedCount);
            mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithEvidenceFilePaths_AddsEvidencesToRequest()
        {
            // Arrange
            const int userId = 1;
            const int groupId = 1;

            var user = new User("Test User", 12345UL, AccountRole.Accountant);
            user.ChangeGroupId(0);
            var group = new Group("Test Group");
            group.AddBudgetTransaction(new BudgetTransaction(true, 100_000m, new FiscalYear(4)));

            var mockUserRepository = new Mock<IUserRepository>();
            mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var mockGroupRepository = new Mock<IGroupRepository>();
            mockGroupRepository.Setup(r => r.GetByIdAsync(groupId)).ReturnsAsync(group);
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var mockFileStorage = CreateFileStorageMock();

            var configuration = CreateConfiguration(4);
            var useCase = new SubmitBudgetRequestUseCase(
                mockUserRepository.Object,
                mockGroupRepository.Object,
                configuration,
                mockUnitOfWork.Object,
                mockFileStorage.Object);

            var evidenceFilePaths = new[]
            {
                new UploadedEvidenceDto("quote.pdf", new byte[] { 1, 2, 3 }),
                new UploadedEvidenceDto("spec.png", new byte[] { 4, 5, 6 })
            };

            mockFileStorage.Setup(r => r.SaveFileAsync("quote.pdf", It.IsAny<Stream>())).ReturnsAsync("stored/quote.pdf");
            mockFileStorage.Setup(r => r.SaveFileAsync("spec.png", It.IsAny<Stream>())).ReturnsAsync("stored/spec.png");

            // Act
            var savedCount = await useCase.ExecuteAsync(userId, groupId, 10_000m, "evidence test", evidenceFilePaths);

            // Assert
            var request = group.Requests.Single();
            Assert.Equal(2, request.Evidences.Count);
            Assert.Equal("stored/quote.pdf", request.Evidences[0].FilePath);
            Assert.Equal("stored/spec.png", request.Evidences[1].FilePath);
            Assert.Equal(2, savedCount);
            mockFileStorage.Verify(r => r.SaveFileAsync("quote.pdf", It.IsAny<Stream>()), Times.Once);
            mockFileStorage.Verify(r => r.SaveFileAsync("spec.png", It.IsAny<Stream>()), Times.Once);
        }

        private static Mock<IFileStorage> CreateFileStorageMock()
        {
            var mock = new Mock<IFileStorage>();
            mock.Setup(r => r.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .ReturnsAsync((string fileName, Stream _) => $"stored/{fileName}");
            mock.Setup(r => r.GetFileAsync(It.IsAny<string>())).ReturnsAsync(Stream.Null);
            mock.Setup(r => r.DeleteFileAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            return mock;
        }

        private static IConfiguration CreateConfiguration(int fiscalYearStartMonth)
        {
            var settings = new Dictionary<string, string?>
            {
                ["FiscalYearStartMonth:Month"] = fiscalYearStartMonth.ToString()
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
    }
}
