# Changelog

## 未发布

### Changed

- Windows App SDK `2.6.5` → `2.7.0`（2.x 服务线补丁，无破坏性变更）。
- 窗口标题与关于页统一显示 `v1.1.0 · Release · 825f7ec · 2026-11-18 09:30`（版本 · 配置 · 短哈希 · 构建时间）；构建时间由 `AssemblyMetadata` 注入。
- `run-release.ps1` 与 IcedPicViewer 对齐：显式 `-p:Platform=x64`，从 `bin\x64\Release` 部署，快捷方式描述改用 ASCII 避免 ACP 1252 下中文变问号。

### Verified

- Release 构建 0/0；Core 测试 21 全绿；部署版启动成功，标题为 `SavorReader v1.1.0 · Release · 825f7ec · 2026-11-18 09:54`；`Data` 保留在 exe 旁。

## 1.1.0 - 2026-11-16

### Added

- 关于页：版本、.NET/WinUI/WebView2、系统、数据目录、许可证与范围说明。
- 书库托管副本：导入时复制到 `Data\Books`，保留原始文件名；同名文件追加序号。
- 相对路径：数据库保存 `Books/...`，整个运行目录可移动；启动时自动迁移 v2 绝对路径和旧哈希名副本。
- 品牌图标：奶油纸底、深墨绿书本、陶土色书签，多尺寸 ICO。
- 窗口标题与关于页显示版本、Debug/Release、Git 短哈希。
- 设置页“瘦身”：清理 EPUB 缓存、未引用书籍/封面、日志；WebView2 缓存在下次启动清理。
- 左侧导航栏记住展开/收起状态。

### Changed

- 部署脚本创建/更新开始菜单快捷方式，并在 Release 构建时强制刷新版本与 Git 短哈希。
- README 补充数据目录、相对路径、版本策略和清理说明。

## 1.0.0 - 2026-11-16

- 初始版本：SQLite 书库、EPUB WebView2 阅读器、PDF 基础查看、导入与元数据、标签、阅读状态和进度。
