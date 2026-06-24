using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace Hikidashi.Web;

/// <summary>
/// MCP endpoint auth as a pure resource server. A managed identity provider (e.g. WorkOS AuthKit or
/// Stytch) is the authorization server — it handles Dynamic Client Registration, the login (incl.
/// "Sign in with Google"), and token issuance. hikidashi only (1) validates the provider's JWT access
/// tokens and (2) advertises OAuth Protected Resource Metadata pointing at the provider, so the Claude
/// app can discover where to authenticate. No hand-rolled OAuth server.
/// </summary>
public static class McpAuthentication
{
    public const string Scope = "mcp";

    public static IServiceCollection AddMcpAuthentication(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        var authority =
            config["Auth:Authority"]
            ?? throw new InvalidOperationException(
                "Auth:Authority not configured (the identity provider's issuer URL)."
            );
        var audience =
            config["Auth:Audience"]
            ?? throw new InvalidOperationException(
                "Auth:Audience not configured (this MCP server's resource identifier)."
            );

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // Issuer discovery + JWKS signature validation come from the provider's authority.
                options.Authority = authority;
                options.Audience = audience;
            })
            .AddMcp(options =>
            {
                options.ResourceMetadata = new ProtectedResourceMetadata
                {
                    Resource = audience,
                    AuthorizationServers = { authority },
                    ScopesSupported = { Scope },
                    BearerMethodsSupported = { "header" },
                };
            });

        services.AddAuthorization(options =>
            options.AddPolicy(
                AuthSchemes.Mcp,
                policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                }
            )
        );

        return services;
    }
}
