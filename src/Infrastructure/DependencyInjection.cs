using System.Text;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Scheduling;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.Scheduling;
using Infrastructure.Tenancy;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddServices()
            .AddTenancy(configuration)
            .AddDatabase()
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal()
            .AddCronValidation();

    public static IServiceCollection AddDomainEventDispatching(this IServiceCollection services)
    {
        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        return services;
    }

    public static IServiceCollection AddScheduling(this IServiceCollection services)
    {
        services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();

        return services;
    }

    private static IServiceCollection AddCronValidation(this IServiceCollection services)
    {
        services.AddSingleton<ICronExpressionValidator, QuartzCronExpressionValidator>();

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    private static IServiceCollection AddTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        string? routingDbConnectionString = configuration.GetConnectionString("routing-db");

        services.AddDbContext<RoutingDbContext>(options =>
            options.UseSqlServer(routingDbConnectionString));

        services.AddMemoryCache();
        services.AddSingleton<ITenantRegistry, DatabaseTenantRegistry>();

        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.Tenancy.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            string connectionString = sp.GetRequiredService<Application.Abstractions.Tenancy.ITenantContext>().ConnectionString;

            options
                .UseSqlServer(connectionString, sqlOptions =>
                    sqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.App))
                .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        string? routingDbConnectionString = configuration.GetConnectionString("routing-db");

        services
            .AddHealthChecks()
            .AddSqlServer(routingDbConnectionString!, name: "db-routing");

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddScoped<PermissionProvider>();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }
}
