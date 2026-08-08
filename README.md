# 桌面便签 / StickyNotes

[中文](#中文) | [English](#english)

## 中文

一个仅面向 Windows 的轻量桌面便签应用。主界面负责管理便签，单张便签可以独立显示、调整大小、更换颜色并切换始终置顶。

## 当前功能

- 新建、重命名、显示、隐藏和永久删除便签
- 多张便签同时显示，关闭单张便签时只隐藏
- 段落式图文正文，文本和多张图片可以按顺序混合排列
- 支持在光标位置通过文件选择或剪贴板截图插入图片，也支持拖入图片文件
- 支持拖动图片调整正文顺序、拖动或按钮缩放、打开原图和删除单张图片
- 支持按钮或 `Ctrl+Z`、`Ctrl+Y`、`Ctrl+Shift+Z` 撤销与重做正文编辑
- 正文编辑后 500 毫秒防抖自动保存
- 单张便签独立设置背景颜色和始终置顶
- 恢复便签的位置、大小、显示状态和置顶状态
- 主界面最小化时不影响已显示便签
- 单实例运行，重复启动时唤醒已有主界面
- 本地保存，不使用账号、网络或云服务
- 隐藏便签时释放对应窗口，只保留列表和文本数据
- 每次启动及使用期间定期生成 ZIP 轮换备份，最多保留 30 份

数据默认保存在 `%LOCALAPPDATA%\StickyNotes\notes.json`。
正文图片文件保存在 `%LOCALAPPDATA%\StickyNotes\Assets`，按便签分别存放；删除便签或图片前会先创建备份。
自动备份保存在 `%LOCALAPPDATA%\StickyNotes\Backups`，每个 ZIP 同时包含 `notes.json` 和当时存在的全部图片。

支持 PNG、JPG/JPEG、BMP 和静态 GIF，单张图片上限为 10 MB。所有文本和图片均只保存在本机。
升级前已经添加的附件图片会自动迁移到对应便签正文末尾，不会删除旧文本或图片文件。

## 发布与安装

### 环境要求

- Windows 10 或 Windows 11（x64）
- 从源码构建时需要 .NET 10 SDK

### 直接下载

普通用户可以从 [Releases](https://github.com/whimy7/stickynotes/releases) 下载 `StickyNotes.exe`。这是 Windows x64 自包含版本，直接运行不需要预装 .NET。

克隆仓库并构建：

```powershell
git clone https://github.com/whimy7/stickynotes.git
cd stickynotes
dotnet build .\StickyNotes.slnx
```

### 安装到本机

在 PowerShell 中运行：

```powershell
.\install.ps1
```

脚本会使用本机的 .NET 10 SDK 生成 Windows x64 自包含单文件版本，将其安装到 `%LOCALAPPDATA%\Programs\StickyNotes`，并创建桌面与开始菜单快捷方式。生成后的应用不需要预装 .NET。

只生成可执行文件时运行：

```powershell
.\publish.ps1
```

输出位于 `artifacts\publish\win-x64\StickyNotes.exe`。

## 卸载

默认保留便签数据：

```powershell
.\uninstall.ps1
```

同时删除本地便签数据：

```powershell
.\uninstall.ps1 -RemoveData
```

## 许可证

本项目使用 [MIT License](LICENSE)。

---

## English

StickyNotes is a lightweight Windows desktop sticky notes application. The main window manages notes, while each note can be displayed independently, resized, recolored, and pinned above other applications.

### Features

- Create, rename, show, hide, and permanently delete notes
- Display multiple notes at the same time
- Mix text paragraphs and inline images in one document
- Insert images at the caret from a file, a clipboard screenshot, or a dropped image file
- Reorder images by dragging, resize them by dragging or using buttons, open the original, or delete them
- Undo and redo document edits with buttons, `Ctrl+Z`, `Ctrl+Y`, or `Ctrl+Shift+Z`
- Auto-save document edits after 500 milliseconds of inactivity
- Set an individual background color and always-on-top state for each note
- Restore note position, size, visibility, and topmost state
- Keep visible notes independent when the main window is minimized
- Single-instance behavior: launching the app again wakes the existing main window
- Local-only storage with no account, network, or cloud service
- Release note windows and keep list and document data when a note is hidden
- Create rotating ZIP backups at startup and during use, keeping up to 30 backups

### Local data

By default, data is stored in `%LOCALAPPDATA%\\StickyNotes\\notes.json`.
Inline image files are stored under `%LOCALAPPDATA%\\StickyNotes\\Assets`, grouped by note. A backup is created before deleting a note or image.
Automatic backups are stored in `%LOCALAPPDATA%\\StickyNotes\\Backups`; each ZIP contains `notes.json` and the images present at that time.

PNG, JPG/JPEG, BMP, and static GIF files are supported, with a 10 MB limit per image. Text and images are stored locally on the computer. Images from the previous attachment-based format are migrated to the end of the corresponding document without deleting existing content.

### Requirements

- Windows 10 or Windows 11 (x64)
- .NET 10 SDK is required to build from source

### Download

Download `StickyNotes.exe` from the [Releases](https://github.com/whimy7/stickynotes/releases) page. The Windows x64 release is self-contained and does not require .NET to be installed on the target computer.

### Build from source

```powershell
git clone https://github.com/whimy7/stickynotes.git
cd stickynotes
dotnet build .\\StickyNotes.slnx
```

To install the application and create Desktop and Start menu shortcuts:

```powershell
.\\install.ps1
```

The install script uses the .NET 10 SDK to create a self-contained single-file build and installs it under `%LOCALAPPDATA%\\Programs\\StickyNotes`.

To only create the executable:

```powershell
.\\publish.ps1
```

The output is `artifacts\\publish\\win-x64\\StickyNotes.exe`.

### Uninstall

The default uninstall command keeps note data:

```powershell
.\\uninstall.ps1
```

To also remove local note data:

```powershell
.\\uninstall.ps1 -RemoveData
```

### License

This project is released under the [MIT License](LICENSE).
