using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.Options;
using BudgetManagementBotSystem.Application.UseCases;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Application.UseCases.UserManagement;
using BudgetManagementBotSystem.Application.UseCases.Groups;
using BudgetManagementBotSystem.Application.UseCases.Budget;
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
builder.Services.AddScoped<DeleteGroupUseCase>();
builder.Services.AddScoped<RegisterUserUseCase>();
builder.Services.AddScoped<BootstrapAdminUseCase>();
builder.Services.AddScoped<SubmitBudgetRequestUseCase>();
builder.Services.AddScoped<IncreaseBudgetLimitUseCase>();
builder.Services.AddScoped<CancelBudgetRequestUseCase>();
builder.Services.AddScoped<UserCancelBudgetRequestUseCase>();
builder.Services.AddScoped<ApproveBudgetRequestUseCase>();
builder.Services.AddScoped<RejectBudgetRequestUseCase>();
builder.Services.AddScoped<GetPendingRequestsUseCase>();
builder.Services.AddScoped<RequestQueryUseCase>();
builder.Services.AddScoped<RevokeApprovalUseCase>();
builder.Services.AddScoped<BudgetManagementBotSystem.Application.UseCases.UserManagement.UserQueryUseCase>();
builder.Services.AddScoped<BudgetManagementBotSystem.Application.UseCases.UserManagement.UserCommandUseCase>();
builder.Services.AddScoped<RequestListUseCase>();
builder.Services.AddScoped<RequestDetailUseCase>();
builder.Services.AddScoped<BudgetManagementBotSystem.Application.UseCases.Budget.BudgetQueryUseCase>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.Configure<AdminBootstrapOptions>(builder.Configuration.GetSection("AdminBootstrap"));

builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddHostedService<Worker>();

var bot = builder.Build();
bot.Run();
