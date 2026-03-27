using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BudgetManagementBotSystem.InfraStructure.Discord;

public class DiscordBotService
{
    private readonly IServiceProvider _provider;
    private DiscordSocketClient _client = null!;
    private InteractionService _interactions = null!;

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

        await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider);

        //サーバーへのコマンドの登録
        _client.Ready += async () =>
        {
            await _interactions.RegisterCommandsGloballyAsync();
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
}
