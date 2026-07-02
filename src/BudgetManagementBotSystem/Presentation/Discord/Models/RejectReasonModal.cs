using Discord;
using Discord.Interactions;

namespace BudgetManagementBotSystem.Presentation.Discord.Models;

public class RejectReasonModal : IModal
{
    public string Title => "申請を却下する";

    [InputLabel("却下理由")]
    [ModalTextInput("reason", TextInputStyle.Paragraph, "例: 領収書が不足しているため", maxLength: 500)]
    public string Reason { get; set; } = string.Empty;
}
