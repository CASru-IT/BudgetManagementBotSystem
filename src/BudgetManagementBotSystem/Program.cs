using BudgetManagementBotSystem.InfraStructure.Discord;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddHostedService<Worker>();

var bot = builder.Build();
bot.Run();
