using BudgetManagementBotSystem.Application.Interface;
using BudgetManagementBotSystem.Application.Options;
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
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = Host.CreateApplicationBuilder(args);

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");

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
builder.Services.AddScoped<ListGroupsUseCase>();
builder.Services.AddScoped<RegisterUserUseCase>();
builder.Services.AddScoped<BootstrapAdminUseCase>();
builder.Services.AddScoped<SubmitBudgetRequestUseCase>();
builder.Services.AddScoped<IncreaseBudgetLimitUseCase>();
builder.Services.AddScoped<AdminAddBudgetTransactionUseCase>();
builder.Services.AddScoped<CancelBudgetRequestUseCase>();
builder.Services.AddScoped<UserCancelBudgetRequestUseCase>();
builder.Services.AddScoped<ApproveBudgetRequestUseCase>();
builder.Services.AddScoped<RejectBudgetRequestUseCase>();
builder.Services.AddScoped<GetPendingRequestsUseCase>();
builder.Services.AddScoped<RequestQueryUseCase>();
builder.Services.AddScoped<RevokeApprovalUseCase>();
builder.Services.AddScoped<UserQueryUseCase>();
builder.Services.AddScoped<UserCommandUseCase>();
builder.Services.AddScoped<RequestListUseCase>();
builder.Services.AddScoped<RequestDetailUseCase>();
builder.Services.AddScoped<NotifyApprovedRequestUseCase>();
builder.Services.AddScoped<NotifyRejectedRequestUseCase>();
builder.Services.AddScoped<BudgetQueryUseCase>();
builder.Services.AddScoped<DiscordRequestNotificationService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.Configure<AdminBootstrapOptions>(builder.Configuration.GetSection("AdminBootstrap"));

builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddSingleton<PendingRequestConfirmationStore>();
builder.Services.AddHostedService<Worker>();

var bot = builder.Build();

if (!useInMemoryDatabase)
{
    using var scope = bot.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BudgetManagementDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

bot.Run();
