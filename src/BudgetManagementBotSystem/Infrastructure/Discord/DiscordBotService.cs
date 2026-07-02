using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using BudgetManagementBotSystem.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace BudgetManagementBotSystem.InfraStructure.Discord;

public class DiscordBotService
{
    private readonly IServiceProvider _provider;
    private DiscordSocketClient _client = null!;
    private InteractionService _interactions = null!;
    private readonly HttpClient _httpClient = new HttpClient();

    public DiscordBotService(IServiceProvider provider)
    {
        _provider = provider;
    }

    public async Task StartAsync(string token)
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent
        };

        _client = new DiscordSocketClient(config);
        _interactions = new InteractionService(_client, new InteractionServiceConfig
        {
            AutoServiceScopes = true,
            EnableAutocompleteHandlers = true,
            DefaultRunMode = RunMode.Async
        });

        _client.Log += m => { Console.WriteLine(m); return Task.CompletedTask; };
        _interactions.Log += m => { Console.WriteLine(m); return Task.CompletedTask; };

        await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider);

        _client.Ready += async () =>
        {
            try
            {
                var configuration = _provider.GetService<IConfiguration>();
                var guildIdStr = configuration?["Discord:TestGuildId"];
                if (!string.IsNullOrWhiteSpace(guildIdStr) && ulong.TryParse(guildIdStr, out var guildId))
                {
                    Console.WriteLine($"Registering commands to test guild {guildId}");
                    await _interactions.RegisterCommandsToGuildAsync(guildId);
                    Console.WriteLine("Registered commands to test guild.");
                }
                else
                {
                    Console.WriteLine("Registering commands globally (may take up to an hour to appear)");
                    await _interactions.RegisterCommandsGloballyAsync();
                    Console.WriteLine("Registered commands globally.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command registration failed: {ex}");
            }
        };

        _client.InteractionCreated += async interaction =>
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            try
            {
                var result = await _interactions.ExecuteCommandAsync(ctx, _provider);
                if (!result.IsSuccess)
                {
                    Console.WriteLine($"Interaction execution failed: {result.Error} / {result.ErrorReason}");
                    if (!interaction.HasResponded)
                    {
                        await interaction.RespondAsync("コマンド実行中にエラーが発生しました。", ephemeral: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception while executing interaction: {ex}");
                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync("内部エラーが発生しました。", ephemeral: true);
                }
            }
        };

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    /// <summary>
    /// 指定ユーザーが同一チャンネルに添付ファイルを含むメッセージを送るのを待ち、添付ファイルを保存してパスを返します。
    /// タイムアウト時は空リストを返します。
    /// </summary>
    public async Task<List<UploadedEvidenceDto>> WaitForAttachmentUploadAsync(ulong userId, TimeSpan timeout, int expectedAttachmentCount, IMessageChannel? channel = null)
    {
        if (expectedAttachmentCount < 1)
        {
            return new List<UploadedEvidenceDto>();
        }

        var tcs = new TaskCompletionSource<List<UploadedEvidenceDto>>();
        var uploads = new List<UploadedEvidenceDto>();
        var receivedCount = 0;
        var syncRoot = new object();

        Task Handler(SocketMessage msg)
        {
            if (msg.Author.Id != userId) return Task.CompletedTask;
            if (channel != null && msg.Channel.Id != channel.Id) return Task.CompletedTask;
            if (msg.Attachments == null || msg.Attachments.Count == 0) return Task.CompletedTask;

            return HandleAttachmentMessageAsync(msg);
        }

        async Task HandleAttachmentMessageAsync(SocketMessage msg)
        {
            foreach (var att in msg.Attachments)
            {
                var uploaded = await DownloadAttachmentAsync(att);

                if (uploaded == null)
                {
                    continue;
                }

                lock (syncRoot)
                {
                    if (receivedCount >= expectedAttachmentCount)
                    {
                        break;
                    }

                    uploads.Add(uploaded);
                    receivedCount++;

                    if (receivedCount >= expectedAttachmentCount)
                    {
                        tcs.TrySetResult(new List<UploadedEvidenceDto>(uploads));
                    }
                }
            }
        }

        _client.MessageReceived += Handler;

        try
        {
            var delay = Task.Delay(timeout);
            var completed = await Task.WhenAny(tcs.Task, delay);

            if (completed == tcs.Task)
            {
                return await tcs.Task;
            }

            return new List<UploadedEvidenceDto>();
        }
        finally
        {
            _client.MessageReceived -= Handler;
        }
    }

    public async Task<UploadedEvidenceDto?> DownloadAttachmentAsync(IAttachment attachment)
    {
        try
        {
            using var response = await _httpClient.GetAsync(attachment.Url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync();
            return new UploadedEvidenceDto(attachment.Filename, content);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> SendDirectMessageAsync(ulong userId, string message)
    {
        try
        {
            var (user, _) = await GetUserForDirectMessageAsync(userId);
            if (user == null)
            {
                return false;
            }

            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendDirectMessageAsync(ulong userId, Embed embed)
    {
        try
        {
            var (user, _) = await GetUserForDirectMessageAsync(userId);
            if (user == null)
            {
                return false;
            }

            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(embed: embed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DirectMessageSendResult> TestDirectMessageAsync(ulong userId, Embed embed)
    {
        if (_client == null)
        {
            return DirectMessageSendResult.Failure(
                "Bot クライアントが初期化されていません。",
                "DiscordBotService.StartAsync が完了していない可能性があります。");
        }

        var (user, userLookupException) = await GetUserForDirectMessageAsync(userId);
        if (user == null)
        {
            if (userLookupException is global::Discord.Net.HttpException httpException)
            {
                return DirectMessageSendResult.Failure(
                    BuildDiscordUserLookupFailureMessage(httpException),
                    "Discord API からユーザー取得失敗が返されました。",
                    httpException.GetType().FullName,
                    httpException.HttpCode.ToString(),
                    httpException.DiscordCode?.ToString(),
                    httpException.Reason);
            }

            if (userLookupException != null)
            {
                return DirectMessageSendResult.Failure(
                    "Discord ユーザーを取得できませんでした。",
                    userLookupException.Message,
                    userLookupException.GetType().FullName);
            }

            return DirectMessageSendResult.Failure(
                "Discord ユーザーを取得できませんでした。",
                "Bot のユーザーキャッシュと Discord API のどちらからも対象ユーザーを取得できませんでした。DiscordUserId が正しいか確認してください。");
        }

        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(embed: embed);
            return DirectMessageSendResult.Success();
        }
        catch (global::Discord.Net.HttpException ex)
        {
            return DirectMessageSendResult.Failure(
                BuildDiscordHttpFailureMessage(ex),
                "Discord API から DM 送信失敗が返されました。",
                ex.GetType().FullName,
                ex.HttpCode.ToString(),
                ex.DiscordCode?.ToString(),
                ex.Reason);
        }
        catch (Exception ex)
        {
            return DirectMessageSendResult.Failure(
                "DM チャンネル作成またはメッセージ送信中に例外が発生しました。",
                ex.Message,
                ex.GetType().FullName);
        }
    }

    private async Task<(IUser? User, Exception? Exception)> GetUserForDirectMessageAsync(ulong userId)
    {
        var cachedUser = _client.GetUser(userId);
        if (cachedUser != null)
        {
            return (cachedUser, null);
        }

        try
        {
            return (await _client.Rest.GetUserAsync(userId), null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private static string BuildDiscordUserLookupFailureMessage(global::Discord.Net.HttpException ex)
    {
        if ((int?)ex.DiscordCode == 10013)
        {
            return "DiscordUserId に該当する Discord ユーザーが見つかりませんでした。";
        }

        if ((int)ex.HttpCode == 404)
        {
            return "Discord API がユーザー未検出を返しました。DiscordUserId が正しいか確認してください。";
        }

        return "Discord API へのユーザー取得リクエストが失敗しました。";
    }

    private static string BuildDiscordHttpFailureMessage(global::Discord.Net.HttpException ex)
    {
        if ((int?)ex.DiscordCode == 50007)
        {
            return "対象ユーザーが DM を受け取れません。プライバシー設定、Bot のブロック、共通サーバー設定の影響が考えられます。";
        }

        if ((int)ex.HttpCode == 403)
        {
            return "Discord API が 403 Forbidden を返しました。対象ユーザーまたはサーバー側の権限・プライバシー設定が原因の可能性があります。";
        }

        return "Discord API への DM 送信リクエストが失敗しました。";
    }
}

public sealed record DirectMessageSendResult(
    bool IsSuccess,
    string Summary,
    string Detail,
    string? ExceptionType = null,
    string? HttpCode = null,
    string? DiscordCode = null,
    string? DiscordReason = null)
{
    public static DirectMessageSendResult Success()
    {
        return new DirectMessageSendResult(true, "DM を送信できました。", "対象ユーザーへのテスト DM 送信に成功しました。");
    }

    public static DirectMessageSendResult Failure(
        string summary,
        string detail,
        string? exceptionType = null,
        string? httpCode = null,
        string? discordCode = null,
        string? discordReason = null)
    {
        return new DirectMessageSendResult(false, summary, detail, exceptionType, httpCode, discordCode, discordReason);
    }
}
