using Discord;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers;

public static class EvidenceAttachmentValidator
{
    private const long MaxEvidenceFileSizeBytes = 10 * 1024 * 1024;

    public static IReadOnlyList<string> Validate(IReadOnlyCollection<IAttachment> attachments)
    {
        var errors = new List<string>();

        if (attachments.Count == 0)
        {
            errors.Add("証憑ファイルを1件以上指定してください。");
            return errors;
        }

        if (attachments.Count > 5)
        {
            errors.Add("証憑ファイルは最大5件まで指定できます。");
        }

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.Filename))
            {
                errors.Add("ファイル名が空の証憑ファイルがあります。");
                continue;
            }

            if (attachment.Size <= 0)
            {
                errors.Add($"{attachment.Filename}: ファイルサイズが0です。");
            }

            if (attachment.Size > MaxEvidenceFileSizeBytes)
            {
                errors.Add($"{attachment.Filename}: ファイルサイズは10MB以下にしてください。");
            }
        }

        return errors;
    }
}
