using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quran.Infrastructure.Persistence;

namespace Quran.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=quran_warsh_db;Username=quran_user;Password=quran_secure_pwd;";

        services.AddDbContext<QuranDbContext>(options =>
            options.UseNpgsql(connectionString, b => 
                b.MigrationsAssembly(typeof(QuranDbContext).Assembly.FullName)));

        services.AddScoped<Quran.Application.Interfaces.IQuranReaderService, Services.QuranReaderService>();
        services.AddSingleton<Quran.Application.Interfaces.IStorageService, Services.LocalStorageService>();
        services.AddScoped<Quran.Application.Interfaces.IAdminValidationService, Services.AdminValidationService>();
        services.AddScoped<Quran.Application.Interfaces.ICorpusCheckerService, Services.CorpusCheckerService>();
        services.AddScoped<Quran.Application.Interfaces.IAudioEditorService, Services.AudioEditorService>();
        services.AddScoped<Quran.Application.Interfaces.IMushafImageService, Services.MushafImageService>();
        services.AddScoped<Quran.Application.Interfaces.ICorpusResolutionService, Services.CorpusResolutionService>();
        services.AddScoped<Quran.Application.Interfaces.IAnnotationService, Services.AnnotationService>();
        services.AddScoped<Quran.Application.Interfaces.IAwjuhService, Services.AwjuhService>();
        services.AddScoped<Quran.Application.Interfaces.IAudioPeaksService, Services.AudioPeaksService>();
        services.AddScoped<Quran.Application.Interfaces.IQuranQualityAuditService, Services.QuranQualityAuditService>();

        return services;
    }
}
