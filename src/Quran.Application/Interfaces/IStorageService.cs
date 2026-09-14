namespace Quran.Application.Interfaces;

public interface IStorageService
{
    Task<string> SaveAudioAsync(Stream content, string relativeStorageKey, CancellationToken cancellationToken = default);
    Task<Stream?> GetAudioStreamAsync(string relativeStorageKey, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string relativeStorageKey, CancellationToken cancellationToken = default);
    string GetPublicUrl(string relativeStorageKey);
    string GetPhysicalPath(string relativeStorageKey);
}
