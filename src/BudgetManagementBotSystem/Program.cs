using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence.Repository;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Db");

builder.Services.AddDbContext<BudgetManagementDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IGroupRepository, EfCoreGroupRepository>();
builder.Services.AddScoped<IUserRepository, EfCoreUserRepository>();

builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddHostedService<Worker>();

var bot = builder.Build();
bot.Run();
