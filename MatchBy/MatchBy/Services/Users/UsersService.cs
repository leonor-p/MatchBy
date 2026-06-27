// Services/Users/UsersService.cs
using MatchBy.Data;
using MatchBy.Enums;
using MatchBy.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchBy.Services.Users;

public class UsersService : IUsersService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UsersService> _logger;

    public UsersService(ApplicationDbContext context, ILogger<UsersService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApplicationUser?> GetUser(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Users                
                .Include(u => u.JoinedMatches)
                .Where(u => u.Id == userId &&
                           u.Status == AccountStatus.Available &&
                           u.DeletedAtUtc == null)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {UserId}", userId);
            return null;
        }
    }

    public async Task<Result<PaginationResponse<List<ApplicationUser>>>> GetUsers(
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.Users
                                .Include(u => u.JoinedMatches)
                .Where(u => u.Status == AccountStatus.Available && u.DeletedAtUtc == null)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    u.UserName!.ToLower().Contains(search) ||
                    u.DisplayName.ToLower().Contains(search) ||
                    (u.BaseLocation != null &&
                     (u.BaseLocation.City.ToLower().Contains(search) ||
                      u.BaseLocation.Country.ToLower().Contains(search)))
                );
            }

            var total = await query.CountAsync(cancellationToken);

            var users = await query
                .OrderBy(u => u.DisplayName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var response = new PaginationResponse<List<ApplicationUser>>
            {
                Data = users,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };

            return new Result<PaginationResponse<List<ApplicationUser>>>
            {
                Success = true,
                Data = response
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users. Search: {Search}, Page: {Page}", search, page);
            return new Result<PaginationResponse<List<ApplicationUser>>>
            {
                Success = false,
                ErrorMessages = new List<string> { "Error fetching users" }
            };
        }
    }
}