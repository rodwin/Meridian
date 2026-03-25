using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.Register;

internal sealed partial class UserRegisteredDomainEventHandler(
    IApplicationDbContext dbContext,
    ITenantContext tenantContext,
    ILogger<UserRegisteredDomainEventHandler> logger) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        User? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == domainEvent.UserId, cancellationToken);

        if (user is null)
        {
            LogUserNotFound(logger, domainEvent.UserId);
            return;
        }

        LogUserFound(logger, tenantContext.TenantId, user.Id, user.Email);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "UserRegisteredDomainEventHandler: user {UserId} not found")]
    private static partial void LogUserNotFound(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "UserRegisteredDomainEventHandler: tenant {TenantId} - user {UserId} - {Email} found in database")]
    private static partial void LogUserFound(ILogger logger, string tenantId, Guid userId, string email);
}
