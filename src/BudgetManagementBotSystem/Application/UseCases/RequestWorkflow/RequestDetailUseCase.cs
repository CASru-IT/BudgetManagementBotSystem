using BudgetManagementBotSystem.Domain.Repository;

namespace BudgetManagementBotSystem.Application.UseCases.RequestWorkflow
{
    public class RequestDetailUseCase
    {
        private readonly IGroupRepository _groupRepository;

        public RequestDetailUseCase(IGroupRepository groupRepository)
        {
            _groupRepository = groupRepository;
        }

        public async Task<(BudgetManagementBotSystem.Domain.Entities.BudgetRequest? request, int? groupId)> GetByIdAsync(int requestId)
        {
            var groups = await _groupRepository.GetAllAsync();
            if (groups == null) return (null, null);

            foreach (var g in groups)
            {
                var r = g.Requests.FirstOrDefault(x => x.Id == requestId);
                if (r != null) return (r, g.Id);
            }

            return (null, null);
        }
    }
}
