# Third-party notices

SavorReader 的代码依赖全部为开源或免费运行时组件；没有引入付费 SDK、商业控件或商业 PDF 引擎。

## Runtime and libraries

| Component | Version | License | Use |
|---|---|---|---|
| Microsoft Windows App SDK / WinUI 3 | 2.6.5 | MIT (source) | WinUI 3 application framework |
| Microsoft WebView2 | 1.0.3719.77 package; system runtime | Microsoft WebView2 terms | EPUB rendering and built-in PDF viewer |
| .NET runtime | 11.0 | MIT | Runtime |
| Microsoft.Data.Sqlite | 11.0.0 | MIT | SQLite persistence |
| SQLitePCLRaw / SQLite | 3.0.5 / 3.53.4 | Apache-2.0 / public domain | SQLite native interop |
| VersOne.Epub | 3.3.6 | Unlicense | EPUB metadata, navigation, spine, cover |
| PdfPig | 0.1.16 | Apache-2.0 | PDF metadata and page count |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | MVVM source generators |

Test-only packages (xUnit, Microsoft.NET.Test.Sdk, coverlet) are not part of the shipped app.

## Notes

- `Windows.Data.Pdf` is a Windows OS API and is not a separately licensed SDK.
- The WebView2 Runtime and the Edge PDF viewer are free but not open-source runtime components. They are the one intentional non-open-source runtime dependency, because WebView2 was explicitly required by this project.
- If a fully open-source runtime stack were required, WebView2 itself would have to be replaced; that is outside this project's constraints.

## License links

- Windows App SDK: https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE
- WebView2: https://learn.microsoft.com/microsoft-edge/webview2/
- .NET: https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- Microsoft.Data.Sqlite: https://github.com/dotnet/efcore/blob/main/LICENSE.txt
- SQLitePCLRaw: https://github.com/ericsink/SQLitePCL.raw/blob/master/LICENSE.txt
- SQLite: https://www.sqlite.org/copyright.html
- VersOne.Epub: https://github.com/vers-one/EpubReader/blob/master/LICENSE
- PdfPig: https://github.com/UglyToad/PdfPig/blob/master/LICENSE
- CommunityToolkit.Mvvm: https://github.com/CommunityToolkit/dotnet/blob/main/License.md
