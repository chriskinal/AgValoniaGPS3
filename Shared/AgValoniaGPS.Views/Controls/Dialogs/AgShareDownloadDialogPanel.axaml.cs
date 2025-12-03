using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AgValoniaGPS.Services.AgShare;
using AgValoniaGPS.Models.AgShare;

namespace AgValoniaGPS.Views.Controls.Dialogs;

/// <summary>
/// View model for field list items in the download dialog
/// </summary>
public class DownloadFieldListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double AreaHa { get; set; }
    public string AreaDisplay => $"{AreaHa:F2} ha";
}

public partial class AgShareDownloadDialogPanel : UserControl
{
    private ObservableCollection<DownloadFieldListItem> _fields = new();
    private AgShareClient? _client;
    private AgShareDownloaderService? _downloaderService;
    private string _fieldsRootDirectory = string.Empty;
    private DownloadFieldListItem? _selectedField;

    public AgShareDownloadDialogPanel()
    {
        InitializeComponent();
        FieldListBox.ItemsSource = _fields;
        this.PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible)
        {
            _ = LoadFieldsAsync();
        }
    }

    private async Task LoadFieldsAsync()
    {
        _fields.Clear();
        BtnDownload.IsEnabled = false;
        BtnDownloadAll.IsEnabled = false;
        StatusLabel.Text = "Loading fields from AgShare...";
        StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#BDC3C7"));

        if (DataContext is not AgValoniaGPS.ViewModels.MainViewModel vm)
            return;

        // Get settings from ViewModel
        var serverUrl = vm.AgShareSettingsServerUrl;
        var apiKey = vm.AgShareSettingsApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            StatusLabel.Text = "Please configure AgShare settings first";
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
            return;
        }

        _client = new AgShareClient(
            string.IsNullOrEmpty(serverUrl) ? "https://agshare.agopengps.com" : serverUrl,
            apiKey);
        _downloaderService = new AgShareDownloaderService(_client);

        // Use FieldsRootDirectory if set, otherwise fallback to default
        _fieldsRootDirectory = vm.FieldsRootDirectory;
        if (string.IsNullOrWhiteSpace(_fieldsRootDirectory))
        {
            _fieldsRootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AgValoniaGPS", "Fields");
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[AgShare Download] Fetching fields from {(string.IsNullOrEmpty(serverUrl) ? "https://agshare.agopengps.com" : serverUrl)}");
            var fields = await _downloaderService.GetOwnFieldsAsync();
            System.Diagnostics.Debug.WriteLine($"[AgShare Download] Received {fields?.Count ?? 0} fields");

            if (fields == null || !fields.Any())
            {
                StatusLabel.Text = "No fields found in your AgShare account";
                return;
            }

            foreach (var field in fields.OrderBy(f => f.Name))
            {
                _fields.Add(new DownloadFieldListItem
                {
                    Id = field.Id,
                    Name = field.Name,
                    AreaHa = field.AreaHa
                });
            }

            StatusLabel.Text = $"Found {_fields.Count} field(s) - select one to download";
            BtnDownloadAll.IsEnabled = _fields.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AgShare Download] Exception: {ex.GetType().Name}: {ex.Message}");
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
        }
    }

    private void FieldListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedField = FieldListBox.SelectedItem as DownloadFieldListItem;
        BtnDownload.IsEnabled = _selectedField != null;
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await LoadFieldsAsync();
    }

    private async void DownloadAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_downloaderService == null || _fields.Count == 0)
            return;

        bool forceOverwrite = ForceOverwriteCheckBox.IsChecked ?? false;

        StatusLabel.Text = "Downloading all fields...";
        StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#BDC3C7"));
        ProgressBar.IsVisible = true;
        ProgressBar.Value = 0;
        BtnDownload.IsEnabled = false;
        BtnDownloadAll.IsEnabled = false;

        try
        {
            var progress = new Progress<int>(percent =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    var pct = (int)(percent * 100.0 / _fields.Count);
                    ProgressBar.Value = pct;
                    StatusLabel.Text = $"Downloading... {percent}/{_fields.Count}";
                });
            });

            var (downloaded, skipped) = await _downloaderService.DownloadAllAsync(
                _fieldsRootDirectory,
                forceOverwrite,
                progress);

            StatusLabel.Text = $"Downloaded {downloaded} field(s), skipped {skipped}";
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#27AE60"));

            // The user will need to open the field selection dialog to see the new fields
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
        }
        finally
        {
            ProgressBar.IsVisible = false;
            BtnDownload.IsEnabled = _selectedField != null;
            BtnDownloadAll.IsEnabled = _fields.Count > 0;
        }
    }

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedField == null || _downloaderService == null)
            return;

        StatusLabel.Text = $"Downloading '{_selectedField.Name}'...";
        StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#BDC3C7"));
        ProgressBar.IsVisible = true;
        ProgressBar.Value = 50;
        BtnDownload.IsEnabled = false;

        try
        {
            var (success, message) = await _downloaderService.DownloadAndSaveAsync(
                _selectedField.Id,
                _fieldsRootDirectory);

            if (success)
            {
                StatusLabel.Text = $"Downloaded '{_selectedField.Name}' successfully!";
                StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#27AE60"));

                await Task.Delay(1500);
                CloseDialog();
            }
            else
            {
                StatusLabel.Text = $"Failed: {message}";
                StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.Foreground = new SolidColorBrush(Color.Parse("#E74C3C"));
        }
        finally
        {
            ProgressBar.IsVisible = false;
            BtnDownload.IsEnabled = _selectedField != null;
        }
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseDialog();
        e.Handled = true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        CloseDialog();
    }

    private void CloseDialog()
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CancelAgShareDownloadDialogCommand?.Execute(null);
        }
    }
}
