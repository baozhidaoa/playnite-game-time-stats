# Game Time Statistics / 游戏时间统计

Game Time Statistics is a Playnite generic plugin that shows playtime trends, daily activity, hourly habits, genre distribution, and recent/top games in a standalone statistics window.

Game Time Statistics 是一个 Playnite 通用插件，用独立统计窗口展示游戏时长趋势、每日活跃、时段分布、类型分布、最近游玩和最高时长游戏。

## Features / 功能

- Standalone Playnite WebView window; no custom theme slot is required.
- Tracks Playnite game sessions and recovers interrupted sessions after restart.
- Reads local Steam userdata when available and can optionally call Steam Web API.
- Shows heatmap, bar, line, hourly, genre, recent-games, and top-games views.
- Stores runtime data in Playnite's plugin user data folder, so updates do not overwrite user history.

## Installation / 安装

Install from Playnite's add-on browser after the add-on database pull request is merged, or install a `.pext` file from a GitHub Release.

在 Playnite 插件商城 PR 合并后，可直接从 Playnite 插件浏览器安装；也可以从 GitHub Release 下载 `.pext` 文件安装。

## Privacy / 隐私说明

The plugin stores `sessions.json`, `steam-snapshots.json`, generated web UI files, and plugin settings in Playnite's plugin user data directory. These files stay on your machine.

插件会在 Playnite 插件用户数据目录保存 `sessions.json`、`steam-snapshots.json`、生成后的 web UI 文件和插件设置。这些文件保留在本机。

If online Steam sync is enabled, the plugin calls Steam Web API endpoints with the configured Steam API key and SteamId64. If the Steam API key is left empty, the plugin still tries to read local Steam configuration/userdata when available. No telemetry or third-party analytics are included.

如果启用在线 Steam 同步，插件会使用配置的 Steam API Key 和 SteamId64 请求 Steam Web API。若未填写 Steam API Key，插件仍会在可用时读取本机 Steam 配置/userdata。插件不包含遥测或第三方统计。

## Development / 开发

Requirements:

- .NET SDK capable of building `net481`
- Playnite SDK files in the repository root, matching the current references
- Playnite Toolbox for packaging and manifest validation

Build:

```powershell
dotnet build src\PlayniteGameStats.csproj -c Release
```

Prepare a release staging directory and package:

```powershell
.\scripts\prepare-release.ps1 -GithubUser YOUR_GITHUB_USER
```

If `Toolbox.exe` is not on `PATH`, pass its full path:

```powershell
.\scripts\prepare-release.ps1 -GithubUser YOUR_GITHUB_USER -ToolboxPath "C:\Path\To\Toolbox.exe"
```

Before publishing, make sure `extension.yaml`, `LICENSE`, `manifests\installer.yaml`, and `manifests\addon-database.yaml` use the correct GitHub user or organization.

正式发布前，请确认 `extension.yaml`、`LICENSE`、`manifests\installer.yaml` 和 `manifests\addon-database.yaml` 使用了正确的 GitHub 用户名或组织名。

## Release Checklist / 发布检查

- Update metadata and changelog for the target version.
- Run `dotnet build src\PlayniteGameStats.csproj -c Release`.
- Run `.\scripts\prepare-release.ps1 -GithubUser baozhidaoa`.
- Upload `dist\GameTimeStats_dc73bf2f-ffd7-40e0-acd4-08e2296a239e_1_0_0.pext` to GitHub Release `v1.0.0`.
- Verify the installer manifest:
  `Toolbox.exe verify installer manifests\installer.yaml`
- Verify the add-on database manifest:
  `Toolbox.exe verify addon manifests\addon-database.yaml`
- Copy `manifests\addon-database.yaml` to `addons/generic/GameTimeStats_dc73bf2f-ffd7-40e0-acd4-08e2296a239e.yaml` in a fork of `JosefNemec/PlayniteAddonDatabase` and open a pull request.

## Links / 链接

- Playnite extension manifest docs: https://api.playnite.link/docs/tutorials/extensions/extensionsManifest.html
- Playnite Toolbox docs: https://api.playnite.link/docs/tutorials/toolbox.html
- Playnite Addon Database: https://github.com/JosefNemec/PlayniteAddonDatabase
