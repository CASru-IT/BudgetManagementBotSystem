using BudgetManagementBotSystem.InfraStructure.Discord;

public class Worker : BackgroundService
{
    private readonly DiscordBotService _bot;
    private readonly IConfiguration _config;

    public Worker(DiscordBotService bot, IConfiguration config)
    {
        _bot = bot;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _config["Discord:Token"] ?? throw new InvalidOperationException("Discord token is not configured");
        await _bot.StartAsync(token);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
