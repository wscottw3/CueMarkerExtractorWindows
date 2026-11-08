using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CueKit;

namespace CueMarkerExtractorApp;

public partial class MainWindow : Window
{
    private static readonly IBrush IdleBorderBrush = Brushes.Gray;
    private static readonly IBrush ActiveBorderBrush = Brushes.DodgerBlue;

    private string? _droppedFilePath;
    private List<Chapter> _chapters = new();

    public MainWindow()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(DropZone, true);
        DropZone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        DropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);

        // Lets "Open With" / double-click launch on Windows (and our own
        // end-to-end testing) load a file immediately instead of requiring
        // a drag every time.
        Opened += (_, _) => TryLoadFromLaunchArguments();
    }

    private void TryLoadFromLaunchArguments()
    {
        var path = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(a => a.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (path != null) Load(path);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            DropZone.BorderBrush = ActiveBorderBrush;
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZone.BorderBrush = IdleBorderBrush;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropZone.BorderBrush = IdleBorderBrush;
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        var path = file?.TryGetLocalPath();
        if (path != null) Load(path);
    }

    private void Load(string path)
    {
        ErrorText.IsVisible = false;
        ConfirmationText.IsVisible = false;
        ChapterList.IsVisible = false;
        ExportButton.IsVisible = false;
        _chapters = new List<Chapter>();

        if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            ShowError("Please drop an MP3 file.");
            return;
        }

        _droppedFilePath = path;
        DropZoneLabel.Text = Path.GetFileName(path);

        try
        {
            var data = File.ReadAllBytes(path);
            _chapters = Id3ChapterParser.ParseChapters(data);
            ChapterList.ItemsSource = _chapters.Select((c, i) => new ChapterRow
            {
                Index = i + 1,
                Title = c.Title ?? "Untitled",
                Timecode = CueSheetGenerator.Timecode(c.StartMs)
            }).ToList();
            ChapterList.IsVisible = true;
            ExportButton.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_droppedFilePath == null) return;

        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var directory = Path.GetDirectoryName(_droppedFilePath);
        IStorageFolder? startFolder = null;
        if (!string.IsNullOrEmpty(directory))
        {
            startFolder = await storageProvider.TryGetFolderFromPathAsync(directory);
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export CUE File",
            SuggestedFileName = Path.GetFileNameWithoutExtension(_droppedFilePath) + ".cue",
            SuggestedStartLocation = startFolder,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CUE Sheet") { Patterns = new[] { "*.cue" } }
            }
        });

        if (file == null) return;

        var cueText = CueSheetGenerator.Generate(Path.GetFileName(_droppedFilePath), _chapters);

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(cueText);
            await writer.FlushAsync();
            ConfirmationText.Text = $"Saved to {file.Name}";
            ConfirmationText.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to save: {ex.Message}");
        }
    }
}
