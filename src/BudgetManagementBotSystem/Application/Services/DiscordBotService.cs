using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Reflection;

namespace BudgetManagementBotSystem.Application.Services;

public class DiscordBotService
{
    private readonly IServiceProvider _provider;
    private DiscordSocketClient _client = null!;
    private CommandService _commands = null!;

    public DiscordBotService(IServiceProvider provider)
    {
        _provider = provider;
    }

    public async Task StartAsync(string token)
    {
        _client = new DiscordSocketClient();
        _commands = new CommandService();

        _client.Log += m => { Console.WriteLine(m); return Task.CompletedTask; };

        await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider);

        _client.MessageReceived += async msg =>
        {
            if (msg is not SocketUserMessage m) return;
            int pos = 0;
            if (!m.HasCharPrefix('!', ref pos)) return;

            var ctx = new SocketCommandContext(_client, m);
            await _commands.ExecuteAsync(ctx, pos, _provider);
        };

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }
}
