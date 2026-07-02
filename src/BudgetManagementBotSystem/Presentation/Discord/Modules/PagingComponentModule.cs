using BudgetManagementBotSystem.Application.DTOs;
using BudgetManagementBotSystem.Application.UseCases.Budget;
using BudgetManagementBotSystem.Application.UseCases.RequestWorkflow;
using BudgetManagementBotSystem.Presentation.Discord.Helpers;
using BudgetManagementBotSystem.Presentation.Discord.Models;
using BudgetManagementBotSystem.Presentation.Discord.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BudgetManagementBotSystem.Presentation.Discord.Modules;

public class PagingComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly PagingSessionStore _pagingSessionStore;
    private readonly BudgetQueryUseCase _budgetQueryUseCase;
    private readonly RequestListUseCase _requestListUseCase;
    private readonly GetPendingRequestsUseCase _getPendingRequestsUseCase;
    private readonly ILogger<PagingComponentModule> _logger;

    public PagingComponentModule(
        PagingSessionStore pagingSessionStore,
        BudgetQueryUseCase budgetQueryUseCase,
        RequestListUseCase requestListUseCase,
        GetPendingRequestsUseCase getPendingRequestsUseCase,
        ILogger<PagingComponentModule> logger)
    {
        _pagingSessionStore = pagingSessionStore;
        _budgetQueryUseCase = budgetQueryUseCase;
        _requestListUseCase = requestListUseCase;
        _getPendingRequestsUseCase = getPendingRequestsUseCase;
        _logger = logger;
    }

    [ComponentInteraction("page:*:*")]
    public async Task HandlePaging(string action, string token)
    {
        if (!_pagingSessionStore.TryGet(token, out var session) || session == null)
        {
            await RespondAsync(
                embed: DiscordEmbedFactory.BuildWarningEmbed(
                    "ページ操作の有効期限が切れています",
                    "もう一度コマンドを実行してください。"),
                ephemeral: true);
            return;
        }

        if (session.OwnerDiscordUserId != Context.User.Id)
        {
            await RespondAsync(
                embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("このページングボタンを操作できるのは、一覧を表示した本人のみです。"),
                ephemeral: true);
            return;
        }

        try
        {
            session.Page = action switch
            {
                "prev" => Math.Max(1, session.Page - 1),
                "next" => session.Page + 1,
                "refresh" => session.Page,
                _ => session.Page
            };

            var rendered = await RenderAsync(session);
            if (session.Page != rendered.Page)
            {
                session.Page = rendered.Page;
                rendered = await RenderAsync(session);
            }

            _pagingSessionStore.Update(session);

            if (Context.Interaction is IComponentInteraction componentInteraction)
            {
                await componentInteraction.UpdateAsync(message =>
                {
                    message.Embed = rendered.Embed;
                    message.Components = rendered.Components;
                });
                return;
            }

            await RespondAsync(embed: rendered.Embed, components: rendered.Components, ephemeral: true);
        }
        catch (UnauthorizedAccessException)
        {
            await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("この一覧を確認する権限がありません。"), ephemeral: true);
        }
        catch (ArgumentException)
        {
            await RespondAsync(embed: DiscordEmbedFactory.BuildAuthorizationErrorEmbed("Discordユーザーがシステムに登録されていません。管理者に登録を依頼してください。"), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to handle paging. DiscordUserId: {DiscordUserId}, Token: {Token}, Target: {Target}, Action: {Action}, Page: {Page}, PageSize: {PageSize}, GroupId: {GroupId}, FiscalYear: {FiscalYear}, Status: {Status}",
                Context.User.Id,
                token,
                session.Target,
                action,
                session.Page,
                session.PageSize,
                session.GroupId,
                session.FiscalYear,
                session.Status);

            await RespondAsync(embed: DiscordEmbedFactory.BuildErrorEmbed("ページ操作を完了できません", "時間を置いて再実行してください。"), ephemeral: true);
        }
    }

    private async Task<RenderedPage> RenderAsync(PagingSession session)
    {
        return session.Target switch
        {
            PagingTarget.UsageHistory => await RenderUsageHistoryAsync(session),
            PagingTarget.RequestList => await RenderRequestListAsync(session),
            PagingTarget.PendingList => await RenderPendingListAsync(session),
            _ => throw new InvalidOperationException($"Unsupported paging target: {session.Target}")
        };
    }

    private async Task<RenderedPage> RenderUsageHistoryAsync(PagingSession session)
    {
        var result = await _budgetQueryUseCase.GetUsageHistoryAsync(
            Context.User.Id,
            session.Page,
            session.PageSize,
            session.GroupId,
            session.FiscalYear);

        return BuildRenderedPage(
            result,
            page => DiscordEmbedFactory.BuildUsageHistoryEmbed(page, session.FiscalYear),
            session);
    }

    private async Task<RenderedPage> RenderRequestListAsync(PagingSession session)
    {
        var result = await _requestListUseCase.ExecuteAsync(
            Context.User.Id,
            session.Status,
            session.Page,
            session.PageSize,
            session.GroupId);

        return BuildRenderedPage(
            result,
            page => DiscordEmbedFactory.BuildRequestListEmbed(page, session.Status),
            session);
    }

    private async Task<RenderedPage> RenderPendingListAsync(PagingSession session)
    {
        var result = await _getPendingRequestsUseCase.ExecuteAsync(
            Context.User.Id,
            session.Page,
            session.PageSize);

        return BuildRenderedPage(
            result,
            DiscordEmbedFactory.BuildPendingRequestsEmbed,
            session);
    }

    private static RenderedPage BuildRenderedPage<T>(
        PagedResult<T> result,
        Func<PagedResult<T>, Embed> embedFactory,
        PagingSession session)
    {
        var totalPages = CalculateTotalPages(result.Total, result.PageSize);
        var page = Math.Min(Math.Max(1, result.Page), totalPages);

        if (page != result.Page)
        {
            return new RenderedPage(embedFactory(result), null, page);
        }

        session.Page = result.Page;
        session.PageSize = result.PageSize;

        var components = totalPages > 1
            ? DiscordComponentFactory.BuildPagingComponents(session.Token, result.Page, totalPages)
            : null;

        return new RenderedPage(embedFactory(result), components, result.Page);
    }

    private static int CalculateTotalPages(int total, int pageSize)
    {
        return pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
    }

    private sealed record RenderedPage(Embed Embed, MessageComponent? Components, int Page);
}
