using System.Collections.Generic;

namespace BudgetManagementBotSystem.Presentation.Discord.Models
{
    public class PaginatedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public IEnumerable<T> Items { get; set; } = new List<T>();
    }
}
