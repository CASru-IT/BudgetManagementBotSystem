using System;
using BudgetManagementBotSystem.Domain.Enums;

namespace BudgetManagementBotSystem.Application.DTOs
{
    public class PendingRequestDto
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime RequestDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public RequestStatus Status { get; set; } = RequestStatus.Pending;
    }
}
