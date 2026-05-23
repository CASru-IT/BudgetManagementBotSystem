using System;
using System.Linq;
using System.Threading.Tasks;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Enums;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.Application.Interface;
using Moq;
using Xunit;

namespace BudgetManagementBotSystem.Tests.Application.UseCases
{
    public class UserCancelBudgetRequestUseCaseTests
    {
        [Fact]
        public async Task ExecuteAsync_AllowsOwnerToCancelPending()
        {
            // arrange
            var group = new Group("TestGroup");
            // set group id via reflection
            typeof(Group).GetProperty("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.SetValue(group, 1);

            var user = new User("Alice", 12345ul, AccountRole.GroupLeader);
            typeof(User).GetProperty("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.SetValue(user, 11);
            // set user's GroupId
            typeof(User).GetProperty("GroupId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.SetValue(user, 1);

            var money = new BudgetManagementBotSystem.Domain.ValueObjects.Money(100);
            var fy = new BudgetManagementBotSystem.Domain.ValueObjects.FiscalYear(4);
            int reqId = group.CreateBudgetRequest(user, money, fy, "desc", Enumerable.Empty<string>());

            var groupRepoMock = new Mock<IGroupRepository>();
            var userRepoMock = new Mock<IUserRepository>();
            var uowMock = new Mock<IUnitOfWork>();

            groupRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);
            userRepoMock.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(user);

            var uc = new UserCancelBudgetRequestUseCase(groupRepoMock.Object, userRepoMock.Object, uowMock.Object);

            // act
            await uc.ExecuteAsync(1, reqId, 11);

            // assert
            var lastStatus = group.Requests.First().StatusHistory.Last().ChangedStatus;
            Assert.Equal(RequestStatus.Cancelled, lastStatus);
            uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }
    }
}
