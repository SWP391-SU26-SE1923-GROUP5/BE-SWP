using System.Text;
using AIStudyHub.Business.Options;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AIStudyHub.API.Extensions;

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        var externalAuthSection = configuration.GetSection("Authentication");
        var externalAuthOptions = externalAuthSection.Get<ExternalAuthOptions>() ?? new ExternalAuthOptions();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidAudience = jwtSection["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = externalAuthOptions.Google.ClientId;
                options.ClientSecret = externalAuthOptions.Google.ClientSecret;
                options.CallbackPath = "/signin-google";
            })
            .AddGitHub(GitHubAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = externalAuthOptions.GitHub.ClientId;
                options.ClientSecret = externalAuthOptions.GitHub.ClientSecret;
                options.CallbackPath = "/signin-github";
                options.Scope.Add("user:email");
            });

        services.AddAuthorization();

        return services;
    }
}
