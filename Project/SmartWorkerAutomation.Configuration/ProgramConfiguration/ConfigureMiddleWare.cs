using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Configuration.MiddleWare;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartWorkerAutomation.Configuration.ProgramConfiguration;

public static class ConfigureMiddleWare
{
    public static IServiceCollection ConfigureMiddlewares(this IServiceCollection services, IConfiguration configuration)
    {
        //Required For Global Error Handling Middleware
        services.AddProblemDetails();

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
            options.SuppressMapClientErrors = true;
        });

        services.Configure<CacheAppSettings>(configuration.GetSection("CacheAppSettings"));

        services.AddAuthentication("CustomTokenScheme")
            .AddScheme<AuthenticationSchemeOptions, CustomTokenAuthenticationHandler>("CustomTokenScheme", null);

        services.AddAuthorization(options =>
        {
            options.AddPolicy("UserPolicy", policy => policy.RequireClaim("UserType", "User"));

            options.AddPolicy("CustomerPolicy", policy => policy.RequireClaim("UserType", "Customer"));

            options.AddPolicy("GuestPolicy", policy => policy.RequireClaim("UserType", "Guest"));

            options.AddPolicy("UserOrCustomer", policy => policy.RequireAssertion(context =>
                    context.User.HasClaim(c =>
                        c.Type == "UserType" &&
                        (c.Value == "User" || c.Value == "Customer")
                    )
                ));
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
