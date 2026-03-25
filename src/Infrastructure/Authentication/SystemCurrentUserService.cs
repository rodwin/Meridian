using Application.Abstractions.Authentication;

namespace Infrastructure.Authentication;

/// <summary>
/// Returns null for all user context — used when ApplicationDbContext is created
/// directly (outside DI) for system operations such as outbox processing and migrations.
/// Audit fields CreatedBy/UpdatedBy will be null for rows written by the system.
/// </summary>
public sealed class SystemCurrentUserService : ICurrentUserService
{
    public static readonly SystemCurrentUserService Instance = new();

    public Guid? UserId => null;
}
