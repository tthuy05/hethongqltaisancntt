using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ItAssetManagement.Api.Mvp;

public sealed record JwtSettings(byte[] Key, string Issuer, string Audience)
{
    public static JwtSettings Create(IConfiguration config, bool development)
    {
        var secret = config["Jwt:SigningKey"];
        var key = string.IsNullOrEmpty(secret) && development ? RandomNumberGenerator.GetBytes(32) :
            System.Text.Encoding.UTF8.GetBytes(secret ?? "");
        if (key.Length < 32) throw new InvalidOperationException("Production requires a JWT signing key of at least 32 bytes.");
        return new(key, "it-asset-management", "it-asset-management-ui");
    }
}
public sealed class TokenIssuer(JwtSettings settings) : ITokenIssuer
{
    public LoginResponse Issue(CurrentUser user, int tokenVersion)
    {
        var now = DateTime.UtcNow; var expires = now.AddMinutes(15);
        var claims = new List<Claim> { new("sub", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("email", user.Email), new("jti", Guid.NewGuid().ToString()), new("token_version", tokenVersion.ToString()),
            new("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) };
        claims.AddRange(user.Roles.Select(role => new Claim("role", role)));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expires,
            new SigningCredentials(new SymmetricSecurityKey(settings.Key), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires, user);
    }
}
public sealed class HttpActor(IHttpContextAccessor accessor) : IActor
{
    private readonly Guid _correlationId = Guid.NewGuid();
    public long? UserId => long.TryParse(accessor.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : null;
    public int? TokenVersion => int.TryParse(accessor.HttpContext?.User.FindFirstValue("token_version"), out var version) ? version : null;
    public Guid CorrelationId => _correlationId;
    public string? Method => accessor.HttpContext?.Request.Method;
    public string? Path => accessor.HttpContext?.Request.Path.Value;
    public bool Has(string permission) => accessor.HttpContext?.User.HasClaim("permission", permission) == true;
}
public static class AuthenticationSetup
{
    public static void AddMvpAuthentication(this IServiceCollection services)
    {
        services.AddSingleton(sp => JwtSettings.Create(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>().IsDevelopment()));
        services.AddSingleton<ITokenIssuer, TokenIssuer>();
        services.AddHttpContextAccessor(); services.AddScoped<IActor, HttpActor>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtSettings>((options, settings) =>
        {
            options.MapInboundClaims = false; options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(settings.Key),
                ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(15), RoleClaimType = "role", NameClaimType = "email"
            };
            options.Events = new()
            {
                OnTokenValidated = async context =>
                {
                    if (!long.TryParse(context.Principal?.FindFirstValue("sub"), out var id) || id <= 0 ||
                        !int.TryParse(context.Principal?.FindFirstValue("token_version"), out var version)) { context.Fail("Invalid account."); return; }
                    var user = await context.HttpContext.RequestServices.GetRequiredService<AuthService>().ValidateAsync(id, version, context.HttpContext.RequestAborted);
                    if (user == null) { context.Fail("Invalid account."); return; }
                    var identity = (ClaimsIdentity)context.Principal!.Identity!;
                    foreach (var claim in identity.FindAll("permission").Concat(identity.FindAll("role")).ToArray()) identity.RemoveClaim(claim);
                    identity.AddClaims(user.Permissions.Select(code => new Claim("permission", code)));
                    identity.AddClaims(user.Roles.Select(code => new Claim("role", code)));
                    context.HttpContext.Items[typeof(CurrentUser)] = user;
                },
                OnChallenge = async context => { context.HandleResponse(); context.Response.Headers.WWWAuthenticate = "Bearer"; await Errors.WriteAsync(context.HttpContext, 401, "UNAUTHORIZED", "Yêu cầu đăng nhập hợp lệ."); },
                OnForbidden = context => Errors.WriteAsync(context.HttpContext, 403, "FORBIDDEN", "Không có quyền thực hiện thao tác.")
            };
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in Permissions.All) options.AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireClaim("permission", permission));
        });
    }
}
