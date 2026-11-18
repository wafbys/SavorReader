# SavorReader

本地个人使用的 EPUB/PDF 书库与阅读器。WinUI 3 + .NET 11 + C# 15，纯本地运行，不打包 MSIX、不对外发布；本地 Release 部署到 `%USERPROFILE%\App\SavorReader`。

## 技术栈

- **应用项目**：`net11.0-windows10.0.26100.0`，WinUI 3（Windows App SDK 2.7.0），unpackaged，`WindowsPackageType=None`
- **核心项目**：`net11.0`，C# 15
- **UI**：WinUI 3 / XAML；自定义标题栏 + Mica
- **EPUB**：解压到本地缓存，WebView2 虚拟主机，spine 章节 iframe 连续滚动
- **PDF**：WebView2 内置 PDF 查看器；`Windows.Data.Pdf` 生成首页封面
- **持久化**：SQLite（`Microsoft.Data.Sqlite`）
- **格式解析**：`VersOne.Epub`、`PdfPig`

## 环境要求

- Windows 11 x64（本机为 Windows 11 26300）
- .NET SDK 11.0.100（`global.json` 已固定）
- Windows App Runtime 2.7.0（本机已安装）
- WebView2 Runtime（本机已安装 157.x）

项目不生成 MSIX，也不需要 Visual Studio；全部操作通过命令行完成。

## 版本

版本号遵循 SemVer，唯一来源是 `src\SavorReader\SavorReader.csproj` 的 `VersionPrefix`：

- 新增功能：升 minor，例如 `1.1.0` → `1.2.0`
- 修复：升 patch，例如 `1.1.0` → `1.1.1`
- 不兼容改动：升 major

版本、配置、Git 短哈希和构建时间会显示在窗口标题和关于页；每次版本变更同步更新 `CHANGELOG.md`，再运行 `run-release.ps1`。

## 构建、测试、运行

```powershell
dotnet build .\SavorReader.slnx -p:Platform=x64
dotnet test .\tests\SavorReader.Core.Tests\SavorReader.Core.Tests.csproj -p:Platform=x64
dotnet run --project .\src\SavorReader\SavorReader.csproj -p:Platform=x64
```

Release 构建并部署到固定执行目录：

```powershell
.\run-release.ps1
```

窗口标题会显示版本、`Debug`/`Release`、Git 短哈希和构建时间，例如：

```text
SavorReader v1.1.0 · Release · 3a83ebb · 2026-11-18 09:30
```

脚本会：

1. `dotnet build -c Release -p:Platform=x64 --no-incremental`（强制重建，确保短哈希刷新）
2. 把 Release 输出复制到 `C:\Users\YF\App\SavorReader`
3. 保留该目录下已有的 `Data`
4. 在 `%APPDATA%\Microsoft\Windows\Start Menu\Programs\SavorReader.lnk` 创建/更新开始菜单快捷方式
5. 启动 `C:\Users\YF\App\SavorReader\SavorReader.exe`

只构建/部署、不启动：

```powershell
.\run-release.ps1 -NoLaunch
```

不创建开始菜单快捷方式：

```powershell
.\run-release.ps1 -NoStartMenuShortcut
```

`run-release.bat` 是同一脚本的批处理入口。

## 数据目录

数据目录固定在**执行文件旁的 `Data` 目录**：

```text
<exe目录>\Data\
├─ library.db          SQLite 书库
├─ library.db-wal      SQLite WAL
├─ Books\              导入时复制的 EPUB/PDF 副本
├─ covers\             封面缓存
├─ cache\epub\         EPUB 解压缓存
├─ webview2\           WebView2 用户数据
└─ logs\app.log        应用日志
```

- Release 运行：`C:\Users\YF\App\SavorReader\Data`
- `dotnet run`：`src\SavorReader\bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64\Data`
- 测试/诊断可用环境变量 `SAVORREADER_DATA_DIR` 覆盖数据根目录。

导入时会把书籍**复制到 `Data\Books`**，书库指向副本而不是原路径。文件名保持原始文件名；同名文件自动追加 `(2)`、`(3)` 后缀，不使用 UUID 或哈希作为文件名。原文件保留；移走原文件不会影响阅读。相同源路径或相同内容重复导入会跳过。旧版本已经导入的外部书籍会在下一次启动时自动复制到 `Data\Books`，已有的哈希名副本也会改回原文件名。

数据库中的托管书籍路径保存为**相对 `Data` 目录的路径**（例如 `Books/书名.epub`），封面路径同样相对。因此整个 `SavorReader` 运行目录可以整体移动，不需要修数据库。但开始菜单快捷方式仍指向旧 exe，需要手动更新；`run-release.ps1` 默认仍部署到 `C:\Users\YF\App\SavorReader`。`SourcePath` 仍保留导入时的原始绝对路径，只用于显示和重复导入判断，不参与阅读。

## 功能

### 书库

- 导入单个/多个 EPUB、PDF；递归扫描文件夹
- 提取标题、作者、出版社、语言、简介、页数、封面
- 网格/列表视图、搜索、状态筛选、标签筛选、排序
- 标签新增/移除
- 阅读状态：未读 / 阅读中 / 已读
- 阅读进度与上次位置
- PDF 封面首页渲染；缺失封面使用占位
- 从书库移除记录（删除书库副本与缓存，不删除原文件）
- 在资源管理器中定位书库副本

### EPUB 阅读器

- WebView2 虚拟主机加载本地解压内容
- spine 章节 iframe 自动测高，连续滚动
- 目录（EPUB3 nav / EPUB2 NCX 由 VersOne 解析）
- 日间/夜间主题
- 字体、字号、行距、边距
- 进度保存与恢复
- 选中文本桥接，为后续高亮/笔记预留 `Annotations` 表与消息入口
- 书籍脚本在 iframe sandbox 中禁用

### PDF

- WebView2 原生 PDF 查看器（页码、缩放、旋转）
- 从书库/阅读页调用系统默认应用打开
- 首页封面由 `Windows.Data.Pdf` 渲染
- 进度按“打开时间 + 阅读状态”记录；原生查看器没有公开的当前页码 API，因此 MVP 不记录 PDF 页码进度

### 设置 / 瘦身 / 关于

- 设置页查看数据目录、日志和运行空间
- “瘦身”扫描并清理 EPUB 解压缓存、未引用的书籍副本/封面和日志，显示可释放空间；不会删除当前书库正在使用的书籍和封面
- WebView2 浏览器缓存标记为下次启动清理，避免删除正在被浏览器进程占用的文件
- 左侧导航栏记住展开/收起状态，重启后恢复
- 导航底部“关于”页显示版本、.NET/WinUI/WebView2 运行环境、系统架构和数据路径
- 关于页可直接打开 `LICENSE` 与 `THIRD-PARTY-NOTICES.md`，并说明当前支持范围

## 应用图标

图标采用“奶油纸底 + 深墨绿书本 + 陶土色书签”：暖色纸感对应阅读与 Savor，书本轮廓保证 16px 任务栏尺寸下仍可辨认。`Assets\AppIcon.ico` 内含 16、24、32、48、64、128、256 多个尺寸，同时用于 exe、窗口标题栏、任务栏和关于页。

## 项目结构

```text
src/
  SavorReader.Core/       net11.0；模型、SQLite、EPUB/PDF 解析、导入、进度
  SavorReader/            net11.0-windows10.0.26100.0；WinUI 3、WebView2、视图
tests/
  SavorReader.Core.Tests/ xUnit；数据库、解析、缓存、导入、进度测试
```

## 设计要点

- C# 15 `closed` 层级用于 `ReadingLocation`（EPUB 位置 / PDF 页码）。
- C# 15 `union` 用于 `ImportOutcome`（成功 / 重复 / 失败），调用端做穷尽 switch。
- EPUB 解压缓存按文件 SHA-256 命名；解压前检查 Zip Slip、条目数和总大小。
- 托管书籍和封面路径存相对数据目录的路径；整个运行目录迁移时不需要改数据库。
- SQLite 使用 WAL、外键、`user_version` 迁移。
- WebView2 的 EPUB 内容与阅读器 shell 位于同一虚拟主机，便于父页面注入样式和读取 iframe DOM。
- PDF 映射其所在目录；只导航到指定 PDF。

## 已知限制

- 固定版式 EPUB（fixed layout）、DRM 加密 EPUB/PDF 不在 MVP 支持范围内，会显示错误。
- PDF 页码进度不记录（见上）。
- EPUB 极端复杂排版可能被阅读器的字体/边距覆盖影响；优先保证可重排书籍。
- 不含云同步、在线元数据抓取、高亮/笔记 UI。
- 依赖 WebView2 Runtime；缺少时阅读器会显示错误。

## 许可证

自有代码默认 MIT（见 `LICENSE`）。第三方依赖和运行时说明见 `THIRD-PARTY-NOTICES.md`。
