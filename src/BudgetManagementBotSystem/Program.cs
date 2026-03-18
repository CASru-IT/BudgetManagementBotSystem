using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Db");

builder.Services.AddDbContext<BudgetManagementDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddHostedService<Worker>();

var bot = builder.Build();
bot.Run();
