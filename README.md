# 桌面便签

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
