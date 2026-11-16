using Microsoft.Web.WebView2.Core;
using SavorReader.Core.Services;

namespace SavorReader.Services;

public sealed class WebView2EnvironmentProvider
{
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CoreWebView2Environment? _environment;

    public WebView2EnvironmentProvider(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<CoreWebView2Environment> GetAsync(
        CancellationToken cancellationToken = default)
    {
        if (_environment is not null)
        {
            return _environment;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_environment is null)
            {
                Directory.CreateDirectory(_paths.WebView2Directory);
                _environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null,
                    _paths.WebView2Directory,
                    null);
            }

            return _environment;
        }
        finally
        {
            _gate.Release();
        }
    }
}
