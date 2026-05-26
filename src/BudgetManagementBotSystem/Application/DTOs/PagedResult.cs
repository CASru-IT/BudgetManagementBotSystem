using System.Collections.Generic;

namespace BudgetManagementBotSystem.Application.DTOs
{
    public class PagedResult<T>
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<T> Items { get; set; } = new List<T>();
    }
}
