using Microsoft.EntityFrameworkCore;
using BudgetManagementBotSystem.Domain.Entities;
using BudgetManagementBotSystem.Domain.Repository;
using BudgetManagementBotSystem.InfraStructure.Persistence;

namespace BudgetManagementBotSystem.Infrastructure.Persistence.Repository;

public class EfCoreUserRepository : IUserRepository
{
	private readonly BudgetManagementDbContext _context;

	public EfCoreUserRepository(BudgetManagementDbContext context)
	{
		_context = context;
	}

	public async Task<User?> GetByIdAsync(int userId)
	{
		return await _context.Users
			.FirstOrDefaultAsync(u => u.Id == userId);
	}

	public async Task<User?> GetByDiscordUserIdAsync(ulong discordUserId)
	{
		return await _context.Users
			.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId);
	}

	public async Task<bool> IsUserExistsAsync(int userId)
	{
		return await _context.Users
			.AnyAsync(u => u.Id == userId);
	}

	public async Task AddAsync(User user)
	{
		await _context.Users.AddAsync(user);
	}

	public async Task DeleteAsync(int userId)
	{
		var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
		if (user == null)
		{
			return;
		}

		_context.Users.Remove(user);
	}
}
