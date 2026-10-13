using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuranVideo.Desktop.Data;
using QuranVideo.Desktop.Models;
using QuranVideo.Desktop.Services;

namespace QuranVideo.Desktop.ViewModels;

public sealed class CollectionItemViewModel : ObservableObject
{
    public int ItemId { get; set; }
    public int ProjectId { get; set; }
    public int OrderIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public string StatusBadge => IsReady ? "✔ جاهز" : "⚠ غير مكتمل";
    public string StatusColor => IsReady ? "#15803d" : "#b91c1c";
}

public sealed partial class CollectionsViewModel : ViewModelBase
{
    private readonly WorkspaceService _workspaceService;
    private readonly CompositionService _compositionService;
    private readonly SequenceValidationService _validationService;
    private CancellationTokenSource? _batchCts;

    [ObservableProperty]
    private ObservableCollection<CollectionEntity> _collections = new();

    [ObservableProperty]
    private CollectionEntity? _selectedCollection;

    [ObservableProperty]
    private ObservableCollection<CollectionItemViewModel> _currentCollectionItems = new();

    [ObservableProperty]
    private ObservableCollection<VideoProjectEntity> _availableProjectsToAdd = new();

    [ObservableProperty]
    private VideoProjectEntity? _selectedProjectToAdd;

    [ObservableProperty]
    private string _newCollectionName = string.Empty;

    [ObservableProperty]
    private string _newCollectionDescription = string.Empty;

    [ObservableProperty]
    private bool _isCreatingCollection;

    [ObservableProperty]
    private bool _isRenamingCollection;

    [ObservableProperty]
    private string _renamedCollectionName = string.Empty;

    [ObservableProperty]
    private bool _isExportingBatch;

    [ObservableProperty]
    private int _batchExportProgress;

    [ObservableProperty]
    private string _batchStatusMessage = string.Empty;

    [ObservableProperty]
    private string? _batchErrorMessage;

    [ObservableProperty]
    private int _currentBatchItemIndex;

    [ObservableProperty]
    private int _totalBatchItemsCount;

    public bool HasCollections => Collections.Count > 0;
    public bool HasNoCollections => Collections.Count == 0;
    public bool HasItems => CurrentCollectionItems.Count > 0;
    public bool HasNoItems => CurrentCollectionItems.Count == 0;

    public CollectionsViewModel(
        WorkspaceService workspaceService,
        CompositionService? compositionService = null,
        SequenceValidationService? validationService = null)
    {
        _workspaceService = workspaceService;
        _compositionService = compositionService ?? new CompositionService(workspaceService);
        _validationService = validationService ?? new SequenceValidationService(workspaceService);

        _ = LoadCollectionsAsync();
    }

    partial void OnSelectedCollectionChanged(CollectionEntity? value)
    {
        _ = LoadCurrentCollectionItemsAsync();
    }

    [RelayCommand]
    public async Task LoadCollectionsAsync()
    {
        Collections.Clear();
        if (string.IsNullOrWhiteSpace(_workspaceService.WorkspacePath)) return;

        var dbPath = _workspaceService.GetDatabasePath();
        if (!File.Exists(dbPath)) return;

        using var db = new AppDbContext(dbPath);
        var list = await db.Collections
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync();

        foreach (var c in list)
        {
            Collections.Add(c);
        }

        OnPropertyChanged(nameof(HasCollections));
        OnPropertyChanged(nameof(HasNoCollections));

        if (SelectedCollection == null || !Collections.Any(c => c.Id == SelectedCollection.Id))
        {
            SelectedCollection = Collections.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task LoadCurrentCollectionItemsAsync()
    {
        CurrentCollectionItems.Clear();
        AvailableProjectsToAdd.Clear();

        if (SelectedCollection == null)
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(HasNoItems));
            return;
        }

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        var items = await db.CollectionItems
            .Include(i => i.Project)
            .ThenInclude(p => p!.SelectedAyahs)
            .Where(i => i.CollectionId == SelectedCollection.Id)
            .OrderBy(i => i.OrderIndex)
            .ToListAsync();

        foreach (var item in items)
        {
            var p = item.Project;
            bool isReady = false;
            string title = p?.Title ?? $"مشروع #{item.ProjectId}";
            string details = string.Empty;

            if (p != null)
            {
                var surah = SurahCatalogService.GetSurah(p.SurahNumber);
                string surahName = surah?.NameArabic ?? $"سورة {p.SurahNumber}";
                details = $"{surahName} (الآيات {p.StartAyahNumber} ➔ {p.EndAyahNumber}) • {p.Orientation}";

                // Validate readiness with complete SequenceValidationService
                var valResult = await _validationService.ValidateProjectAsync(p, checkPhysicalFiles: true);
                isReady = valResult.IsValid;
            }

            CurrentCollectionItems.Add(new CollectionItemViewModel
            {
                ItemId = item.Id,
                ProjectId = item.ProjectId,
                OrderIndex = item.OrderIndex,
                Title = title,
                Details = details,
                IsReady = isReady
            });
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasNoItems));

        // Load available projects to add
        var existingProjectIds = items.Select(i => i.ProjectId).ToHashSet();
        var allProjects = await db.Projects
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        foreach (var p in allProjects)
        {
            if (!existingProjectIds.Contains(p.Id))
            {
                AvailableProjectsToAdd.Add(p);
            }
        }

        SelectedProjectToAdd = AvailableProjectsToAdd.FirstOrDefault();
    }

    [RelayCommand]
    public void StartCreateCollection()
    {
        NewCollectionName = string.Empty;
        NewCollectionDescription = string.Empty;
        IsCreatingCollection = true;
    }

    [RelayCommand]
    public void CancelCreateCollection()
    {
        IsCreatingCollection = false;
    }

    [RelayCommand]
    public async Task ConfirmCreateCollectionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCollectionName)) return;

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        var col = new CollectionEntity
        {
            Name = NewCollectionName.Trim(),
            Description = NewCollectionDescription?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Collections.Add(col);
        await db.SaveChangesAsync();

        IsCreatingCollection = false;
        await LoadCollectionsAsync();
        SelectedCollection = Collections.FirstOrDefault(c => c.Id == col.Id);
    }

    [RelayCommand]
    public void StartRenameCollection()
    {
        if (SelectedCollection == null) return;
        RenamedCollectionName = SelectedCollection.Name;
        IsRenamingCollection = true;
    }

    [RelayCommand]
    public void CancelRenameCollection()
    {
        IsRenamingCollection = false;
    }

    [RelayCommand]
    public async Task ConfirmRenameCollectionAsync()
    {
        if (SelectedCollection == null || string.IsNullOrWhiteSpace(RenamedCollectionName)) return;

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        var existing = await db.Collections.FindAsync(SelectedCollection.Id);
        if (existing != null)
        {
            existing.Name = RenamedCollectionName.Trim();
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        IsRenamingCollection = false;
        var selectedId = SelectedCollection.Id;
        await LoadCollectionsAsync();
        SelectedCollection = Collections.FirstOrDefault(c => c.Id == selectedId);
    }

    [RelayCommand]
    public async Task DeleteCollectionAsync()
    {
        if (SelectedCollection == null) return;

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        var existing = await db.Collections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == SelectedCollection.Id);

        if (existing != null)
        {
            // V3-07-03: Remove only the collection and item links, preserving all projects and media!
            db.CollectionItems.RemoveRange(existing.Items);
            db.Collections.Remove(existing);
            await db.SaveChangesAsync();
        }

        SelectedCollection = null;
        await LoadCollectionsAsync();
    }

    [RelayCommand]
    public async Task AddProjectToCollectionAsync()
    {
        if (SelectedCollection == null || SelectedProjectToAdd == null) return;

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        int maxOrder = await db.CollectionItems
            .Where(i => i.CollectionId == SelectedCollection.Id)
            .Select(i => (int?)i.OrderIndex)
            .MaxAsync() ?? 0;

        var item = new CollectionItemEntity
        {
            CollectionId = SelectedCollection.Id,
            ProjectId = SelectedProjectToAdd.Id,
            OrderIndex = maxOrder + 1,
            AddedAtUtc = DateTime.UtcNow
        };

        db.CollectionItems.Add(item);
        await db.SaveChangesAsync();

        await LoadCurrentCollectionItemsAsync();
    }

    [RelayCommand]
    public async Task RemoveItemFromCollectionAsync(CollectionItemViewModel? itemVm)
    {
        if (itemVm == null || SelectedCollection == null) return;

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        // V3-07-03: Remove item from collection without touching project entity or media files
        var existing = await db.CollectionItems.FindAsync(itemVm.ItemId);
        if (existing != null)
        {
            db.CollectionItems.Remove(existing);
            await db.SaveChangesAsync();
        }

        await LoadCurrentCollectionItemsAsync();
    }

    [RelayCommand]
    public async Task MoveItemUpAsync(CollectionItemViewModel? itemVm)
    {
        if (itemVm == null || SelectedCollection == null) return;
        int idx = CurrentCollectionItems.IndexOf(itemVm);
        if (idx <= 0) return;

        var prev = CurrentCollectionItems[idx - 1];

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        var curEntity = await db.CollectionItems.FindAsync(itemVm.ItemId);
        var prevEntity = await db.CollectionItems.FindAsync(prev.ItemId);

        if (curEntity != null && prevEntity != null)
        {
            int temp = curEntity.OrderIndex;
            curEntity.OrderIndex = prevEntity.OrderIndex;
            prevEntity.OrderIndex = temp;
            await db.SaveChangesAsync();
        }

        await LoadCurrentCollectionItemsAsync();
    }

    [RelayCommand]
    public async Task MoveItemDownAsync(CollectionItemViewModel? itemVm)
    {
        if (itemVm == null || SelectedCollection == null) return;
        int idx = CurrentCollectionItems.IndexOf(itemVm);
        if (idx < 0 || idx >= CurrentCollectionItems.Count - 1) return;

        var next = CurrentCollectionItems[idx + 1];

        var dbPath = _workspaceService.GetDatabasePath();
        using var db = new AppDbContext(dbPath);

        var curEntity = await db.CollectionItems.FindAsync(itemVm.ItemId);
        var nextEntity = await db.CollectionItems.FindAsync(next.ItemId);

        if (curEntity != null && nextEntity != null)
        {
            int temp = curEntity.OrderIndex;
            curEntity.OrderIndex = nextEntity.OrderIndex;
            nextEntity.OrderIndex = temp;
            await db.SaveChangesAsync();
        }

        await LoadCurrentCollectionItemsAsync();
    }

    [RelayCommand]
    public async Task ExportCollectionBatchAsync()
    {
        if (SelectedCollection == null || CurrentCollectionItems.Count == 0) return;

        // V3-07-05: Pre-validate entire selection before launching. Any error blocks whole batch!
        var dbPath = _workspaceService.GetDatabasePath();
        using (var db = new AppDbContext(dbPath))
        {
            var projectIds = CurrentCollectionItems.Select(i => i.ProjectId).ToList();
            var projects = await db.Projects
                .Include(p => p.SelectedAyahs)
                .Where(p => projectIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var validationErrors = new List<string>();
            foreach (var item in CurrentCollectionItems)
            {
                if (!projects.TryGetValue(item.ProjectId, out var project))
                {
                    item.IsReady = false;
                    validationErrors.Add($"المشروع #{item.ProjectId} ({item.Title}) غير موجود.");
                    continue;
                }

                var val = await _validationService.ValidateProjectAsync(project, checkPhysicalFiles: true);
                item.IsReady = val.IsValid;
                if (!val.IsValid)
                {
                    var errSummary = string.Join(" | ", val.GetAllErrorMessages());
                    validationErrors.Add($"المشروع ({item.Title}): {errSummary}");
                }
            }

            if (validationErrors.Count > 0)
            {
                BatchErrorMessage = $"تعذر بدء التصدير: تحتوي المجموعة على عناصر غير جاهزة ({validationErrors.Count} عنصر). يرجى مراجعة وتجهيز كافة المقاطع أولاً:\n" + string.Join("\n", validationErrors);
                return;
            }
        }

        IsExportingBatch = true;
        BatchErrorMessage = null;
        BatchExportProgress = 0;
        TotalBatchItemsCount = CurrentCollectionItems.Count;
        CurrentBatchItemIndex = 0;
        _batchCts = new CancellationTokenSource();

        try
        {
            // V3-07-04: Export each item into its own distinct file; never merge the collection!
            for (int i = 0; i < CurrentCollectionItems.Count; i++)
            {
                if (_batchCts.IsCancellationRequested) break;

                var item = CurrentCollectionItems[i];
                CurrentBatchItemIndex = i + 1;
                BatchStatusMessage = $"جارٍ تصدير العنصر {CurrentBatchItemIndex} من {TotalBatchItemsCount}: {item.Title}...";

                var itemProgress = new Progress<int>(pct =>
                {
                    double basePct = (i * 100.0) / TotalBatchItemsCount;
                    double currPct = (pct * 1.0) / TotalBatchItemsCount;
                    BatchExportProgress = (int)(basePct + currPct);
                });

                var (ok, err, _) = await _compositionService.RenderProjectAsync(item.ProjectId, itemProgress, _batchCts.Token);

                // V3-07-06: Error handling without false success
                if (!ok)
                {
                    BatchErrorMessage = $"فشل تصدير العنصر ({item.Title}): {err}";
                    IsExportingBatch = false;
                    return;
                }
            }

            if (_batchCts.IsCancellationRequested)
            {
                BatchStatusMessage = "تم إلغاء التصدير بالدفعة بناءً على طلب المستخدم.";
            }
            else
            {
                BatchExportProgress = 100;
                BatchStatusMessage = $"اكتمل تصدير جميع عناصر المجموعة بنجاح ({TotalBatchItemsCount} ملفات فيديو مستقلة).";
            }
        }
        catch (OperationCanceledException)
        {
            BatchStatusMessage = "تم إلغاء التصدير بالدفعة.";
        }
        catch (Exception ex)
        {
            BatchErrorMessage = $"حدث خطأ غير متوقع أثناء تصدير المجموعة: {ex.Message}";
        }
        finally
        {
            IsExportingBatch = false;
            _batchCts = null;
        }
    }

    [RelayCommand]
    public void CancelBatchExport()
    {
        _batchCts?.Cancel();
    }
}
