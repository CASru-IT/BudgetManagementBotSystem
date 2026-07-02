using Discord;

namespace BudgetManagementBotSystem.Presentation.Discord.Helpers;

public static class EvidenceAttachmentValidator
{
    private const long MaxEvidenceFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".pdf"
    };

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

        var duplicateNames = attachments
            .Where(a => !string.IsNullOrWhiteSpace(a.Filename))
            .GroupBy(a => a.Filename, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNames.Count > 0)
        {
            errors.Add($"同じファイル名は指定できません: {string.Join(", ", duplicateNames)}");
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

            var extension = Path.GetExtension(attachment.Filename);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                errors.Add($"{attachment.Filename}: 対応していないファイル形式です。使用可能: jpg / jpeg / png / webp / pdf");
            }
        }

        return errors;
    }
}
