# Paster

Windows 上的常用文本粘贴工具。主窗口按分页管理词条，关闭后缩到托盘继续运行。在任意输入框按 `Ctrl+Shift+V` 呼出列表，点一条就会粘贴到刚才的窗口，然后自动隐藏。

词条保存在本机，默认路径是 `%AppData%\Paster\clips.json`。设置里可以改这个路径，也可以勾选开机启动。

## 环境

- Windows 10 或 Windows 11，64 位
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（选 Windows x64 的 SDK 安装包）

装好后打开终端，确认版本是 8：

```powershell
dotnet --version
```

也可以用 Visual Studio 2022，安装时勾选「.NET 桌面开发」。

## 克隆后运行

```powershell
git clone https://github.com/<你的用户名>/<仓库名>.git
cd <仓库名>
dotnet run -c Release
```

第一次运行会下载恢复信息并编译，完成后窗口会直接打开。之后想再启动，在项目目录里再执行一次 `dotnet run -c Release` 即可。

编译结果在 `bin\Release\net8.0-windows\`。这个目录依赖本机已安装的 .NET 8 桌面运行时（安装 SDK 时会一并带上），把整个文件夹拷走才能运行，只拷 `Paster.exe` 不够。

## 使用

- 右上角关闭：缩到托盘，程序仍在运行。托盘图标双击可重新打开，右键选「退出」才真正结束。
- `Ctrl+Shift+V`：在当前光标处呼出列表并粘贴。若提示快捷键注册失败，说明这个组合已被其它软件占用。
- 设置里的「开机时启动」：登录后只在托盘运行，不弹出主窗口。

## 打包给不编译的人

仓库里只放源码。要给别人一个解压即用的程序，在本机执行：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

生成的单个程序在：

`bin\Release\net8.0-windows\win-x64\publish\Paster.exe`

对方电脑不需要安装 .NET，也不需要这份源码。把这个 exe 打成 zip，挂到 GitHub 仓库的 Releases 里供下载。它体积大约一百多 MB，不适合放进 Git 提交记录。
