using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class UserRegisteredDomainEventHandler(
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
            logger.LogWarning("UserRegisteredDomainEventHandler: user {UserId} not found", domainEvent.UserId);
            return;
        }

        logger.LogInformation(
            "UserRegisteredDomainEventHandler: tenant {TenantId} - user {UserId} - {Email} found in database",
            tenantContext.TenantId,
            user.Id,
            user.Email);
    }
}

internal sealed class UserRegisteredDomainEventHandler2(
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
            logger.LogWarning("UserRegisteredDomainEventHandler2: user {UserId} not found", domainEvent.UserId);
            return;
        }

        logger.LogInformation(
            "UserRegisteredDomainEventHandler2: tenant {TenantId} - user {UserId} - {Email} found in database",
            tenantContext.TenantId,
            user.Id,
            user.Email);
    }
}
