using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SavorReader.Core.Models;
using SavorReader.Core.Services;

namespace SavorReader.ViewModels;

public sealed class BookItemViewModel : ObservableObject
{
    private readonly Book _book;
    private ImageSource? _coverImage;

    public BookItemViewModel(Book book)
    {
        _book = book;
    }

    public Book Book => _book;

    public long Id => _book.Id;

    public string Title => _book.Title;

    public string AuthorText => string.IsNullOrWhiteSpace(_book.Author) ? "未知作者" : _book.Author;

    public string FileName => Path.GetFileName(_book.SourcePath ?? _book.FilePath);

    public string FilePath => _book.FilePath;

    public string FormatText => _book.Format == BookFormat.Epub ? "EPUB" : "PDF";

    public bool IsPdf => _book.Format == BookFormat.Pdf;

    public bool IsMissing => !File.Exists(_book.FilePath);

    public string StatusText => _book.Status switch
    {
        ReadingStatus.Unread => "未读",
        ReadingStatus.Reading => "阅读中",
        ReadingStatus.Finished => "已读",
        _ => "未知",
    };

    public double ProgressPercent => Math.Clamp(_book.Progress, 0, 1) * 100;

    public string ProgressText => ReadingLocationCodec.FormatPercent(_book.Progress);

    public string PageCountText => _book.PageCount is > 0 ? $"{_book.PageCount} 页" : "页数未知";

    public string PublisherText => string.IsNullOrWhiteSpace(_book.Publisher) ? "出版社未知" : _book.Publisher;

    public string LanguageText => string.IsNullOrWhiteSpace(_book.Language) ? "语言未知" : _book.Language;

    public string DescriptionText => string.IsNullOrWhiteSpace(_book.Description) ? "暂无简介。" : _book.Description;

    public string AddedText => $"加入于 {_book.AddedAtUtc.ToLocalTime():yyyy-MM-dd}";

    public string LastOpenedText => _book.LastOpenedAtUtc is { } last
        ? $"上次阅读 {last.ToLocalTime():yyyy-MM-dd HH:mm}"
        : "尚未打开";

    public string LocationText => ReadingLocationCodec.FormatDisplay(_book.Location) is { } location
        ? $"位置：{location}"
        : "暂无阅读位置";

    public string TagsText => _book.Tags.Count == 0 ? "未添加标签" : string.Join(" · ", _book.Tags);

    public IReadOnlyList<string> Tags => _book.Tags;

    public string OpenButtonText => _book.Status == ReadingStatus.Finished ? "重新阅读" : "开始阅读";

    public ImageSource? CoverImage
    {
        get => _coverImage;
        private set
        {
            if (SetProperty(ref _coverImage, value))
            {
                OnPropertyChanged(nameof(HasCover));
                OnPropertyChanged(nameof(CoverVisibility));
            }
        }
    }

    public bool HasCover => CoverImage is not null;

    public Visibility CoverVisibility => HasCover ? Visibility.Visible : Visibility.Collapsed;

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (AuthorText?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || _book.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public Task LoadCoverAsync(AppPaths paths)
    {
        var path = paths.ResolveCoverPath(_book.CoverPath);
        if (path is null || !File.Exists(path))
        {
            CoverImage = null;
            return Task.CompletedTask;
        }

        try
        {
            var image = new BitmapImage
            {
                DecodePixelWidth = 360,
                UriSource = new Uri(path, UriKind.Absolute),
            };
            CoverImage = image;
        }
        catch
        {
            CoverImage = null;
        }

        return Task.CompletedTask;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(AuthorText));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FormatText));
        OnPropertyChanged(nameof(IsPdf));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(PageCountText));
        OnPropertyChanged(nameof(PublisherText));
        OnPropertyChanged(nameof(LanguageText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(AddedText));
        OnPropertyChanged(nameof(LastOpenedText));
        OnPropertyChanged(nameof(LocationText));
        OnPropertyChanged(nameof(TagsText));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(OpenButtonText));
    }
}
