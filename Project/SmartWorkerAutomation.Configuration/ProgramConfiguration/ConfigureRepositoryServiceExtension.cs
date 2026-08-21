using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.Core.Security;
using SmartWorkerAutomation.DataProvider.Automation;
using SmartWorkerAutomation.DataProvider.Interface;
using SmartWorkerAutomation.DataProvider.Interface.Automation;
using SmartWorkerAutomation.DataProvider.Service;
using SmartWorkerAutomation.DataProvider.Service.Automation;


namespace SmartWorkerAutomation.Configuration.ProgramConfiguration;

public static class ConfigureRepositoryServiceExtension
{
    public static IServiceCollection ConfigureRepositoryService(this IServiceCollection services, IConfiguration configuration)
    {
        //services.AddDbContextPool<SmartWorkerAutomationContext>(options =>
        //{
        //    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        //    options.EnableSensitiveDataLogging(false);
        //    options.EnableDetailedErrors(false);
        //});


        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
        services.AddSingleton<ILogServices, LogServices>();
        services.AddSingleton<ConnectionStringEncryptor>();

        services.AddSingleton<MasterDbConnectionFactory>();
        services.AddScoped<DbConnectionFactory>();
        services.AddSingleton<IQueryStore, QueryStore>();
        //services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMasterAuthRepository, MasterAuthRepository>();
        services.AddScoped<ITenantResolverService, TenantResolverService>();
        services.AddSingleton<ITokenEncryptionService, TokenEncryptionService>();
        services.AddScoped<IBranchService, BranchService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IInquiryService, InquiryService>();
        services.AddScoped<IRecordsExportService, RecordsExportService>();
        services.AddScoped<IRecordsImportValidationService, RecordsImportValidationService>();
        services.AddScoped<IFileIngestionService, FileIngestionService>();
        services.AddScoped<IStagingReviewService, StagingReviewService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReplyReviewService, ReplyReviewService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationsService, NotificationsService>();
        services.AddScoped<IWhatsAppInboundService, WhatsAppInboundService>();
        //services.AddScoped<ITenantResolverService, TenantResolverService>();
        services.AddScoped<IOrganisationOnboardingService, OrganisationOnboardingService>();
        services.AddScoped<ICustomerEnquiryService, CustomerEnquiryService>();
        services.AddScoped<ICustomerEnquiryImportService, CustomerEnquiryImportService>();

        services.AddHttpClient<IWhatsAppService, WhatsAppService>();
        services.AddHttpClient<IReplyClassificationService, ReplyClassificationService>();
        services.AddHttpClient<IFirebasePushService, FirebasePushService>();
        services.AddHttpClient<GmailClient>();



        services.AddMemoryCache();

        return services;
    }
}
