using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using Discord;
using Moq;

namespace BudgetManagementBotSystem.Tests.Presentation.Discord;

public class EvidenceAttachmentValidatorTests
{
    [Fact]
    public void Validate_WithUnsupportedExtension_DoesNotReturnError()
    {
        var attachments = new[]
        {
            CreateAttachment("archive.zip", 1024),
            CreateAttachment("spreadsheet.csv", 1024),
            CreateAttachment("document.exe", 1024)
        };

        var errors = EvidenceAttachmentValidator.Validate(attachments);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithDuplicateFileNames_DoesNotReturnError()
    {
        var attachments = new[]
        {
            CreateAttachment("receipt.pdf", 1024),
            CreateAttachment("receipt.pdf", 2048)
        };

        var errors = EvidenceAttachmentValidator.Validate(attachments);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithEmptyFile_ReturnsError()
    {
        var attachments = new[] { CreateAttachment("empty.pdf", 0) };

        var errors = EvidenceAttachmentValidator.Validate(attachments);

        Assert.Contains(errors, error => error.Contains("ファイルサイズが0です。"));
    }

    [Fact]
    public void Validate_WithFileOverLimit_ReturnsError()
    {
        var attachments = new[] { CreateAttachment("large.pdf", 10 * 1024 * 1024 + 1) };

        var errors = EvidenceAttachmentValidator.Validate(attachments);

        Assert.Contains(errors, error => error.Contains("ファイルサイズは10MB以下にしてください。"));
    }

    private static IAttachment CreateAttachment(string fileName, int size)
    {
        var attachment = new Mock<IAttachment>();
        attachment.SetupGet(a => a.Filename).Returns(fileName);
        attachment.SetupGet(a => a.Size).Returns(size);
        attachment.SetupGet(a => a.ContentType).Returns("application/octet-stream");
        return attachment.Object;
    }
}
