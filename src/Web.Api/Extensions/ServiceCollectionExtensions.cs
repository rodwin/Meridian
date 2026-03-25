using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;

namespace Web.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                const string schemeName = JwtBearerDefaults.AuthenticationScheme;

                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Enter your JWT Bearer token",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                };

                document.Components.SecuritySchemes[schemeName] = securityScheme;

                var schemeReference = new OpenApiSecuritySchemeReference(schemeName);

                foreach (var path in document.Paths.Values)
                {
                    if (path.Operations == null)
                    {
                        continue;
                    }

                    foreach (var operation in path.Operations.Values)
                    {
                        operation.Security ??= new List<OpenApiSecurityRequirement>();

                        operation.Security.Add(new OpenApiSecurityRequirement
                        {
                            [schemeReference] = new List<string>()
                        });
                    }
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
