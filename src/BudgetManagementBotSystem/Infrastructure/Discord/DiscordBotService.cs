using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
        //インテントの管理
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
        };

        _client = new DiscordSocketClient(config);
        _interactions = new InteractionService(_client, new InteractionServiceConfig
        {
            AutoServiceScopes = true,
            DefaultRunMode = RunMode.Async
        });

        _client.Log += m => { Console.WriteLine(m); return Task.CompletedTask; };
        _interactions.Log += m => { Console.WriteLine(m); return Task.CompletedTask; };

        await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider);

        //サーバーへのコマンドの登録
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

        //コマンドが呼び出されたときのイベントハンドラーの登録
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
    /// 指定ユーザーが同一チャンネルに添付ファイルを含むメッセージを送るのを待ち、添付ファイルを一時保存してパスを返します。
    /// タイムアウト時は空リストを返します。
    /// </summary>
    public async Task<List<string>> WaitForAttachmentUploadAsync(ulong userId, TimeSpan timeout, IMessageChannel? channel = null)
    {
        var tcs = new TaskCompletionSource<List<string>>();

        Task Handler(SocketMessage msg)
        {
            if (msg.Author.Id != userId) return Task.CompletedTask;
            if (channel != null && msg.Channel.Id != channel.Id) return Task.CompletedTask;
            if (msg.Attachments == null || msg.Attachments.Count == 0) return Task.CompletedTask;

            _ = Task.Run(async () =>
            {
                var paths = new List<string>();
                foreach (var att in msg.Attachments)
                {
                    try
                    {
                        var ext = Path.GetExtension(att.Filename);
                        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
                        var tempFile = Path.Combine(Path.GetTempPath(), $"evidence_{Guid.NewGuid():N}{ext}");
                        using var resp = await _httpClient.GetAsync(att.Url);
                        resp.EnsureSuccessStatusCode();
                        await using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                        await (await resp.Content.ReadAsStreamAsync()).CopyToAsync(fs);
                        paths.Add(tempFile);
                    }
                    catch
                    {
                        // skip failed attachment
                    }
                }
                tcs.TrySetResult(paths);
            });

            return Task.CompletedTask;
        }

        _client.MessageReceived += Handler;

        var delay = Task.Delay(timeout);
        var completed = await Task.WhenAny(tcs.Task, delay);
        _client.MessageReceived -= Handler;

        if (completed == tcs.Task)
        {
            return await tcs.Task;
        }
        else
        {
            return new List<string>();
        }
    }
}
