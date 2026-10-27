using RoomTrack.IAM.Application.ACL.Services;
using RoomTrack.IAM.Application.Internal.Configuration;
using RoomTrack.IAM.Infrastructure.Notifications;
using RoomTrack.IAM.Infrastructure.Passwords;
using RoomTrack.IAM.Interfaces.REST.ExceptionHandling;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.ExceptionHandling;
using RoomTrack.IAM.Infrastructure.Tokens.Opaque;
using RoomTrack.IAM.Application.Internal.CommandServices;
using RoomTrack.IAM.Application.Internal.QueryServices;
using RoomTrack.IAM.Application.OutboundServices;
using RoomTrack.IAM.Domain.Repositories;
using RoomTrack.IAM.Domain.Services;
using RoomTrack.IAM.Infrastructure.Authentication;
using RoomTrack.IAM.Infrastructure.Hashing.BCrypt.Services;
using RoomTrack.IAM.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.IAM.Infrastructure.Seed;
using RoomTrack.IAM.Infrastructure.Tokens.JWT.Configuration;
using RoomTrack.IAM.Infrastructure.Tokens.JWT.Services;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.IAM.Interfaces.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace RoomTrack.IAM.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static void AddIamContextServices(this WebApplicationBuilder builder)
    {
        // Authentication and authorization are registered by AddRoomTrackServiceDefaults.
        builder.Services.AddOptions<InitialChainAdminSettings>()
            .Bind(builder.Configuration.GetSection(InitialChainAdminSettings.SectionName))
            // Legacy variable names kept for existing deployments.
            .PostConfigure(settings =>
            {
                if (string.IsNullOrWhiteSpace(settings.LoginEmail))
                    settings.Email = Environment.GetEnvironmentVariable("INITIAL_CHAIN_ADMIN_USERNAME");
                if (string.IsNullOrWhiteSpace(settings.Password))
                    settings.Password = Environment.GetEnvironmentVariable("INITIAL_CHAIN_ADMIN_PASSWORD");
            });

        // IAM Bounded Context Injection Configuration
        builder.Services.AddOptions<AccountSecuritySettings>()
            .Bind(builder.Configuration.GetSection(AccountSecuritySettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Password policy (NIST SP 800-63B-4) with the breached password lookup (HIBP range API, k-anonymity)
        builder.Services.AddOptions<PasswordPolicySettings>()
            .Bind(builder.Configuration.GetSection(PasswordPolicySettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddHttpClient<IBreachedPasswordChecker, PwnedPasswordsChecker>((services, client) =>
        {
            var settings = services.GetRequiredService<IOptions<PasswordPolicySettings>>().Value;
            client.BaseAddress = new Uri(settings.PwnedPasswordsApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.PwnedPasswordsTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RoomTrack-API/1.0");
        });
        builder.Services.AddScoped<NewPasswordValidator>();

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IAccountTokenRepository, AccountTokenRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IMfaRecoveryCodeRepository, MfaRecoveryCodeRepository>();
        builder.Services.AddScoped<SessionIssuer>();
        builder.Services.AddScoped<IMfaCommandService, MfaCommandService>();
        builder.Services.AddOptions<MfaSettings>()
            .Bind(builder.Configuration.GetSection(MfaSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddScoped<AccountTokenIssuer>();
        builder.Services.AddScoped<IAuthenticationCommandService, AuthenticationCommandService>();
        builder.Services.AddScoped<IUserCommandService, UserCommandService>();
        builder.Services.AddScoped<IUserQueryService, UserQueryService>();
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IHashingService, HashingService>();
        builder.Services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        builder.Services.AddScoped<IAccountNotificationService, AccountEmailNotificationService>();
        builder.Services.AddSingleton<IProblemDetailsEnricher, IamProblemDetailsEnricher>();
        builder.Services.AddScoped<IIamContextFacade, IamContextFacade>();

        builder.Services.AddScoped<IRoleAuthorizationService, RoleAuthorizationService>();
        builder.Services.AddScoped<IUserScopeService, UserScopeService>();
    }
}
