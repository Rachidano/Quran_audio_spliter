using Microsoft.Extensions.DependencyInjection;
using Quran.Import.Services;

namespace Quran.Import;

public static class DependencyInjection
{
    public static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        services.AddScoped<QuranSeedService>();
        services.AddScoped<WarshAyahImportService>();
        services.AddScoped<AudioIngestionService>();
        services.AddHttpClient<Quran.Application.Interfaces.IArchiveOrgInventoryService, ArchiveOrgInventoryService>();
        return services;
    }
}
