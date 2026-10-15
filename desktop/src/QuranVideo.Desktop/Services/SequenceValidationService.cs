using System.IO;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;

namespace QuranVideo.Desktop.Services;

public class SequenceItemValidation
{
    public int SurahNumber { get; set; }
    public int AyahNumber { get; set; }
    public string AyahId { get; set; } = string.Empty;

    public AyahPassageEntity? ChosenPassage { get; set; }
    public List<AyahPassageEntity> AvailableTakes { get; set; } = new();

    public AyahImageRecipeEntity? ImageRecipe { get; set; }
    public string? ImagePath { get; set; }

    public bool HasGapWithPrevious { get; set; }
    public double GapSeconds { get; set; }
    public bool JunctionReviewed { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public bool IsValid => Errors.Count == 0;
}

public class SequenceValidationResult
{
    public bool IsValid => GlobalErrors.Count == 0 && Items.All(i => i.IsValid);
    public bool HasJunctionWarnings => Items.Any(i => i.HasGapWithPrevious && !i.JunctionReviewed);

    public List<SequenceItemValidation> Items { get; set; } = new();
    public List<string> GlobalErrors { get; set; } = new();
    public List<string> GlobalWarnings { get; set; } = new();

    public List<string> GetAllErrorMessages()
    {
        var list = new List<string>(GlobalErrors);
        foreach (var item in Items)
        {
            list.AddRange(item.Errors);
        }
        return list;
    }
}

public sealed class SequenceValidationService
{
    private readonly WorkspaceService _workspaceService;

    public SequenceValidationService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    public async Task<SequenceValidationResult> ValidateProjectAsync(
        VideoProjectEntity project,
        bool checkPhysicalFiles = true,
        CancellationToken cancellationToken = default)
    {
        var chosenTakes = project.SelectedAyahs?
            .ToDictionary(sa => sa.AyahNumber, sa => sa.PassageId);

        var reviewedJunctions = project.SelectedAyahs?
            .Where(sa => sa.JunctionReviewed)
            .Select(sa => sa.AyahNumber)
            .ToHashSet() ?? new HashSet<int>();

        var result = await ValidateSequenceAsync(
            project.SurahNumber,
            project.StartAyahNumber,
            project.EndAyahNumber,
            project.ReciterId,
            project.AudioSourceId,
            chosenTakes,
            checkPhysicalFiles,
            reviewedJunctions,
            cancellationToken);

        // 0. Vérifier que toutes les ayahs de l'intervalle sont présentes et ont un passage valide
        for (int a = project.StartAyahNumber; a <= project.EndAyahNumber; a++)
        {
            var sa = project.SelectedAyahs?.FirstOrDefault(x => x.AyahNumber == a);
            if (sa == null || sa.PassageId <= 0)
            {
                result.GlobalErrors.Add($"تعذّر التصدير: المقطع الصوتي للآية {a} غير محدد في المشروع.");
            }
        }

        // 1. Contrôle de la fidélité des révisions et des bornes enregistrées
        if (project.SelectedAyahs != null && project.SelectedAyahs.Count > 0)
        {
            var projectAyahMap = project.SelectedAyahs.ToDictionary(a => a.AyahNumber);
            foreach (var item in result.Items)
            {
                if (projectAyahMap.TryGetValue(item.AyahNumber, out var sa))
                {
                    if (item.ChosenPassage != null)
                    {
                        if (sa.PassageRevision > 0 && item.ChosenPassage.Revision != sa.PassageRevision)
                        {
                            item.Errors.Add($"تعذّر التصدير: تم تعديل مراجعة المقطع الصوتي للآية {item.AyahNumber} في قاعدة البيانات بعد حفظ المشروع، ويتطلب مراجعة جديدة.");
                        }
                        else if (Math.Abs(item.ChosenPassage.StartTimeSec - sa.StartTimeSec) > 0.05 ||
                                 Math.Abs(item.ChosenPassage.EndTimeSec - sa.EndTimeSec) > 0.05)
                        {
                            item.Errors.Add($"تعذّر التصدير: تم تغيير توقيت المقطع الصوتي للآية {item.AyahNumber} بعد حفظ المشروع، ويتطلب إعادة التحقق.");
                        }
                    }

                    if (item.ImageRecipe != null)
                    {
                        if (sa.ImageRevision > 0 && item.ImageRecipe.Revision != sa.ImageRevision)
                        {
                            item.Errors.Add($"تعذّر التصدير: تم تعديل مراجعة صورة الآية {item.AyahNumber} (المراجعة {item.ImageRecipe.Revision} بدلاً من {sa.ImageRevision}) بعد حفظ المشروع، وتتطلب مراجعة جديدة.");
                        }
                        else if (sa.ImageRecipeId > 0 && item.ImageRecipe.Id != sa.ImageRecipeId)
                        {
                            item.Errors.Add($"تعذّر التصدير: تم تغيير وصفة صورة الآية {item.AyahNumber} بعد حفظ المشروع، وتتطلب مراجعة جديدة.");
                        }
                    }
                }
            }
        }

        // 2. Contrôle des ressources graphiques activées (fond personnalisé et logo)
        if (project.Background == BackgroundType.CustomImage)
        {
            if (string.IsNullOrWhiteSpace(project.CustomBackgroundPath))
            {
                result.GlobalErrors.Add("تعذّر التصدير: تم تفعيل خلفية مخصصة لكن مسار الصورة غير محدد.");
            }
            else if (checkPhysicalFiles && !File.Exists(_workspaceService.ToAbsolutePath(project.CustomBackgroundPath)))
            {
                result.GlobalErrors.Add("تعذّر التصدير: تم تفعيل خلفية مخصصة لكن ملف الصورة غير موجود على القرص.");
            }
        }

        if (project.Watermark == WatermarkType.Logo && project.IsWatermarkEnabled)
        {
            if (string.IsNullOrWhiteSpace(project.WatermarkImagePath))
            {
                result.GlobalErrors.Add("تعذّر التصدير: تم تفعيل شعار مخصص لكن مسار ملف الشعار غير محدد.");
            }
            else if (checkPhysicalFiles && !File.Exists(_workspaceService.ToAbsolutePath(project.WatermarkImagePath)))
            {
                result.GlobalErrors.Add("تعذّر التصدير: تم تفعيل شعار مخصص لكن ملف الشعار غير موجود على القرص.");
            }
        }

        return result;
    }

    public async Task<SequenceValidationResult> ValidateSequenceAsync(
        int surahNumber,
        int startAyahNumber,
        int endAyahNumber,
        string reciterId,
        string? audioSourceId = null,
        Dictionary<int, int>? selectedPassageIds = null,
        bool checkPhysicalFiles = false,
        HashSet<int>? reviewedJunctions = null,
        CancellationToken cancellationToken = default)
    {
        var result = new SequenceValidationResult();

        // 1. Validation de la sourate et des limites du corpus Warsh local
        var surah = SurahCatalogService.GetSurah(surahNumber);
        if (surah == null)
        {
            result.GlobalErrors.Add($"تعذّر التصدير: رقم السورة ({surahNumber}) غير صحيح.");
            return result;
        }

        if (startAyahNumber < 1 || endAyahNumber > surah.AyahCount)
        {
            result.GlobalErrors.Add(
                $"تعذّر التصدير: نطاق الآيات المحدد ({startAyahNumber} إلى {endAyahNumber}) خارج حدود سورة {surah.NameArabic} (1 إلى {surah.AyahCount}).");
            return result;
        }

        if (startAyahNumber > endAyahNumber)
        {
            result.GlobalErrors.Add(
                $"تعذّر التصدير: آية البداية ({startAyahNumber}) يجب أن تسبق أو تساوي آية النهاية ({endAyahNumber}).");
            return result;
        }

        var dbPath = _workspaceService.GetDatabasePath();
        if (!File.Exists(dbPath))
        {
            result.GlobalErrors.Add("تعذّر التصدير: قاعدة بيانات القرآن الكريم غير متوفرة.");
            return result;
        }

        using var db = new AppDbContext(dbPath);

        // 2. Résolution et validation de la source audio unique
        AudioSourceEntity? audioSource = null;
        if (!string.IsNullOrWhiteSpace(audioSourceId))
        {
            audioSource = await db.AudioSources
                .Include(s => s.Collection)
                .FirstOrDefaultAsync(s => s.Id == audioSourceId, cancellationToken);
        }
        else
        {
            audioSource = await db.AudioSources
                .Include(s => s.Collection)
                .FirstOrDefaultAsync(s => s.SurahNumber == surahNumber && (s.Collection != null && s.Collection.ReciterId == reciterId), cancellationToken);
        }

        if (audioSource == null)
        {
            result.GlobalErrors.Add(
                $"تعذّر التصدير: الملف الصوتي لسورة {surah.NameArabic} للقارئ المحدد غير متوفر محلياً.");
            return result;
        }

        // Vérification de la correspondance du récitateur
        if (audioSource.Collection != null && !string.IsNullOrWhiteSpace(reciterId) &&
            !string.Equals(audioSource.Collection.ReciterId, reciterId, StringComparison.OrdinalIgnoreCase))
        {
            result.GlobalErrors.Add(
                $"تعذّر التصدير: القارئ المحدد للمشروع ({reciterId}) لا يطابق قارئ التسجيل الصوتي ({audioSource.Collection.ReciterId}).");
        }

        var resolvedAudioPath = Path.Combine(_workspaceService.WorkspacePath, audioSource.RelativeFilePath);
        if (checkPhysicalFiles)
        {
            if (!File.Exists(resolvedAudioPath))
            {
                result.GlobalErrors.Add(
                    $"تعذّر التصدير: الملف الصوتي ({audioSource.RelativeFilePath}) غير موجود على القرص.");
                return result;
            }

            bool audioValid = false;
            try
            {
                var fi = new FileInfo(resolvedAudioPath);
                if (fi.Length >= 100)
                {
                    using var reader = new NAudio.Wave.AudioFileReader(resolvedAudioPath);
                    audioValid = reader.TotalTime.TotalSeconds > 0;
                }
            }
            catch
            {
                audioValid = false;
            }

            if (!audioValid)
            {
                result.GlobalErrors.Add(
                    $"تعذّر التصدير: الملف الصوتي ({audioSource.RelativeFilePath}) تالف أو غير صالح للتشغيل.");
                return result;
            }
        }

        // 3. Construction stricte et sans omission de chaque verset de l'intervalle [startAyahNumber .. endAyahNumber]
        for (int ayah = startAyahNumber; ayah <= endAyahNumber; ayah++)
        {
            var item = new SequenceItemValidation
            {
                SurahNumber = surahNumber,
                AyahNumber = ayah,
                AyahId = $"{surahNumber}:{ayah}",
                JunctionReviewed = reviewedJunctions != null && reviewedJunctions.Contains(ayah)
            };

            // a. Récupération des prises audio
            var allPassages = await db.Passages
                .Where(p => p.SurahNumber == surahNumber && p.AyahNumber == ayah && p.AudioSourceId == audioSource.Id)
                .OrderBy(p => p.Revision)
                .ToListAsync(cancellationToken);

            item.AvailableTakes = allPassages;

            // Filtrer les prises complètes
            var fullTakes = allPassages
                .Where(p => p.Kind == PassageKind.Full || p.Kind == PassageKind.FullRetake)
                .ToList();

            if (fullTakes.Count == 0)
            {
                // Vérifier s'il n'y a que des prises partielles
                var hasPartial = allPassages.Any(p => p.Kind == PassageKind.PartialRetake || p.Kind == PassageKind.Portion);
                if (hasPartial)
                {
                    item.Errors.Add(
                        $"تعذّر التصدير: المقطع الصوتي للآية {ayah} عبارة عن استدراك جزئي ولا يمثل آية كاملة.");
                }
                else
                {
                    item.Errors.Add(
                        $"تعذّر التصدير: المقطع الصوتي للآية {ayah} غير محدد. يجب استكمال جميع الآيات من {startAyahNumber} إلى {endAyahNumber} قبل التصدير.");
                }
            }
            else
            {
                // Sélection de la prise
                AyahPassageEntity? chosen = null;
                if (selectedPassageIds != null && selectedPassageIds.TryGetValue(ayah, out int chosenId))
                {
                    chosen = fullTakes.FirstOrDefault(t => t.Id == chosenId);
                    if (chosen == null)
                    {
                        var requestedTake = allPassages.FirstOrDefault(t => t.Id == chosenId);
                        if (requestedTake != null && (requestedTake.Kind == PassageKind.PartialRetake || requestedTake.Kind == PassageKind.Portion))
                        {
                            item.Errors.Add(
                                $"تعذّر التصدير: المقطع الصوتي المختار للآية {ayah} عبارة عن استدراك جزئي ولا يمثل آية كاملة.");
                        }
                        else
                        {
                            item.Errors.Add(
                                $"تعذّر التصدير: رقم المقطع الصوتي المحدد ({chosenId}) للآية {ayah} غير صالح.");
                        }
                    }
                }
                else
                {
                    // Si plusieurs prises existent sans sélection explicite, exiger un choix explicite
                    if (fullTakes.Count > 1)
                    {
                        item.Errors.Add(
                            $"تعذّر التصدير: توجد عدة تسجيلات صوتية كاملة للآية {ayah}، يجب تحديد التسجيل المراد استخدامه صراحة.");
                    }
                    else
                    {
                        chosen = fullTakes.First();
                    }
                }

                if (chosen != null)
                {
                    item.ChosenPassage = chosen;

                    // Contrôle strict des bornes temporelles
                    if (chosen.StartTimeSec < 0 || chosen.EndTimeSec <= chosen.StartTimeSec)
                    {
                        item.Errors.Add(
                            $"تعذّر التصدير: التوقيت الزمني للآية {ayah} غير صالح ({chosen.StartTimeSec:F2}s إلى {chosen.EndTimeSec:F2}s).");
                    }
                    else if (audioSource.DurationSeconds > 0 && chosen.EndTimeSec > audioSource.DurationSeconds + 0.05)
                    {
                        item.Errors.Add(
                            $"تعذّر التصدير: نهاية المقطع للآية {ayah} ({chosen.EndTimeSec:F2}s) تتجاوز المدة الكلية للملف الصوتي ({audioSource.DurationSeconds:F2}s).");
                    }
                }
            }

            // b. Récupération et contrôle de l'image de l'ayah
            var recipe = await db.ImageRecipes
                .FirstOrDefaultAsync(r => r.AyahId == item.AyahId, cancellationToken);

            item.ImageRecipe = recipe;
            if (recipe == null)
            {
                item.Errors.Add(
                    $"تعذّر التصدير: لم يتم استخراج صورة الآية {ayah} بعد.");
            }
            else
            {
                var fullImgPath = Path.Combine(_workspaceService.WorkspacePath, recipe.RelativeImagePath);
                item.ImagePath = fullImgPath;

                if (checkPhysicalFiles)
                {
                    if (!File.Exists(fullImgPath))
                    {
                        item.Errors.Add(
                            $"تعذّر التصدير: ملف صورة الآية {ayah} غير موجود على القرص ({recipe.RelativeImagePath}).");
                    }
                    else
                    {
                        bool imgValid = false;
                        try
                        {
                            var fi = new FileInfo(fullImgPath);
                            if (fi.Length >= 50)
                            {
                                using var stream = File.OpenRead(fullImgPath);
                                using var img = System.Drawing.Image.FromStream(stream, false, false);
                                imgValid = img.Width > 0 && img.Height > 0;
                            }
                        }
                        catch
                        {
                            imgValid = false;
                        }

                        if (!imgValid)
                        {
                            item.Errors.Add(
                                $"تعذّر التصدير: ملف صورة الآية {ayah} تالف أو غير صالح ({recipe.RelativeImagePath}).");
                        }
                    }
                }
            }

            result.Items.Add(item);
        }

        // 4. Contrôle de la continuité temporelle entre prises consécutives
        for (int i = 0; i < result.Items.Count - 1; i++)
        {
            var cur = result.Items[i];
            var next = result.Items[i + 1];

            if (cur.ChosenPassage != null && next.ChosenPassage != null)
            {
                // Vérification du chevauchement temporel réel (intersection des intervalles audio)
                bool hasActualOverlap = Math.Max(cur.ChosenPassage.StartTimeSec, next.ChosenPassage.StartTimeSec) <
                                       Math.Min(cur.ChosenPassage.EndTimeSec, next.ChosenPassage.EndTimeSec) - 0.05;

                if (hasActualOverlap)
                {
                    next.Errors.Add(
                        $"تعذّر التصدير: يوجد تداخل زمني بين الآية {cur.AyahNumber} والآية {next.AyahNumber} ({next.ChosenPassage.StartTimeSec:F2}s < {cur.ChosenPassage.EndTimeSec:F2}s).");
                }
                else if (next.ChosenPassage.StartTimeSec >= cur.ChosenPassage.EndTimeSec - 0.05)
                {
                    // Vérification de l'intervalle / pause en cas d'enregistrement consécutif
                    var gap = next.ChosenPassage.StartTimeSec - cur.ChosenPassage.EndTimeSec;
                    if (gap > 0.15)
                    {
                        next.HasGapWithPrevious = true;
                        next.GapSeconds = gap;
                        if (!next.JunctionReviewed)
                        {
                            next.Errors.Add(
                                $"تعذّر التصدير: يوجد فاصل زمني ({gap:F1} ثانية) بين الآية {cur.AyahNumber} والآية {next.AyahNumber} يتطلب المراجعة والتأكيد قبل التصدير.");
                        }
                        else
                        {
                            next.Warnings.Add(
                                $"تنبيه: تم تأكيد الفاصل الزمني ({gap:F1} ثانية) بين الآية {cur.AyahNumber} والآية {next.AyahNumber}.");
                        }
                    }
                }
            }
        }

        return result;
    }
}
