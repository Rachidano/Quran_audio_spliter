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
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
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

    private void OnWaveformSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is StudioViewModel vm && sender is Slider slider)
        {
            if (slider.IsMouseCaptureWithin || (slider.IsMouseOver && Mouse.LeftButton == MouseButtonState.Pressed))
            {
                vm.Seek(e.NewValue);
            }
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
