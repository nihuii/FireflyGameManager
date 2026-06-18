# Firefly Game Manager：Bangumi 账号与游戏信息导入实现方案

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**编写日期：** 2026-06-07  
**目标版本：** Firefly Game Manager / .NET 10 / WPF  
**方案状态：** 待实施  

**目标：** 为 Firefly 增加可选的 Bangumi 账号连接、游戏资料搜索与导入、收藏状态管理能力，同时保持现有本地游戏库、WebDAV、启动统计和云存档功能完全独立可用。

**架构：** Bangumi 仅作为外部账号与游戏资料提供方，不作为 Firefly 的主账号系统或数据存储后端。游戏路径、启动配置、游玩记录、存档和 Firefly 设置继续由本地 SQLite 与 WebDAV 管理；Bangumi Token 仅保存在当前 Windows 用户本机，并使用 DPAPI 加密。

**技术栈：** C# 14、.NET 10 Windows、WPF、Microsoft.Data.Sqlite、HttpClient、JSON、Windows DPAPI、现有 WebDAV V2 协议。

---

## 1. 背景与可行性结论

Bangumi 提供游戏条目搜索、条目详情、用户资料和用户收藏相关 API，因此本项目可以实现：

- 使用 Bangumi Access Token 连接账号。
- 显示当前 Bangumi 用户的头像、昵称和用户名。
- 根据游戏名称检索 Bangumi 游戏条目。
- 导入封面、中文名、原名、简介、发行日期、开发商、发行商和标签。
- 将 Firefly 游戏关联到 Bangumi Subject ID。
- 查看和修改“想玩、在玩、玩过、搁置、抛弃”等 Bangumi 收藏状态。
- 将已导入的游戏资料通过 Firefly WebDAV 同步到其他设备。

Bangumi 不适合作为以下数据的存储端：

- 本地 EXE 路径、工作目录、启动参数。
- Firefly 设置和壁纸。
- 游戏存档压缩包。
- Firefly 自己统计的游玩时长和启动记录。

因此必须继续维持 Firefly 当前的“本地优先 + WebDAV 同步”架构。

---

## 2. 功能边界

### 2.1 本期实现

1. Bangumi 游戏资料搜索，无需登录即可使用。
2. 从搜索结果中选择条目，并导入游戏资料。
3. 支持为已有游戏重新关联或解除关联。
4. 使用 Bangumi Access Token 登录。
5. Token 使用 DPAPI 加密保存。
6. 显示当前登录用户资料。
7. 在游戏详情页显示和修改 Bangumi 收藏状态。
8. 已导入的外部游戏资料纳入 Firefly WebDAV V2 元数据同步。
9. Bangumi 服务不可用时，不影响游戏启动、本地备份和 WebDAV 存档同步。

### 2.2 本期不实现

1. 不使用 Bangumi 替代 Firefly WebDAV。
2. 不保存 Bangumi 用户密码。
3. 不把 Bangumi Access Token 上传到 WebDAV。
4. 不根据游戏名称自动采用第一条搜索结果。
5. 不自动覆盖用户手动修改的游戏名称和封面。
6. 不实现 Firefly 自建云账号服务器。
7. 不在桌面客户端内硬编码共享 OAuth `client_secret`。

### 2.3 后续可选

1. 使用 Firefly OAuth 中转服务实现浏览器一键授权。
2. 接入 VNDB、SteamGridDB、IGDB 等其他资料源。
3. 批量为现有游戏匹配 Bangumi 条目。
4. 导入用户 Bangumi 收藏列表并生成本地游戏候选项。
5. 支持 Bangumi 评分、评论、标签和进度的完整编辑。

---

## 3. 核心设计原则

### 3.1 数据所有权

| 数据 | 权威来源 | 是否上传 Firefly WebDAV |
| --- | --- | --- |
| EXE、游戏目录、存档目录 | 当前设备 | 是，按设备路径文件同步 |
| 存档 ZIP、Manifest | Firefly | 是 |
| Firefly 游玩记录 | Firefly | 是 |
| Bangumi Access Token | 当前 Windows 用户 | 否 |
| Bangumi 用户资料缓存 | Bangumi | 否 |
| Bangumi 收藏状态 | Bangumi | 否，仅保留本地缓存 |
| 游戏简介、标签、发行日期等资料 | Bangumi 导入快照 | 是 |
| Firefly 游戏名称与封面 | 用户 | 是 |

### 3.2 用户修改优先

Bangumi 刷新资料时：

- 不自动覆盖 Firefly 当前游戏名称。
- 不自动覆盖 Firefly 当前封面。
- 不自动删除用户手动填写的资料。
- 必须先展示差异，由用户选择要更新的字段。
- Bangumi 条目解除关联后，保留已经导入到 Firefly 的资料。

### 3.3 本地功能不依赖 Bangumi

以下操作不得因为 Bangumi 未登录、超时、限流或故障而失败：

- 打开游戏库。
- 启动游戏。
- 统计游玩时长。
- 本地存档备份与恢复。
- WebDAV 存档同步。
- 编辑本机游戏路径。

---

## 4. 推荐实施顺序

本功能应拆成四个可独立验收的阶段。

### 阶段 A：游戏资料搜索与导入

无需登录即可搜索 Bangumi，并将资料导入 Firefly。

### 阶段 B：Bangumi Token 登录与收藏状态

增加账号设置、登录状态和收藏状态管理。

### 阶段 C：外部资料 WebDAV 兼容同步

将已导入的资料同步到其他 Firefly 设备，但绝不同步账号 Token。

### 阶段 D：可选 OAuth 快捷登录

仅在具备安全 Token 交换方案后实施。

推荐优先完成阶段 A。它对用户价值最高，也不会引入账号授权风险。

---

## 5. Bangumi API 接入约定

### 5.1 基础约定

```text
API Base URL: https://api.bgm.tv
Authorization: Bearer {accessToken}
Accept: application/json
User-Agent: FireflyGameManager/{version} ({project-url-or-contact})
```

必须使用可识别的 `User-Agent`，不得伪装浏览器或使用空 User-Agent。

### 5.2 计划使用的能力

| 用途 | 方法与路径 |
| --- | --- |
| 验证 Token、获取当前用户 | `GET /v0/me` |
| 搜索游戏条目 | `POST /v0/search/subjects` |
| 获取条目详情 | `GET /v0/subjects/{subjectId}` |
| 获取用户收藏列表 | `GET /v0/users/{username}/collections` |
| 获取某条目收藏状态 | `GET /v0/users/{username}/collections/{subjectId}` |
| 创建收藏 | `POST /v0/users/-/collections/{subjectId}` |
| 修改收藏 | `PATCH /v0/users/-/collections/{subjectId}` |

实施时应以 Bangumi 当前 OpenAPI 文档为准，并通过 API 合约测试固定实际使用的字段。

### 5.3 请求策略

- 搜索由用户点击按钮触发，不对每次键盘输入发送请求。
- 默认超时 15 秒。
- 对 HTTP 429 按 `Retry-After` 提示用户稍后重试，不进行无限自动重试。
- 对单次 5xx 最多自动重试一次。
- 搜索结果缓存 10 分钟。
- 条目详情导入后持久化到 SQLite，后续不依赖在线加载。
- 取消页面或关闭搜索面板时取消未完成请求。

---

## 6. 登录方案

### 6.1 第一版：Access Token 登录

设置页面提供：

```text
Bangumi Access Token 输入框
[连接 Bangumi]
[退出登录]
```

登录流程：

```text
用户输入 Token
→ 调用 GET /v0/me
→ 验证成功
→ DPAPI 加密 Token
→ 保存 bangumi-account.json
→ 显示头像、昵称、用户名
```

不得要求或保存 Bangumi 密码。

### 6.2 Token 本地存储

新增文件：

```text
%LocalAppData%\FireflyGameManager\bangumi-account.json
```

建议结构：

```json
{
  "schemaVersion": 1,
  "username": "example",
  "nickname": "Example",
  "avatarUrl": "https://...",
  "encryptedAccessToken": "DPAPI_BASE64",
  "verifiedAtUtc": "2026-06-07T12:00:00Z"
}
```

`bangumi-account.json` 不得包含明文 Token，也不得被数据导出或 WebDAV 同步功能上传。

### 6.3 OAuth 快捷登录限制

桌面应用内置共享 `client_secret` 可以被反编译提取，因此不应直接在 Firefly 客户端中嵌入生产 OAuth 密钥。

若后续实现浏览器一键登录，应采用以下任一方案：

1. 用户自行填写 Bangumi OAuth Client ID 与 Secret。
2. 部署 Firefly 自有 HTTPS OAuth 中转服务，由服务端保存 Secret。
3. Bangumi 官方未来支持适合公共客户端的 PKCE 流程后再接入。

在满足上述条件前，Access Token 登录是更安全、实现成本更低的方案。

---

## 7. 数据模型设计

### 7.1 外部游戏资料模型

新增：

```csharp
public sealed class ExternalGameMetadata
{
    public string Provider { get; init; } = "bangumi";
    public string SubjectId { get; init; } = string.Empty;
    public bool IsLinked { get; init; } = true;
    public string OriginalName { get; init; } = string.Empty;
    public string LocalizedName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string ReleaseDate { get; init; } = string.Empty;
    public string Developer { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string ImageUrl { get; init; } = string.Empty;
    public string SubjectUrl { get; init; } = string.Empty;
    public DateTime SourceUpdatedAtUtc { get; init; }
}
```

`Game`、`AddGameRequest` 和 `UpdateGameRequest` 增加可空的：

```csharp
ExternalGameMetadata? ExternalMetadata
```

所有新增构造参数放在参数列表末尾并提供默认值，保证旧代码和旧测试兼容。

### 7.2 搜索结果模型

新增：

```csharp
public sealed record GameMetadataSearchResult(
    string Provider,
    string SubjectId,
    string Name,
    string LocalizedName,
    string ReleaseDate,
    string ImageUrl,
    string SummaryPreview);
```

搜索结果只保存展示需要的轻量字段。用户选择结果后，再请求完整详情。

### 7.3 Bangumi 账号模型

新增：

```csharp
public sealed record BangumiAccount(
    string Username,
    string Nickname,
    string AvatarUrl,
    string AccessToken,
    DateTime VerifiedAtUtc);
```

`AccessToken` 只允许存在于进程内存和加密后的本地账号文件中。

### 7.4 收藏状态模型

新增：

```csharp
public enum BangumiCollectionType
{
    None,
    Wish,
    Collect,
    Doing,
    OnHold,
    Dropped
}

public sealed record BangumiCollectionState(
    string GameId,
    string SubjectId,
    string Username,
    BangumiCollectionType Type,
    int Rating,
    string Comment,
    DateTime? RemoteUpdatedAtUtc,
    DateTime LastSyncedAtUtc);
```

第一版 UI 重点支持收藏状态；评分和评论先完成数据层及 API 支持，可在后续 UI 中开放。

`IsLinked=false` 表示保留已经导入的资料快照，但停止在线刷新与 Bangumi 收藏状态操作。这样“解除关联”不会让详情页中已经导入的简介、日期和标签突然消失。

---

## 8. SQLite 兼容迁移

新增表，不直接向 `games` 表塞入大量外部资料字段。

```sql
CREATE TABLE IF NOT EXISTS game_external_metadata (
    game_id TEXT PRIMARY KEY,
    provider TEXT NOT NULL,
    subject_id TEXT NOT NULL,
    is_linked INTEGER NOT NULL DEFAULT 1,
    original_name TEXT NOT NULL DEFAULT '',
    localized_name TEXT NOT NULL DEFAULT '',
    summary TEXT NOT NULL DEFAULT '',
    release_date TEXT NOT NULL DEFAULT '',
    developer TEXT NOT NULL DEFAULT '',
    publisher TEXT NOT NULL DEFAULT '',
    tags_json TEXT NOT NULL DEFAULT '[]',
    image_url TEXT NOT NULL DEFAULT '',
    subject_url TEXT NOT NULL DEFAULT '',
    source_updated_at TEXT NOT NULL,
    imported_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_game_external_metadata_provider_subject
ON game_external_metadata(provider, subject_id);

CREATE TABLE IF NOT EXISTS bangumi_collection_states (
    game_id TEXT PRIMARY KEY,
    subject_id TEXT NOT NULL,
    username TEXT NOT NULL,
    collection_type TEXT NOT NULL DEFAULT 'none',
    rating INTEGER NOT NULL DEFAULT 0,
    comment TEXT NOT NULL DEFAULT '',
    remote_updated_at TEXT,
    last_synced_at TEXT NOT NULL
);
```

兼容规则：

- 旧数据库迁移后，所有旧游戏的 `ExternalMetadata` 均为空。
- 不修改原有游戏 ID。
- 不改变现有游玩记录和存档同步状态。
- 外部资料损坏时仅忽略该游戏的外部资料，不影响游戏本体加载。
- 删除游戏时删除本地外部资料和收藏缓存，但不默认删除 Bangumi 远端收藏。
- 解除 Bangumi 关联时将 `is_linked` 设为 `0`，保留资料快照。

---

## 9. 服务与接口设计

### 9.1 元数据提供方接口

新增：

```csharp
public interface IGameMetadataProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<GameMetadataSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<ExternalGameMetadata?> GetDetailsAsync(
        string subjectId,
        CancellationToken cancellationToken = default);
}
```

第一版实现：

```text
BangumiGameMetadataProvider
```

未来的 VNDB、SteamGridDB、IGDB 均通过同一接口接入。

### 9.2 Bangumi API 客户端

新增：

```csharp
public interface IBangumiApiClient
{
    Task<BangumiAccount> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameMetadataSearchResult>> SearchGamesAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<ExternalGameMetadata?> GetGameDetailsAsync(
        string subjectId,
        CancellationToken cancellationToken = default);

    Task<BangumiCollectionState?> GetCollectionAsync(
        BangumiAccount account,
        string gameId,
        string subjectId,
        CancellationToken cancellationToken = default);

    Task<BangumiCollectionState> SaveCollectionAsync(
        BangumiAccount account,
        BangumiCollectionState state,
        CancellationToken cancellationToken = default);
}
```

实现类：

```text
BangumiApiClient
```

要求：

- 所有请求设置 Firefly User-Agent。
- 使用注入的 `HttpClient` 工厂，便于测试。
- 对响应 JSON 使用专用 DTO，不直接反序列化为 Firefly 业务模型。
- DTO 到业务模型的映射集中在 `BangumiDtoMapper`。
- API 错误转换为用户可理解的信息，不在 UI 中展示完整堆栈。

### 9.3 账号存储接口

新增：

```csharp
public interface IBangumiAccountStore
{
    BangumiAccount? Load();
    void Save(BangumiAccount account);
    void Clear();
}
```

实现：

```text
JsonBangumiAccountStore
```

复用现有 `ISecretProtector`。同时将 `DpapiSecretProtector` 扩展为支持可选用途字符串：

```csharp
new DpapiSecretProtector("FireflyGameManager.Bangumi.v1")
```

默认构造行为必须保持现有 WebDAV 密码可解密。

### 9.4 外部资料仓储

扩展 `IGameLibraryService`：

```csharp
ExternalGameMetadata? GetExternalMetadata(string gameId);
Game UpdateExternalMetadata(string gameId, ExternalGameMetadata? metadata);
BangumiCollectionState? GetBangumiCollectionState(string gameId);
void SaveBangumiCollectionState(BangumiCollectionState state);
```

`SqliteGameLibraryService` 与 `InMemoryGameLibraryService` 必须同时实现，以保持测试和设计时数据兼容。

### 9.5 封面下载服务

新增：

```csharp
public interface IRemoteImageCacheService
{
    Task<string?> DownloadForImportAsync(
        string provider,
        string subjectId,
        string imageUrl,
        CancellationToken cancellationToken = default);
}
```

下载规则：

- 仅允许 HTTPS。
- 最大响应大小 8 MB。
- 只接受可解码的 JPEG 或 PNG。
- 使用同目录临时文件下载后原子替换。
- 本地文件名必须经过 `SafePathSegment`。
- 失败时保持原封面，不阻止保存游戏。

建议缓存路径：

```text
%LocalAppData%\FireflyGameManager\MetadataCache\bangumi\{subjectId}\cover.jpg
```

游戏保存后继续使用现有 `LocalCoverCacheService` 复制到按游戏 ID 管理的封面缓存。

---

## 10. UI 与交互设计

### 10.1 设置中心：账号与信息源

在设置中心增加“账号与信息源”分类。

未登录状态：

```text
Bangumi
用于同步收藏状态，并为游戏搜索封面与介绍。

[Access Token 输入框]
[连接 Bangumi]
```

登录状态：

```text
[头像] 昵称
       @username
       最近验证：2026-06-07 20:00

[重新验证] [退出登录]
```

要求：

- Token 使用 PasswordBox。
- 登录失败时保留用户输入，但不落盘。
- 退出登录只删除本机账号凭据，不删除已导入资料。
- 搜索游戏资料不强制要求登录。

### 10.2 添加与编辑游戏：搜索资料

在游戏名称字段右侧增加：

```text
[搜索在线资料]
```

点击后在当前内容区显示资料搜索面板：

```text
搜索词：[冬日狂想曲             ] [搜索]

结果：
[封面] 中文名 / 原名 / 日期 / 简介摘要
[封面] 中文名 / 原名 / 日期 / 简介摘要

[查看详情] [应用此资料]
```

用户应用资料时弹出字段选择：

```text
[x] 中文名称
[x] 游戏简介
[x] 封面
[x] 发行日期
[x] 开发商与发行商
[x] 标签
```

编辑已有游戏时，默认不勾选覆盖游戏名称和封面；添加新游戏时默认勾选。

### 10.3 游戏详情页：资料区

在现有详情页增加简洁的“游戏资料”区域：

```text
简介
发行日期 · 开发商 · 发行商
标签

资料来源：Bangumi #12345
[在 Bangumi 查看] [刷新资料] [解除关联]
```

简介默认折叠为适当高度，用户可展开，避免详情页过长。

### 10.4 游戏详情页：收藏状态

仅在已登录、游戏资料 `IsLinked=true` 且已关联 Bangumi 时显示：

```text
Bangumi 收藏状态
[未收藏] [想玩] [在玩] [玩过] [搁置] [抛弃]
```

状态修改必须由用户主动点击触发，不根据 Firefly 启动游戏自动修改 Bangumi 收藏。

### 10.5 UI 风格

- 使用现有动态主题资源、SectionCardStyle、SettingsOptionRowStyle 和按钮样式。
- 不增加新的顶级导航按钮。
- 搜索结果使用紧凑列表，不使用大量嵌套卡片。
- 网络加载状态使用现有状态条视觉语言。
- 所有错误都显示为页面内状态，不使用连续弹窗打断用户。

---

## 11. 游戏资料导入流程

```text
用户输入或由 EXE 推断游戏名称
→ 点击“搜索在线资料”
→ BangumiGameMetadataProvider 搜索游戏条目
→ 展示候选结果
→ 用户确认某个条目
→ 获取完整条目详情
→ 用户选择需要导入的字段
→ 下载并校验封面
→ 更新 AddGameViewModel 或现有游戏
→ 保存 SQLite 外部资料
→ 更新全局元数据时间戳
→ 后续 WebDAV 同步时上传资料快照
```

不得跳过“用户确认候选条目”步骤。

---

## 12. 收藏状态同步流程

### 12.1 打开详情页

```text
检查是否登录 Bangumi
→ 检查游戏是否有关联 Subject ID
→ 优先显示 SQLite 缓存状态
→ 后台请求远端收藏状态
→ 成功则刷新缓存和 UI
→ 失败则保留缓存，并显示“无法刷新”
```

### 12.2 修改收藏状态

```text
用户选择新状态
→ 禁用状态按钮，显示保存中
→ 调用 Bangumi API
→ 成功后保存 SQLite 缓存
→ 失败则恢复原状态并显示错误
```

Bangumi 收藏失败不得改变 Firefly 本地游戏数据。

---

## 13. WebDAV V2 兼容同步

### 13.1 同步内容

在 `GameCloudMetadata` 中增加可空字段：

```csharp
public ExternalGameMetadata? ExternalMetadata { get; set; }
```

旧客户端忽略未知 JSON 字段；新客户端读取不到该字段时视为未关联外部资料。

### 13.2 冲突规则

- 外部资料变化属于全局游戏资料变化，应推进全局 `UpdatedAtUtc`。
- 设备路径、管理员启动、存档目录等本机字段仍不得推进全局时间戳。
- 较新全局元数据胜出时，同时应用其外部资料。
- `ExternalMetadata.IsLinked=false` 表示解除在线关联但保留资料快照。
- 远端外部资料为空且远端全局元数据较新时，表示删除外部资料快照。
- Bangumi Token、用户资料缓存和收藏状态不得写入 WebDAV。

### 13.3 封面规则

- Bangumi 导入封面后，封面仍按 Firefly 当前封面缓存与冲突策略同步。
- 较旧设备不得用旧 Bangumi 封面覆盖较新云端封面。
- 如果用户选择“刷新资料但保留现有封面”，不得上传 Bangumi 新封面。

---

## 14. 安全与隐私

1. 不保存 Bangumi 密码。
2. Access Token 使用当前 Windows 用户范围的 DPAPI 加密。
3. Token 不写入日志、异常信息、WebDAV 或数据导出 ZIP。
4. 所有 Bangumi API 请求使用 HTTPS。
5. 外部图片限制大小并验证格式。
6. 简介和标签按纯文本显示，不执行 HTML。
7. Bangumi 账号退出后清空内存中的 Token。
8. 所有网络失败均不得影响本地游戏启动与存档操作。
9. 默认不自动修改用户 Bangumi 收藏状态。
10. 对可能包含成人内容的条目，不自动展示远程大图；由用户确认后再下载封面。

---

## 15. 错误处理

| 场景 | 处理方式 |
| --- | --- |
| 搜索无结果 | 提示修改搜索词，允许继续手动添加 |
| Bangumi 超时 | 显示超时状态，不阻止保存游戏 |
| Token 无效 | 不保存 Token，提示重新生成 |
| Token 被撤销 | 保留已导入资料，账号状态变为需重新连接 |
| HTTP 429 | 显示限流提示和可重试时间 |
| 条目详情缺少字段 | 仅导入存在字段 |
| 封面下载失败 | 保留当前封面或空封面 |
| 图片格式不支持 | 不保存图片，资料文字仍可导入 |
| 收藏状态保存失败 | 恢复旧状态并保留缓存 |
| WebDAV 同步到旧客户端 | 旧客户端忽略外部资料字段 |
| 外部资料 JSON 损坏 | 忽略该资料，不影响游戏加载 |

---

## 16. 文件结构规划

### 16.1 新增模型

```text
GameManager.App/Models/ExternalGameMetadata.cs
GameManager.App/Models/GameMetadataSearchResult.cs
GameManager.App/Models/BangumiAccount.cs
GameManager.App/Models/BangumiCollectionState.cs
GameManager.App/Models/MetadataImportOptions.cs
```

### 16.2 新增服务

```text
GameManager.App/Services/IGameMetadataProvider.cs
GameManager.App/Services/IBangumiApiClient.cs
GameManager.App/Services/BangumiApiClient.cs
GameManager.App/Services/BangumiDtoMapper.cs
GameManager.App/Services/BangumiGameMetadataProvider.cs
GameManager.App/Services/IBangumiAccountStore.cs
GameManager.App/Services/JsonBangumiAccountStore.cs
GameManager.App/Services/IRemoteImageCacheService.cs
GameManager.App/Services/RemoteImageCacheService.cs
```

### 16.3 新增 ViewModel 与视图

```text
GameManager.App/ViewModels/GameMetadataSearchViewModel.cs
GameManager.App/ViewModels/BangumiAccountSettingsViewModel.cs
GameManager.App/ViewModels/BangumiCollectionViewModel.cs
GameManager.App/Views/GameMetadataSearchView.xaml
GameManager.App/Views/GameMetadataSearchView.xaml.cs
GameManager.App/Views/BangumiAccountSettingsView.xaml
GameManager.App/Views/BangumiAccountSettingsView.xaml.cs
```

### 16.4 修改现有文件

```text
GameManager.App/Services/AppPaths.cs
GameManager.App/Services/DpapiSecretProtector.cs
GameManager.App/Services/LocalDataMaintenanceService.cs
GameManager.App/Services/IGameLibraryService.cs
GameManager.App/Services/SqliteGameLibraryService.cs
GameManager.App/Services/InMemoryGameLibraryService.cs
GameManager.App/Services/WebDavGameSyncService.cs
GameManager.App/Services/WebDavCloudMetadataPullService.cs
GameManager.App/Models/Game.cs
GameManager.App/Models/AddGameRequest.cs
GameManager.App/Models/UpdateGameRequest.cs
GameManager.App/Models/GameCloudMetadata.cs
GameManager.App/ViewModels/AddGameViewModel.cs
GameManager.App/ViewModels/GameDetailViewModel.cs
GameManager.App/ViewModels/AppearanceSettingsViewModel.cs
GameManager.App/ViewModels/MainWindowViewModel.cs
GameManager.App/Views/AddGameView.xaml
GameManager.App/Views/GameDetailView.xaml
GameManager.App/Views/AppearanceSettingsView.xaml
GameManager.App/MainWindow.xaml.cs
GameManager.App.Tests/Program.cs
```

---

## 17. 详细实施计划

### Task 1：建立外部资料模型与兼容 SQLite 迁移

**文件：**

- Create: `GameManager.App/Models/ExternalGameMetadata.cs`
- Create: `GameManager.App/Models/GameMetadataSearchResult.cs`
- Create: `GameManager.App/Models/MetadataImportOptions.cs`
- Modify: `GameManager.App/Models/Game.cs`
- Modify: `GameManager.App/Models/AddGameRequest.cs`
- Modify: `GameManager.App/Models/UpdateGameRequest.cs`
- Modify: `GameManager.App/Services/IGameLibraryService.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Modify: `GameManager.App/Services/InMemoryGameLibraryService.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写失败测试：旧数据库升级后仍能加载所有游戏。
- [ ] 编写失败测试：外部资料可以保存、重新打开和解除关联。
- [ ] 编写失败测试：外部资料更新推进全局元数据时间戳，路径编辑仍不推进。
- [ ] 创建模型并为现有构造函数增加末尾可选参数。
- [ ] 创建 `game_external_metadata` 表和索引。
- [ ] 在 SQLite 和内存服务中实现读取、保存与解除关联。
- [ ] 运行：

```powershell
dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj
```

- [ ] 预期：新增外部资料测试通过，现有测试全部通过。

### Task 2：实现 Bangumi HTTP 客户端和资料提供方

**文件：**

- Create: `GameManager.App/Services/IGameMetadataProvider.cs`
- Create: `GameManager.App/Services/IBangumiApiClient.cs`
- Create: `GameManager.App/Services/BangumiApiClient.cs`
- Create: `GameManager.App/Services/BangumiDtoMapper.cs`
- Create: `GameManager.App/Services/BangumiGameMetadataProvider.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写 HTTP 假处理器测试，验证搜索请求路径、方法、请求体和 User-Agent。
- [ ] 编写测试，验证只返回游戏类型候选项。
- [ ] 编写测试，验证空简介、空图片和不完整 Infobox 可安全映射。
- [ ] 编写测试，验证 401、429、5xx 和超时转换为明确错误。
- [ ] 实现 `BangumiApiClient`，所有 JSON DTO 保持内部私有或独立 DTO。
- [ ] 实现 `BangumiDtoMapper`，集中提取开发商、发行商、日期和标签。
- [ ] 实现 `BangumiGameMetadataProvider`，不要求登录即可搜索和读取公开详情。
- [ ] 运行完整测试。

### Task 3：实现安全封面下载

**文件：**

- Modify: `GameManager.App/Services/AppPaths.cs`
- Create: `GameManager.App/Services/IRemoteImageCacheService.cs`
- Create: `GameManager.App/Services/RemoteImageCacheService.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写测试，验证拒绝 HTTP URL、超大响应和无效图片。
- [ ] 编写测试，验证保留安全文件名并使用原子写入。
- [ ] 编写测试，验证下载失败不会删除已有封面。
- [ ] 新增 `AppPaths.MetadataCacheDirectory`。
- [ ] 实现 HTTPS、大小、格式和临时文件安全校验。
- [ ] 运行完整测试。

### Task 4：实现添加与编辑页面的资料搜索

**文件：**

- Create: `GameManager.App/ViewModels/GameMetadataSearchViewModel.cs`
- Create: `GameManager.App/Views/GameMetadataSearchView.xaml`
- Create: `GameManager.App/Views/GameMetadataSearchView.xaml.cs`
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`
- Modify: `GameManager.App/Views/AddGameView.xaml`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写测试，验证搜索默认使用当前游戏名称。
- [ ] 编写测试，验证搜索期间按钮禁用、取消请求和错误状态。
- [ ] 编写测试，验证必须由用户选择候选项。
- [ ] 编写测试，验证编辑游戏时默认不覆盖名称和封面。
- [ ] 实现资料搜索子 ViewModel。
- [ ] 在添加/编辑页中嵌入搜索面板。
- [ ] 应用候选项时保存外部资料，并按选项复制名称与封面。
- [ ] 使用现有动态样式完成视觉一致性。
- [ ] 运行测试并启动 WPF 应用进行添加、编辑页面视觉核验。

### Task 5：在游戏详情页展示和刷新资料

**文件：**

- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/GameDetailView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写测试，验证有关联资料时显示资料区。
- [ ] 编写测试，验证无资料时不显示空白资料区。
- [ ] 编写测试，验证刷新资料不自动覆盖名称与封面。
- [ ] 编写测试，验证解除关联后保留已导入文字和封面。
- [ ] 增加简介、日期、开发商、发行商、标签和来源链接。
- [ ] 增加刷新资料与解除关联命令。
- [ ] 运行测试并进行详情页视觉核验。

### Task 6：实现 Bangumi Token 登录

**文件：**

- Create: `GameManager.App/Models/BangumiAccount.cs`
- Create: `GameManager.App/Services/IBangumiAccountStore.cs`
- Create: `GameManager.App/Services/JsonBangumiAccountStore.cs`
- Modify: `GameManager.App/Services/DpapiSecretProtector.cs`
- Modify: `GameManager.App/Services/AppPaths.cs`
- Modify: `GameManager.App/Services/LocalDataMaintenanceService.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写测试，验证 `/v0/me` 成功后保存用户资料。
- [ ] 编写测试，验证无效 Token 不落盘。
- [ ] 编写测试，验证账号 JSON 不包含明文 Token。
- [ ] 编写测试，验证数据导出 ZIP 不包含 `bangumi-account.json`。
- [ ] 编写测试，验证旧 WebDAV 加密密码在 DPAPI 扩展后仍可读取。
- [ ] 增加用途隔离的 DPAPI 构造方式，并保留默认兼容行为。
- [ ] 实现账号加载、保存、清除和损坏配置降级。
- [ ] 确保数据导出功能明确排除 `bangumi-account.json`。
- [ ] 运行完整测试。

### Task 7：实现设置中心的账号与信息源页面

**文件：**

- Create: `GameManager.App/ViewModels/BangumiAccountSettingsViewModel.cs`
- Create: `GameManager.App/Views/BangumiAccountSettingsView.xaml`
- Create: `GameManager.App/Views/BangumiAccountSettingsView.xaml.cs`
- Modify: `GameManager.App/ViewModels/AppearanceSettingsViewModel.cs`
- Modify: `GameManager.App/Views/AppearanceSettingsView.xaml`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `GameManager.App/MainWindow.xaml.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写测试，验证未登录、登录中、已登录和失效状态。
- [ ] 编写测试，验证退出登录不会删除已导入游戏资料。
- [ ] 将“账号与信息源”加入设置分类入口。
- [ ] 使用 PasswordBox 输入 Token。
- [ ] 显示头像、昵称、用户名和最近验证时间。
- [ ] 增加重新验证和退出登录命令。
- [ ] 运行测试并对设置页面进行视觉核验。

### Task 8：实现 Bangumi 收藏状态管理

**文件：**

- Create: `GameManager.App/Models/BangumiCollectionState.cs`
- Create: `GameManager.App/ViewModels/BangumiCollectionViewModel.cs`
- Modify: `GameManager.App/Services/IBangumiApiClient.cs`
- Modify: `GameManager.App/Services/BangumiApiClient.cs`
- Modify: `GameManager.App/Services/IGameLibraryService.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Modify: `GameManager.App/Services/InMemoryGameLibraryService.cs`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/GameDetailView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写测试，验证打开详情页时先展示缓存，再刷新远端。
- [ ] 编写测试，验证未登录或未关联游戏时隐藏收藏控件。
- [ ] 编写测试，验证状态修改成功后更新缓存。
- [ ] 编写测试，验证远端保存失败时恢复旧状态。
- [ ] 创建 `bangumi_collection_states` 表。
- [ ] 实现获取、创建和修改收藏状态。
- [ ] 在详情页增加紧凑状态选择控件。
- [ ] 运行完整测试并视觉核验。

### Task 9：将外部资料纳入 WebDAV V2

**文件：**

- Modify: `GameManager.App/Models/GameCloudMetadata.cs`
- Modify: `GameManager.App/Services/WebDavGameSyncService.cs`
- Modify: `GameManager.App/Services/WebDavCloudMetadataPullService.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] 编写测试，验证上传 metadata.json 包含可空外部资料。
- [ ] 编写测试，验证旧云端 metadata.json 不含字段时仍可读取。
- [ ] 编写测试，验证较新远端资料胜出、较旧远端资料不覆盖本地。
- [ ] 编写测试，验证解除关联可以同步。
- [ ] 编写测试，验证 Bangumi Token 和收藏状态永不进入 WebDAV 请求。
- [ ] 将外部资料加入 V2 全局元数据与合并策略。
- [ ] 保持现有封面冲突策略和机器路径隔离规则。
- [ ] 运行完整测试。

### Task 10：最终安全、回归与视觉核验

**文件：**

- Modify: `GameManager.App.Tests/Program.cs`
- Modify: `开发方案.md`
- Modify: `Bangumi账号与游戏信息导入实现方案.md`

- [ ] 运行完整控制台回归测试。
- [ ] 运行：

```powershell
dotnet build .\GameManager.App.Tests\GameManager.App.Tests.csproj --no-restore
```

- [ ] 预期：0 警告、0 错误。
- [ ] 扫描工作区，确认没有明文 Bangumi Token。
- [ ] 验证 Bangumi 故障时仍能启动游戏、备份存档和使用 WebDAV。
- [ ] 验证旧 SQLite 数据和旧 WebDAV V2 数据兼容。
- [ ] 启动 WPF 应用，在 100%、125%、150% DPI 下检查：
  - 设置账号页。
  - 添加游戏资料搜索。
  - 编辑游戏资料搜索。
  - 游戏详情资料区。
  - 收藏状态控件。
- [ ] 更新方案完成状态和测试数量。

---

## 18. 测试矩阵

### 18.1 API 合约测试

- 搜索请求包含正确方法、路径、JSON 和 User-Agent。
- 详情请求正确映射名称、简介、图片和 Infobox。
- 收藏状态枚举与 Bangumi API 值双向映射正确。
- 401、403、404、429、5xx 和超时均有明确结果。

### 18.2 数据安全测试

- `bangumi-account.json` 不包含明文 Token。
- WebDAV 上传请求不包含 Token。
- 数据导出 ZIP 不包含 Token。
- 日志消息不包含 Token。
- 外部图片不会写出 `MetadataCache` 目录。

### 18.3 兼容测试

- 无外部资料的旧游戏正常加载。
- 旧数据库自动迁移且不丢失游戏、游玩记录和同步状态。
- 旧 WebDAV metadata.json 正常加载。
- 新 metadata.json 被旧版 Firefly 忽略未知字段时不受影响。
- Bangumi 资料解除关联后，游戏本体仍存在。

### 18.4 UI 测试

- 搜索无结果、加载中、失败和成功状态完整。
- 长游戏名称、长简介和大量标签不会溢出。
- 无封面候选项有统一占位样式。
- 壁纸透明模式下文字和按钮仍可读。
- 未登录状态不会出现不可用的收藏操作。

---

## 19. 验收标准

阶段 A 验收：

- 用户可以在添加或编辑游戏时搜索 Bangumi。
- 用户可以明确选择候选条目和要导入的字段。
- 封面与介绍导入后可离线显示。
- 搜索或下载失败不影响手动添加游戏。

阶段 B 验收：

- 用户可以使用 Access Token 连接 Bangumi。
- Token 加密保存且不会进入 WebDAV、日志和导出文件。
- 已关联游戏可以读取和修改收藏状态。
- 退出账号后本地游戏和已导入资料不受影响。

阶段 C 验收：

- 外部资料可以通过 Firefly WebDAV 同步到另一台设备。
- 较旧设备不会覆盖较新资料与封面。
- 账号凭据和收藏状态不会通过 Firefly WebDAV 同步。

整体验收：

- 所有已有测试继续通过。
- 新增功能有完整自动化测试。
- 构建为 0 警告、0 错误。
- WPF 界面与现有 UI 风格统一、简洁、无重叠。

---

## 20. 风险与应对

| 风险 | 应对 |
| --- | --- |
| Bangumi API 发生字段变化 | 使用 DTO 映射层与合约测试，不让 API DTO 进入业务模型 |
| 搜索结果匹配错误 | 强制用户选择，不自动采用第一条 |
| Token 泄露 | DPAPI 加密，禁止日志、导出和 WebDAV 上传 |
| OAuth Secret 被反编译 | 第一版使用 Access Token，OAuth 仅通过安全中转实现 |
| 成人内容图片意外展示 | 默认不自动下载，用户确认后导入 |
| 外部资料覆盖用户编辑 | 字段选择导入，刷新时默认保留名称和封面 |
| Bangumi 服务不可用 | 所有外部功能可降级，本地核心功能完全独立 |
| 详情页功能堆砌 | 简介折叠、收藏状态紧凑显示、资料来源操作放次级区域 |

---

## 21. 阶段 A / B 实施状态（2026-06-08）

阶段 A 已实现：

- 已新增外部游戏资料模型、搜索结果模型，以及 SQLite 兼容迁移。
- 添加和编辑游戏页面已支持显式搜索 Bangumi、显示封面/名称/日期/简介候选信息、查看完整详情，并分别选择是否导入名称、封面、简介、发行日期、开发商、发行商和标签。
- Bangumi 候选列表使用固定高度和单行紧凑摘要，真实搜索结果中的换行简介不会挤压相邻条目。
- Bangumi 搜索会将完全同名、忽略空白/标点/符号后的同名、前缀命中和包含命中的条目重新排序；当 CJK 查询在 v0 搜索中没有强标题命中时，会补充请求旧版 `search/subject` 接口并合并去重，使结果更接近 Bangumi 网页搜索。
- 编辑现有游戏时默认不覆盖本地名称和封面。
- 搜索结果缓存 10 分钟；搜索、详情读取和封面下载支持取消，离开添加/编辑页面时会取消未完成请求。
- Bangumi 请求默认 15 秒超时，单次 5xx 自动重试一次，并为 401、403、404、429、5xx 和超时提供可理解的页面内错误。
- 已实现公开详情映射、HTTPS 图片大小限制、PNG/JPEG 文件头和真实解码校验、原子缓存写入。
- 添加/编辑页已在自身资源中提供延迟模板所需的可见性转换器，避免首次进入页面时发生 XAML 资源解析崩溃。
- 游戏详情页已显示简介、日期、开发商、发行商、标签与来源链接；长简介支持展开/收起。
- 详情页刷新资料时先展示变化字段并等待用户确认，只有被选中的字段才会更新；名称和封面只有在用户明确勾选后才会覆盖。
- 解除关联会保留已经导入的本地资料快照。

阶段 B 已实现：

- 已实现 Bangumi Access Token 登录、重新验证和退出登录。
- Token 使用独立用途的 DPAPI 加密保存；本地数据导出明确排除 `bangumi-account.json`。
- 旧版整库 WebDAV 上传会使用安全数据库副本，排除收藏缓存和阶段 C 才允许同步的外部资料；手动下载后恢复本机私有表。
- 设置中心已增加“账号与数据源”，使用 `PasswordBox` 输入 Token，并显示头像、昵称、用户名和验证时间。
- 已新增 Bangumi 收藏状态缓存表；游戏详情页支持读取、刷新和修改收藏状态。
- 收藏创建使用 `POST`，已有收藏修改使用 `PATCH`；保存失败时会恢复旧状态。
- Token 被撤销或授权失败时保留本地账号资料并标记为“需要重新连接”，收藏控件会隐藏，本地游戏资料不受影响。
- 未登录、授权失效或未关联游戏时隐藏收藏控件。
- Bangumi 当前公开 API 未提供删除条目收藏接口，因此 Firefly 不发送未文档化的“取消收藏”请求。

阶段 C 基础同步已实现：

- 已新增独立的 `ExternalGameMetadataCloudSnapshot` 云端快照模型，外部资料通过 `v2/games/{gameId}/external-metadata.json` 同步，不写入现有 `metadata.json`，保持旧客户端基础游戏资料兼容。
- `SqliteGameLibraryService` 已支持读取单个/全部外部资料快照，并按 `snapshotUpdatedAtUtc` 合并云端资料。
- 同一 provider + subject 时较新快照胜出，较旧云端资料不会覆盖本机；provider 或 subject 不一致时返回冲突结果，当前阶段只记录/保守跳过，完整冲突 UI 留到后续阶段。
- `WebDavGameSyncService` 已支持上传和下载 `external-metadata.json`；单游戏上传、启动云端拉取和全量同步都会处理外部资料快照。
- 旧版整库 WebDAV 上传仍使用安全数据库副本，继续排除 `game_external_metadata` 与 `bangumi_collection_states`；阶段 C 外部资料只走 V2 独立 JSON 文件，不同步 Token 和收藏缓存。

自动化覆盖：

- 测试入口目前共登记 188 项测试。
- 已新增 SQLite 外部资料、字段级导入、搜索取消和缓存、刷新差异确认、简介折叠、收藏缓存、Bangumi API 状态翻译与重试、Token 加密与失效状态、图片真实解码、数据导出排除、WebDAV 收藏缓存隔离、阶段 C 外部资料快照读写/上传/下载/启动拉取/全量同步、Bangumi 精确标题优先、标题符号忽略排序、旧搜索接口回退和 UI 接入测试。
- 2026-06-17 已完成 Bangumi 搜索修复回归：180 项测试全部通过，构建 0 警告、0 错误；真实请求验证 `search/subject/冬日狂想曲` 第一条返回 `427028 / あまえんぼ冬 / 冬日狂想曲`，`search/subject/千恋万花` 第一条返回 `172612 / 千恋＊万花`。
- 2026-06-17 已完成阶段 C 基础同步回归：188 项测试全部通过。

本轮未包含：

- 阶段 C 后续：外部资料冲突可视化解决 UI。

2026-06-17 阶段 2/3 已完成：

- 外部资料冲突已从“保守跳过”推进为可持久化、可视化解决：`external_metadata_conflicts` 保存本机/云端快照和 compact reason，详情页提供保留本机、采用云端、解除关联三个动作。
- WebDAV 旧整库上传/下载流程已把 `external_metadata_conflicts` 作为本机私有表处理，避免本机冲突状态扩散到云端数据库副本。
- 管理页已支持批量匹配未关联游戏：先搜索并显示待确认候选，用户勾选后才写入 `ExternalGameMetadata`。
- Bangumi 收藏 UI 已支持评分和短评，保存时与收藏类型一起提交到 API 并写入本地缓存。
- 搜索结果模型新增 `AuxiliaryInfo`，Bangumi 搜索排序继续支持标题符号忽略和多关键词归一化。
- 当前测试入口登记 197 项，完整 `GameManager.App.Tests` 已通过。
- 阶段 D：OAuth 登录和更多资料提供方。

---

## 22. 参考资料

- Bangumi API 文档：<https://bangumi.github.io/api/>
- Bangumi OAuth 说明：<https://github.com/bangumi/api/blob/master/docs-raw/How-to-Auth.md>
- Bangumi User-Agent 说明：<https://github.com/bangumi/api/blob/master/docs-raw/user%20agent.md>
- PotatoVN 项目：<https://github.com/GoldenPotato137/PotatoVN>
- Firefly 原开发方案：`D:\Study\Projects\FireflyGameManager\开发方案.md`
- Firefly WebDAV V2 设计：`D:\Study\Projects\FireflyGameManager\docs\superpowers\specs\2026-06-07-compatible-cloud-save-v2-design.md`
