using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using SmartWorkerAutomation.API.BackgroundServices;
using SmartWorkerAutomation.Configuration.MiddleWare;
using SmartWorkerAutomation.Configuration.ProgramConfiguration;
using SmartWorkerAutomation.DataProvider.Interface;
using SmartWorkerAutomation.DataProvider.Service;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

public class Program
{
    public static void Main(string[] args)
    {

        var builder = WebApplication.CreateBuilder(args);

        var logDirectory = Path.Combine("wwwroot", "SmartWorkerAutomationLogs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Error()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
            .MinimumLevel.Override("System", LogEventLevel.Fatal)
            // Root is Error-only by design - only failures get logged,
            // no per-cycle/per-message informational noise. The temporary
            // per-service Information override used while debugging inbound
            // email capture has been removed now that
            // InboundEmailBackgroundService no longer emits LogInformation.
            .Enrich.WithProperty("Application", "SmartWorkerAutomationAPI")
            .WriteTo.File(
                Path.Combine(logDirectory, "SmartWorkerAutomationLogs-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10_000_000,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        builder.Host.UseSerilog();

        // Every hand-written SQL statement used by the Automation (SmartWorker) side
        // lives in Config/Queries.json, not inline in C# - see
        // SmartWorkerAutomation.Core/Repository/Automation/IQueryStore.cs.
        // reloadOnChange means editing a query takes effect on next request, no
        // rebuild/redeploy needed.
        builder.Configuration.AddJsonFile("Config/Queries.json", optional: false, reloadOnChange: true);

        // Add services to the container.

        builder.Services.ConfigureRepositoryService(builder.Configuration);

        // SmartWorker (Automation) side - Dapper/Postgres-backed, independent of the
        // EF/SmartBill services registered above.
        //builder.Services.ConfigureAutomationServices();

        builder.Services.AddControllers(options =>
        {
            options.Filters.Add(typeof(ModelStateInvalidDetailsCaptureFilter));

        });

        builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

        builder.Services.AddSingleton<ILogServices, LogServices>();
        //builder.Services.AddTransient(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        builder.Services.ConfigureMiddlewares(builder.Configuration);

        // --- Automation (SmartWorker) hosted background services ---
        // Native replacements for the retired n8n workflows. See each class for details
        // on what it replaces and what config it still needs filled in
        // (OpenAI:ApiKey, Firebase:*, Gmail:*, Meta:WebhookVerifyToken/AppSecret).
        builder.Services.AddHostedService<DailyAutomationRefreshService>();
        builder.Services.AddHostedService<ReminderSendBackgroundService>();
        builder.Services.AddHostedService<ReconcileWhatsAppStatusBackgroundService>();
        builder.Services.AddHostedService<ReplyClassificationBackgroundService>();
        builder.Services.AddHostedService<InboundEmailBackgroundService>();

        // --- Automation CORS policy (Angular/Capacitor clients) ---
        const string AngularDevCorsPolicy = "AngularDevCorsPolicy";
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var defaultOrigins = new[]
        {
            "http://localhost:4200",       // local Angular dev server (web)
            "http://localhost:4201",       // local Angular dev server (mobile)
            "https://localhost",           // Capacitor Android (androidScheme: 'https')
            "capacitor://localhost",       // Capacitor iOS
        };
        var allowedOrigins = defaultOrigins.Concat(configuredOrigins).Distinct().ToArray();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(AngularDevCorsPolicy, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        // --- Automation JWT auth ---
        // NOTE: the SmartBill side of this app may already configure its own auth
        // scheme elsewhere. If so, this needs reconciling (e.g. named schemes)
        // rather than two calls to AddAuthentication - flagging for manual review.
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var jwtKeyValue = jwtSettings["Key"];
        if (!string.IsNullOrEmpty(jwtKeyValue))
        {
            var key = Encoding.UTF8.GetBytes(jwtKeyValue);
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });
        }
        // TODO: add "Jwt:Key", "Jwt:Issuer", "Jwt:Audience" to appsettings.json - see
        // appsettings.automation.reference.json. Registration above is skipped when
        // missing so the app can still start, but Automation auth won't work until set.

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartWorkerAutomation API", Version = "v1" });
            // Configure JWT Bearer for Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter 'Bearer' followed by a space and your token."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    new string[] {}
                }
            });
        });

        var app = builder.Build();

        // Automation (SmartWorker) exception middleware, kept alongside the
        // existing app.UseExceptionHandler() below rather than replacing it.
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // Configure the HTTP request pipeline.
        //if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            await next();
        });

        app.UseExceptionHandler();

        // Behind a reverse proxy (Nginx Proxy Manager etc.), this app receives plain
        // HTTP inside the Docker network while the proxy terminates real HTTPS -
        // it forwards X-Forwarded-Proto so ASP.NET Core knows the original request
        // was HTTPS. Without this, UseHttpsRedirection() below can redirect-loop
        // through the proxy. Clearing KnownNetworks/KnownProxies is the standard
        // simplification for a single-VPS docker-compose setup where the proxy's
        // container IP isn't fixed - only the proxy is exposed publicly, so this
        // is safe there, but revisit if the deployment topology differs.
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseWebSockets();

        app.UseCors(AngularDevCorsPolicy);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}