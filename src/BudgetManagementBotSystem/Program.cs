using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.Options;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Discord;
using BudgetManagementBotSystem.InfraStructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.FileStorage;
using BudgetManagementBotSystem.Infrastructure.Persistence;
using BudgetManagementBotSystem.Infrastructure.Persistence.Repository;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Db");
var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

builder.Services.AddDbContext<BudgetManagementDbContext>(options =>
{
    if (useInMemoryDatabase)
    {
        options.UseInMemoryDatabase("BudgetManagementBotSystem");
        return;
    }

    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IGroupRepository, EfCoreGroupRepository>();
builder.Services.AddScoped<IUserRepository, EfCoreUserRepository>();
builder.Services.AddScoped<RegisterGroupUseCase>();
builder.Services.AddScoped<RegisterUserUseCase>();
builder.Services.AddScoped<BootstrapAdminUseCase>();
builder.Services.AddScoped<SubmitBudgetRequestUseCase>();
builder.Services.AddScoped<IncreaseBudgetLimitUseCase>();
builder.Services.AddScoped<CancelBudgetRequestUseCase>();
builder.Services.AddScoped<UserCancelBudgetRequestUseCase>();
builder.Services.AddScoped<ApproveBudgetRequestUseCase>();
builder.Services.AddScoped<RejectBudgetRequestUseCase>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.Configure<AdminBootstrapOptions>(builder.Configuration.GetSection("AdminBootstrap"));

builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddHostedService<Worker>();

var bot = builder.Build();
bot.Run();
