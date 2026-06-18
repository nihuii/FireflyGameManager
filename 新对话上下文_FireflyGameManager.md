# FireflyGameManager 新对话上下文

更新时间：2026-06-17

这份文档用于在新开的 Codex 对话中快速接手 `D:\Study\Projects\FireflyGameManager` 项目。新对话开始后建议先阅读本文件，再按“建议读取顺序”查看代码和方案文档。

## 1. 项目定位

FireflyGameManager 是一个基于 .NET 10 / WPF 的本地游戏库管理工具，目标是管理本机游戏、启动游戏、统计游玩时长、备份/恢复本地存档，并通过 WebDAV 同步用户数据和存档备份。后续已扩展 Bangumi 账号连接和在线游戏资料导入能力。

项目类型：

- 桌面应用：WPF
- 目标框架：`net10.0-windows`
- 数据库：SQLite，使用 `Microsoft.Data.Sqlite 10.0.7`
- UI 架构：XAML + MVVM 风格 ViewModel
- 本地数据目录：`%LocalAppData%\FireflyGameManager`
- 主要工程：
  - `GameManager.App`：WPF 应用
  - `GameManager.App.Tests`：控制台式测试入口，目前登记 180 项测试

## 2. 建议读取顺序

新对话接手时优先读这些文件：

1. `新对话上下文_FireflyGameManager.md`：当前交接文档。
2. `开发方案.md`：原始产品规划。
3. `Bangumi账号与游戏信息导入实现方案.md`：Bangumi 登录、资料导入、阶段 A/B/C/D 规划与实施状态。
4. `GameManager.App\MainWindow.xaml.cs`：生产运行时依赖组装入口。
5. `GameManager.App\ViewModels\MainWindowViewModel.cs`：页面路由、导航、主要 ViewModel 注入。
6. `GameManager.App\Services\AppPaths.cs`：本地数据路径。
7. `GameManager.App\Services\SqliteGameLibraryService.cs`：核心数据库持久化。
8. `GameManager.App\Services\WebDavManualSyncService.cs`、`WebDavFullSyncService.cs`、`WebDavGameSyncService.cs`、`WebDavCloudMetadataPullService.cs`：WebDAV 同步链路。
9. `GameManager.App\Services\BangumiApiClient.cs`、`BangumiGameMetadataProvider.cs`、`BangumiDtoMapper.cs`：Bangumi 搜索、详情、收藏状态 API。
10. `GameManager.App.Tests\Program.cs`：全部回归测试和假实现。

## 3. 当前功能概览

### 已实现的本地游戏库能力

- 添加、修改、删除、置顶游戏。
- 支持添加游戏时选择 EXE、游戏目录、存档目录、封面。
- 存档目录允许为空，首次启动不因缺少存档目录阻塞。
- 游戏库卡片有封面展示、悬停动画、右下角更多菜单。
- 管理模式支持多选删除游戏条目，仅删除数据库条目，不删除本地游戏文件。
- 游戏详情页支持启动游戏、打开游戏目录、打开存档目录、修改信息、返回游戏库。

### 已实现的启动与统计能力

- 点击“开始游戏”会真正启动 EXE。
- 支持启动参数、工作目录、管理员运行、监控进程名。
- 游戏退出后统计游玩时长，记录上次启动时间。
- 记录单次游玩会话，云端合并时不会减少本地累计时长。

### 已实现的本地存档备份能力

- 游戏详情页支持“备份存档”和“恢复存档”。
- 将游戏存档目录压缩为 zip，保存到 `%LocalAppData%\FireflyGameManager\SaveBackups`。
- 支持备份历史列表。
- 支持从历史列表恢复备份、删除备份文件。
- 恢复失败会回滚原存档。
- 备份历史在游戏改名后仍保留。
- 备份文件路径和游戏 id 经过安全处理，避免危险路径片段。

### 已实现的 WebDAV / 坚果云能力

- 设置页可填写 WebDAV 地址、用户名、应用密码、远程目录。
- 支持测试连接，嵌套远程目录会顺序创建。
- 支持手动上传：
  - `metadata/app.db`
  - `save-backups` 下的存档 zip
- 支持手动下载：
  - 云端用户数据库
  - 云端存档备份 zip
- 支持全量同步：
  - 先确认远端下载成功，再合并本地与远端数据。
  - 所有游戏同步元数据。
  - 只有启用存档同步的游戏上传存档。
- 已实现 WebDAV V2 的单游戏、单设备数据结构。
- 启动时如果 WebDAV 配置完整，会进行云端元数据拉取。
- MachineGamePath、管理员启动、本机同步开关均纳入同步策略。

### 已实现的 UI / 设置能力

- 应用使用自绘沉浸式标题栏和圆角外框。
- 左侧紧凑导航栏，图标来自 `images\2` 并复制到 `GameManager.App\Assets`。
- 支持系统托盘。
- 设置中心分区：
  - 常规
  - 启动
  - 游戏库
  - 外观
  - 账号与数据源
  - 数据维护
- 支持壁纸、透明化 UI、根据壁纸提取色系动态更新 UI 颜色。
- 支持关闭窗口行为、语言选项、自动启动、启动最小化、备份保留数量、卡片大小、排序等偏好。

### 已实现的 Bangumi 阶段 A/B 能力

详见 `Bangumi账号与游戏信息导入实现方案.md`。

阶段 A 已实现：

- 添加/编辑游戏页面支持 Bangumi 游戏资料搜索。
- 候选结果显示封面、中文名/原名、日期、简介摘要。
- 支持查看完整详情，再选择导入字段：
  - 名称
  - 封面
  - 简介
  - 发行日期
  - 开发商
  - 发行商
  - 标签
- 新游戏默认导入名称和封面；编辑已有游戏默认不覆盖本地名称和封面。
- 搜索结果缓存 10 分钟。
- 请求支持取消；离开页面会取消未完成请求。
- 封面下载只接受 HTTPS，限制大小，校验 PNG/JPEG 文件头并用 WPF 解码验证真实图片。
- 详情页显示外部资料区，支持来源链接、刷新资料、解除关联。
- 刷新资料不会直接覆盖本地数据，会先展示差异并由用户选择字段。
- 长简介可展开/收起。

阶段 B 已实现：

- 设置中心支持 Bangumi Access Token 登录。
- Token 通过 DPAPI 加密保存到 `bangumi-account.json`。
- 数据导出和 WebDAV 同步不会上传 Bangumi Token。
- 支持重新验证和退出登录。
- Token 失效时会保留账号资料并标记“需要重新连接”。
- 游戏详情页支持 Bangumi 收藏状态读取、刷新、修改。
- 创建收藏使用 `POST`，已有收藏修改使用 `PATCH`。
- Bangumi 当前公开 API 没有提供删除收藏接口，因此项目不发送未文档化的“取消收藏”请求。

## 4. 最近一次关键修复：Bangumi 搜索质量

用户反馈：

- 在修改游戏界面的 Bangumi 游戏资料中输入游戏名称后，得到的都是相关结果。
- 例如搜索“冬日狂想曲”时，Bangumi 网页能得到正确结果，但应用不准确。
- 后续又发现搜索“千恋万花”后需要往下划才出现目标“千恋＊万花”。

原因：

- 应用原先只使用 `POST /v0/search/subjects`，且直接按接口返回顺序展示。
- Bangumi v0 搜索和网页搜索的召回/排序不一致。
- 对 CJK 标题，Bangumi 旧版 `GET /search/subject/{query}?type=4` 更接近网页搜索。
- 本地排序原来只忽略空白，没有忽略 `＊`、`-`、间隔符等标题符号。

当前修复：

- `BangumiApiClient.SearchGamesAsync` 先请求 v0 搜索。
- 如果查询包含 CJK 字符，且 v0 结果没有强标题命中，则补充请求旧版 `search/subject`。
- 合并两路结果，按 subject id 去重。
- 本地重新排序：
  - 忽略空白、标点、符号后的完全同名优先。
  - 前缀命中次之。
  - 包含命中再次。
  - 同分时按资料完整度排序，优先有封面、简介、日期的条目。
- 标题规范化使用 Unicode FormKC，并只保留字母、数字和组合标记。

已验证的真实接口表现：

- `search/subject/冬日狂想曲` 第一条返回 `427028 / あまえんぼ冬 / 冬日狂想曲`。
- `search/subject/千恋万花` 第一条返回 `172612 / 千恋＊万花`。

相关文件：

- `GameManager.App\Services\BangumiApiClient.cs`
- `GameManager.App\Services\BangumiDtoMapper.cs`
- `GameManager.App.Tests\Program.cs`
- `Bangumi账号与游戏信息导入实现方案.md`

相关测试：

- `bangumi api ranks exact title matches first`
- `bangumi api ignores title separator symbols for ranking`
- `bangumi api falls back to legacy subject search when modern search misses exact title`

## 5. 目前未实现或后续阶段

Bangumi 方案中仍未包含：

- 阶段 C：外部资料 WebDAV 同步。
  - 当前 WebDAV 同步明确排除阶段 C 才允许同步的外部资料和 Bangumi 收藏缓存。
  - 后续需要设计如何把 `ExternalGameMetadata` 快照同步到云端，同时避免同步 Token 和私有收藏状态。
- 阶段 D：OAuth 登录和更多资料提供方。
  - 当前只支持用户手动填写 Bangumi Access Token。
  - 未实现浏览器 OAuth 登录。
  - 未实现 VNDB、SteamGridDB、IGDB 等其他资料源。

其他可继续优化的方向：

- Bangumi 搜索结果可增加“精确/相关/来自网页搜索”标记，但要注意 UI 简洁。
- Bangumi 搜索可增加别名/原名/中文名的多关键词补查。
- 详情页在线资料区还可以进一步压缩布局，减少功能堆叠感。
- WebDAV 外部资料同步需要兼容旧云端数据和冲突策略。
- 清理 `artifacts`、`_verify`、`bin`、`obj` 等生成物是否应纳入 `.gitignore`，但不要在未确认前删除用户需要的验证截图。

## 6. 关键数据文件和路径

由 `AppPaths.cs` 定义：

- 数据目录：`%LocalAppData%\FireflyGameManager`
- SQLite 数据库：`%LocalAppData%\FireflyGameManager\app.db`
- 存档备份目录：`%LocalAppData%\FireflyGameManager\SaveBackups`
- WebDAV 设置：`%LocalAppData%\FireflyGameManager\webdav-settings.json`
- 外观设置：`%LocalAppData%\FireflyGameManager\appearance-settings.json`
- 应用设置：`%LocalAppData%\FireflyGameManager\app-settings.json`
- 封面缓存：`%LocalAppData%\FireflyGameManager\CoverCache`
- 在线资料封面缓存：`%LocalAppData%\FireflyGameManager\MetadataCache`
- Bangumi 账号：`%LocalAppData%\FireflyGameManager\bangumi-account.json`
- 本机 id：`%LocalAppData%\FireflyGameManager\machine-id.txt`

安全边界：

- `bangumi-account.json` 不应被 WebDAV 上传。
- Bangumi Token 不应写入数据库、导出包或日志。
- 手动下载数据库时应保护本机私有表和本机路径设置。

## 7. 重要架构入口

生产依赖组装在 `GameManager.App\MainWindow.xaml.cs`：

- `SqliteGameLibraryService`
- `SqliteSyncLogService`
- `WebDavGameSyncService`
- `LocalSaveBackupService`
- `JsonWebDavSettingsStore`
- `WebDavCloudMetadataPullService`
- `SaveSyncCoordinator`
- `BangumiApiClient`
- `JsonBangumiAccountStore`
- `BangumiGameMetadataProvider`
- `RemoteImageCacheService`
- `SystemTrayService`

页面路由在 `MainWindowViewModel`：

- 游戏库：`GameLibraryViewModel`
- 添加/修改游戏：`AddGameViewModel`
- 管理游戏库：`ManageGameLibraryViewModel`
- 游戏详情：`GameDetailViewModel`
- 同步中心：`WebDavSettingsViewModel`
- 设置中心：`AppearanceSettingsViewModel`

视图文件：

- `MainWindow.xaml`
- `Views\GameLibraryView.xaml`
- `Views\AddGameView.xaml`
- `Views\GameDetailView.xaml`
- `Views\ManageGameLibraryView.xaml`
- `Views\WebDavSettingsView.xaml`
- `Views\AppearanceSettingsView.xaml`

## 8. 测试和构建

当前测试入口：

```powershell
dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj
```

当前构建命令：

```powershell
dotnet build .\GameManager.App.Tests\GameManager.App.Tests.csproj --no-restore
```

最近验证状态：

- 180 项测试全部通过。
- 构建通过，0 警告，0 错误。
- `git diff --check` 无格式错误，但会提示一些 LF/CRLF 行尾警告。

注意：

- 如果 WPF 应用正在运行，测试或构建可能因为 `GameManager.App.exe` 被锁定而失败。
- 遇到类似 `file is being used by another process` 时，先关闭或停止 `GameManager.App` 进程，再重跑测试。

## 9. 当前 Git / 工作区注意事项

当前工作区是脏的，而且有很多新增文件尚未被 Git 跟踪。不要在新对话里误以为这些功能“不存在”。

特别注意：

- Bangumi 阶段 A/B 相关模型和服务当前显示为未跟踪文件，例如：
  - `GameManager.App\Models\BangumiAccount.cs`
  - `GameManager.App\Models\BangumiCollectionState.cs`
  - `GameManager.App\Models\ExternalGameMetadata.cs`
  - `GameManager.App\Models\GameMetadataSearchResult.cs`
  - `GameManager.App\Models\MetadataImportOptions.cs`
  - `GameManager.App\Services\BangumiApiClient.cs`
  - `GameManager.App\Services\BangumiApiException.cs`
  - `GameManager.App\Services\BangumiDtoMapper.cs`
  - `GameManager.App\Services\BangumiGameMetadataProvider.cs`
  - `GameManager.App\Services\JsonBangumiAccountStore.cs`
  - `GameManager.App\Services\RemoteImageCacheService.cs`
- `GameManager.App.Tests\Program.cs` 包含大量新增回归测试。
- `Bangumi账号与游戏信息导入实现方案.md` 已更新到 2026-06-17 的搜索修复状态。
- `artifacts`、`_verify`、`bin`、`obj` 里有大量验证产物和构建产物。

不要随意执行：

- `git reset --hard`
- `git checkout --`
- 递归删除项目目录

如需清理生成物，应先列出目标路径并确认只清理构建/截图产物，不要碰用户资料和源代码。

## 10. UI 风格约束

项目现在偏沉浸式、半透明、壁纸背景风格。做 UI 改动时需要注意：

- 不要再做浅绿色割裂风格。
- 不要嵌套大量卡片。
- 详情页、设置页、同步页应保持与主界面一致的暗色玻璃/半透明视觉。
- 按钮、输入框、列表应使用已有动态资源和共享样式。
- 左侧导航栏为紧凑图标导航，选中项显示文字。
- 顶部标题栏为自绘，不使用系统默认标题栏。

常用样式集中在：

- `GameManager.App\Styles\AppStyles.xaml`
- `MainWindow.xaml`
- 各 `Views\*.xaml`

## 11. 新对话建议开场提示

可以把下面这段直接发给新对话：

```text
请接手 D:\Study\Projects\FireflyGameManager 项目。先阅读根目录的 新对话上下文_FireflyGameManager.md、开发方案.md、Bangumi账号与游戏信息导入实现方案.md，再查看 MainWindow.xaml.cs、MainWindowViewModel.cs、BangumiApiClient.cs 和 GameManager.App.Tests\Program.cs。注意当前工作区有很多未提交/未跟踪但有效的 Stage A/B 实现文件，不要误删或回滚。当前项目是 .NET 10 WPF，测试命令是 dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj，最近状态为 180 项测试通过、构建 0 警告 0 错误。后续改动请保持 UI 风格统一，并优先加回归测试。
```

## 12. 推荐下一步

如果新对话继续开发，建议优先从以下任务中选一个：

1. 实现 Bangumi 阶段 C：外部资料 WebDAV 同步。
2. 继续优化 Bangumi 搜索：别名、原名、中文名多关键词补查，增加“更精确结果优先”的更多案例。
3. 清理 Git 状态和 `.gitignore`，区分源代码、方案文档、验证截图和构建产物。
4. 对设置页和详情页做一次 UI 一致性复查，避免功能堆叠。
5. 为 Bangumi 在线资料导入增加更明确的用户提示：哪些字段会覆盖，哪些字段只保存快照。

