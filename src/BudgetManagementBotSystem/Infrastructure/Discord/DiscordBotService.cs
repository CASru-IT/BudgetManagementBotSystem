using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
        _interactions = new InteractionService(_client);

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
            await _interactions.ExecuteCommandAsync(ctx, _provider);
        };

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }
}
