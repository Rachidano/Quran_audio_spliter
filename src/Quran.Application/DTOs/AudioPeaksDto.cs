namespace Quran.Application.DTOs;

public record AudioPeaksDto(
    string SourceHash,
    double DurationSeconds,
    double StepSeconds,
    IReadOnlyList<float> Peaks,
    string Status
);
