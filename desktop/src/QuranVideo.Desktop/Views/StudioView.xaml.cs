using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuranVideo.Desktop.ViewModels;

namespace QuranVideo.Desktop.Views;

public partial class StudioView : UserControl
{
    public StudioView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown += OnWindowPreviewKeyDown;
        }

        if (DataContext is StudioViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.RecordedPassages.CollectionChanged -= OnRecordedPassagesChanged;
            vm.RecordedPassages.CollectionChanged += OnRecordedPassagesChanged;
            _ = vm.LoadSavedProjectsAsync();
            _ = vm.RefreshWizardSequenceAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.RecordedPassages.CollectionChanged -= OnRecordedPassagesChanged;
        }
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown -= OnWindowPreviewKeyDown;
        }
    }

    // L06-08: Raccourcis clavier protégés contre la saisie dans un champ texte
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't intercept shortcuts when user is typing in a TextBox
        if (e.OriginalSource is TextBox) return;

        if (DataContext is not StudioViewModel vm) return;

        // Fenêtre « تهيئة الفيديو » ouverte : Échap la ferme, et aucun raccourci du studio ne doit
        // agir en arrière-plan (Entrée enregistrerait sinon la fin de l'ayah)
        if (vm.IsVideoSettingsOpen)
        {
            if (e.Key == Key.Escape)
            {
                vm.CloseVideoSettingsCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Space)
        {
            vm.TogglePlayPauseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F7)
        {
            vm.SetStartFromCursorCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F8 || e.Key == Key.Enter)
        {
            _ = vm.SaveEndAndAdvanceCommand.ExecuteAsync(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F9)
        {
            _ = vm.SaveRetakeCommand.ExecuteAsync(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _ = vm.UndoLastSaveCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    // Tableau des segments : faire défiler jusqu'à la ligne de l'ayah en cours (sinon la sélection est invisible)
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StudioViewModel.CurrentAyahNumber)) ScrollCurrentAyahIntoView();
    }

    private void OnRecordedPassagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        ScrollCurrentAyahIntoView();

    private void ScrollCurrentAyahIntoView()
    {
        // Après la mise en page, quand les lignes du tableau existent
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (DataContext is not StudioViewModel vm) return;
            var passage = vm.RecordedPassages.FirstOrDefault(p => p.AyahNumber == vm.CurrentAyahNumber);
            if (passage != null && PassagesList.ItemContainerGenerator.ContainerFromItem(passage) is FrameworkElement row)
                row.BringIntoView();
        });
    }

    // Saisie d'un temps (معلومات المقطع) : Entrée valide la valeur, Échap l'annule ; puis le studio
    // reprend le focus pour que F7 / F8 / Espace fonctionnent de nouveau immédiatement
    private void OnTimeTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || (e.Key != Key.Enter && e.Key != Key.Escape)) return;

        var binding = box.GetBindingExpression(TextBox.TextProperty);
        if (e.Key == Key.Enter) binding?.UpdateSource();
        else binding?.UpdateTarget();

        Keyboard.ClearFocus();
        Focus();
        e.Handled = true;
    }

    // Clic sur le fond assombri (hors de la fenêtre) : fermer la fenêtre de réglages
    private void OnVideoSettingsBackdropMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, VideoSettingsBackdrop) && DataContext is StudioViewModel vm)
        {
            vm.CloseVideoSettingsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnToggleAdvancedDrawerClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            vm.ToggleAdvancedDrawerCommand.Execute(null);
        }
    }
}
