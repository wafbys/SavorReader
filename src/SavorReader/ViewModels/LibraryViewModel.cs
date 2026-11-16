using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SavorReader.Core.Models;
using SavorReader.Core.Services;
using SavorReader.Services;

namespace SavorReader.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly AppServices _services = App.Services;
    private readonly List<BookItemViewModel> _all = [];

    [ObservableProperty]
    public partial BookItemViewModel? SelectedBook { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ReadingStatus? StatusFilter { get; set; }

    [ObservableProperty]
    public partial string? TagFilter { get; set; }

    [ObservableProperty]
    public partial string SortMode { get; set; } = "recent";

    [ObservableProperty]
    public partial bool IsGridView { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    public ObservableCollection<BookItemViewModel> VisibleBooks { get; } = [];

    public ObservableCollection<string> AllTags { get; } = [];

    public ObservableCollection<string> SelectedTags { get; } = [];

    public bool HasSelection => SelectedBook is not null;

    public bool HasVisibleBooks => VisibleBooks.Count > 0;

    public string SummaryText
    {
        get
        {
            var finished = _all.Count(book => book.Book.Status == ReadingStatus.Finished);
            return $"共 {_all.Count} 本 · 已读 {finished} 本";
        }
    }

    partial void OnSelectedBookChanged(BookItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        SelectedTags.Clear();
        if (value is not null)
        {
            foreach (var tag in value.Book.Tags)
            {
                SelectedTags.Add(tag);
            }
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnStatusFilterChanged(ReadingStatus? value) => ApplyFilter();

    partial void OnTagFilterChanged(string? value) => ApplyFilter();

    partial void OnSortModeChanged(string value) => ApplyFilter();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var selectedId = SelectedBook?.Id;
            var viewMode = await _services.Settings.GetLibraryViewModeAsync(cancellationToken);
            IsGridView = !string.Equals(viewMode, "list", StringComparison.OrdinalIgnoreCase);

            var books = await _services.Books.GetAllAsync(cancellationToken);
            _all.Clear();
            foreach (var book in books)
            {
                _all.Add(new BookItemViewModel(book));
            }

            await Task.WhenAll(_all.Select(item => item.LoadCoverAsync(_services.Paths)));

            var selectedTag = TagFilter;
            AllTags.Clear();
            foreach (var tag in await _services.Books.GetAllTagsAsync(cancellationToken))
            {
                AllTags.Add(tag);
            }

            TagFilter = selectedTag is not null && AllTags.Contains(selectedTag, StringComparer.OrdinalIgnoreCase)
                ? selectedTag
                : null;

            ApplyFilter();

            SelectedBook = selectedId is { } id
                ? VisibleBooks.FirstOrDefault(item => item.Id == id)
                : VisibleBooks.FirstOrDefault();
            OnPropertyChanged(nameof(SummaryText));
        }
        catch (Exception exception)
        {
            AppLog.Error("加载书库失败", exception);
            ShowInfo($"加载书库失败：{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default) =>
        await LoadAsync(cancellationToken);

    [RelayCommand]
    private async Task ImportFilesAsync()
    {
        try
        {
            var paths = await _services.Pickers.PickBooksAsync(App.MainWindow);
            await ImportPathsAsync(paths);
        }
        catch (Exception exception)
        {
            AppLog.Error("选择导入文件失败", exception);
            ShowInfo($"导入失败：{exception.Message}");
        }
    }

    [RelayCommand]
    private async Task ScanFolderAsync()
    {
        try
        {
            var folder = await _services.Pickers.PickFolderAsync(App.MainWindow);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            var paths = await Task.Run(() => BookImportService.EnumerateBooks(folder));
            await ImportPathsAsync(paths);
        }
        catch (Exception exception)
        {
            AppLog.Error("扫描文件夹失败", exception);
            ShowInfo($"扫描失败：{exception.Message}");
        }
    }

    public async Task ImportPathsAsync(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0)
        {
            ShowInfo("没有选择可导入的 EPUB 或 PDF 文件。");
            return;
        }

        IsBusy = true;
        var failures = new List<string>();
        var imported = 0;
        var duplicates = 0;

        try
        {
            var progress = new Progress<ImportProgress>(value =>
            {
                StatusMessage = value.Total == 0
                    || value.Completed >= value.Total
                    || string.IsNullOrWhiteSpace(value.CurrentFile)
                        ? string.Empty
                        : $"正在处理 {value.Completed + 1}/{value.Total}：{Path.GetFileName(value.CurrentFile)}";
            });

            var results = await _services.Imports.ImportManyAsync(paths, progress);
            foreach (var result in results)
            {
                var outcome = result switch
                {
                    ImportSuccess success => await HandleImportedAsync(success.Book, isPdf: success.Book.Format == BookFormat.Pdf),
                    ImportDuplicate => "duplicate",
                    ImportFailure failure => HandleFailure(failure),
                };

                switch (outcome)
                {
                    case "imported":
                        imported++;
                        break;
                    case "duplicate":
                        duplicates++;
                        break;
                    default:
                        failures.Add(outcome);
                        break;
                }
            }

            var message = $"导入完成：新增 {imported} 本";
            if (duplicates > 0)
            {
                message += $"，重复 {duplicates} 本";
            }

            if (failures.Count > 0)
            {
                message += $"，失败 {failures.Count} 本";
                AppLog.Warn("部分文件导入失败：\n" + string.Join('\n', failures));
            }

            ShowInfo(message);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("导入失败", exception);
            ShowInfo($"导入失败：{exception.Message}");
        }
        finally
        {
            StatusMessage = string.Empty;
            IsBusy = false;
        }
    }

    public async Task AddTagAsync(string tag)
    {
        if (SelectedBook is null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var tags = SelectedBook.Book.Tags.Append(tag.Trim()).ToArray();
        await SetTagsAsync(tags);
    }

    public async Task RemoveTagAsync(string tag)
    {
        if (SelectedBook is null)
        {
            return;
        }

        var tags = SelectedBook.Book.Tags
            .Where(existing => !string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        await SetTagsAsync(tags);
    }

    public async Task SetStatusAsync(ReadingStatus status)
    {
        if (SelectedBook is null)
        {
            return;
        }

        try
        {
            await _services.Progress.SetStatusAsync(SelectedBook.Book, status);
            SelectedBook.Refresh();
            OnPropertyChanged(nameof(SummaryText));
        }
        catch (Exception exception)
        {
            AppLog.Error("更新阅读状态失败", exception);
            ShowInfo($"更新状态失败：{exception.Message}");
        }
    }

    public async Task RemoveSelectedBookAsync()
    {
        if (SelectedBook is null)
        {
            return;
        }

        var itemToRemove = SelectedBook;
        var book = itemToRemove.Book;
        var id = book.Id;
        await _services.Books.DeleteAsync(id);
        _all.RemoveAll(item => item.Id == id);
        VisibleBooks.Remove(itemToRemove);
        SelectedBook = VisibleBooks.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(book.ContentHash))
        {
            try
            {
                await _services.EpubCache.DeleteAsync(book.ContentHash);
            }
            catch (Exception exception)
            {
                AppLog.Warn("删除 EPUB 缓存失败", exception);
            }
        }

        if (!string.IsNullOrWhiteSpace(book.CoverPath)
            && !await _services.Books.AnyOtherBookUsesCoverAsync(book.CoverPath, id))
        {
            var coverPath = _services.Paths.ResolveCoverPath(book.CoverPath);
            try
            {
                if (coverPath is not null && File.Exists(coverPath))
                {
                    File.Delete(coverPath);
                }
            }
            catch (Exception exception)
            {
                AppLog.Warn("删除封面失败", exception);
            }
        }

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasVisibleBooks));
        ShowInfo("已从书库移除记录。原文件没有被删除。");
    }

    public async Task SetViewModeAsync(bool grid)
    {
        IsGridView = grid;
        await _services.Settings.SetLibraryViewModeAsync(grid ? "grid" : "list");
    }

    public void ShowInfo(string message)
    {
        InfoBarMessage = message;
        IsInfoBarOpen = true;
    }

    private async Task<string> HandleImportedAsync(Book book, bool isPdf)
    {
        if (isPdf)
        {
            await _services.PdfCovers.EnsureCoverAsync(book);
        }

        return "imported";
    }

    private static string HandleFailure(ImportFailure failure)
    {
        var message = $"{Path.GetFileName(failure.FilePath)}：{failure.Message}";
        AppLog.Warn("导入文件失败：" + message);
        return message;
    }

    private async Task SetTagsAsync(IEnumerable<string> tags)
    {
        if (SelectedBook is null)
        {
            return;
        }

        try
        {
            await _services.Books.SetTagsAsync(SelectedBook.Id, tags);
            SelectedBook.Book.Tags.Clear();
            SelectedBook.Book.Tags.AddRange(await _services.Books.GetTagsAsync(SelectedBook.Id));
            SelectedBook.Refresh();
            SelectedTags.Clear();
            foreach (var tag in SelectedBook.Book.Tags)
            {
                SelectedTags.Add(tag);
            }

            var selectedTag = TagFilter;
            AllTags.Clear();
            foreach (var tag in await _services.Books.GetAllTagsAsync())
            {
                AllTags.Add(tag);
            }

            if (selectedTag is not null && !AllTags.Contains(selectedTag, StringComparer.OrdinalIgnoreCase))
            {
                TagFilter = null;
            }

            ApplyFilter();
        }
        catch (Exception exception)
        {
            AppLog.Error("更新标签失败", exception);
            ShowInfo($"更新标签失败：{exception.Message}");
        }
    }

    private void ApplyFilter()
    {
        if (VisibleBooks is null)
        {
            return;
        }

        IEnumerable<BookItemViewModel> query = _all;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(item => item.Matches(SearchText.Trim()));
        }

        if (StatusFilter is { } status)
        {
            query = query.Where(item => item.Book.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(TagFilter))
        {
            query = query.Where(item => item.Book.Tags.Contains(TagFilter, StringComparer.OrdinalIgnoreCase));
        }

        query = SortMode switch
        {
            "title" => query.OrderBy(item => item.Title, StringComparer.CurrentCulture),
            "author" => query.OrderBy(item => item.AuthorText, StringComparer.CurrentCulture),
            "added" => query.OrderByDescending(item => item.Book.AddedAtUtc),
            _ => query.OrderByDescending(item => item.Book.LastOpenedAtUtc ?? item.Book.AddedAtUtc),
        };

        VisibleBooks.Clear();
        foreach (var item in query)
        {
            VisibleBooks.Add(item);
        }

        OnPropertyChanged(nameof(HasVisibleBooks));
        OnPropertyChanged(nameof(SummaryText));
    }
}
