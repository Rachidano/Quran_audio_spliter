using Microsoft.Extensions.DependencyInjection;
using Quran.Application.Interfaces;
using Quran.AudioProcessor.Services;

namespace Quran.AudioProcessor;

public static class DependencyInjection
{
    public static IServiceCollection AddAudioProcessorServices(this IServiceCollection services)
    {
        services.AddSingleton<IAudioProcessorService, FfmpegAudioProcessor>();
        return services;
    }
}
