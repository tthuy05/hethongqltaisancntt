using Microsoft.OpenApi;

namespace ItAssetManagement.Api.Mvp;

public static class MvpOpenApi
{
    public static void AddMvpOpenApi(this IServiceCollection services) => services.AddOpenApi(options =>
        options.AddDocumentTransformer((document, context, ct) =>
        {
            document.Info.Title = "IT Asset Management — M1 Development API";
            document.Info.Description = "Real API backed by shared Neon. Use demo records for writes. No production approval.";
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header };
            foreach (var path in document.Paths)
                foreach (var operation in path.Value.Operations ?? [])
                {
                    var isPublic = path.Key.StartsWith("/health/", StringComparison.Ordinal) || path.Key == "/api/v1/auth/login";
                    operation.Value.Security = isPublic ? [] :
                        [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
                }
            return Task.CompletedTask;
        }));
}
