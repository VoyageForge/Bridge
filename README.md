# Bridge

## 简介
Bridge 是 VoyageForge 的 Unity 通信连接模块，用来统一连接本地与远程服务、客户端与服务端之间的配置与调用入口。

它围绕"环境 + 端点"组织 API 地址，把不同环境下的 Web API、Socket 或其他远程入口收拢到同一套配置体系里，并通过编辑器面板提供可视化维护能力。

## 目录说明

### Runtime
- `Runtime/Scripts/IBridgeConfig.cs` — Bridge 配置访问接口：运行时查询地址，以及编辑器侧的环境/端点增删改。
- `Runtime/Scripts/IBridgeConfigProvider.cs` — Bridge 配置提供器接口，允许项目按需决定配置的加载方式。
- `Runtime/Scripts/BridgeClient.cs` — Bridge 核心运行时入口，抽象泛型单例基类，提供 **UniTask 异步（Wait）模式**、请求/响应拦截器链、多端点支持。
- `Runtime/Scripts/Request.cs` — 请求数据模型，支持 url、method、bodyJson、headers、timeoutSeconds、endpointKey、cancellationToken。
- `Runtime/Scripts/Response.cs` — 响应数据容器，包含 data、statusCode、statusText、headers、RawText。
- `Runtime/Scripts/Config/BridgeConfigAsset.cs` — ScriptableObject 形式的 Bridge 配置资源，负责环境与端点地址管理。
- `Runtime/Scripts/Config/BridgeConfig.cs` — 纯 C#（JSON）形式的配置实现，行为与 BridgeConfigAsset 一致。
- `Runtime/Scripts/Config/EndpointConfig.cs` — 单条端点配置（环境键 + 端点键 + 地址）。
- `Runtime/Scripts/Config/JsonBridgeConfigProvider.cs` — 从 StreamingAssets 读取 JSON 配置的提供器。
- `Runtime/Scripts/Config/BridgeConfigProviderFactory.cs` — 按类型名反射创建配置提供器。

### Editor
- `Editor/Scripts/BridgeSettingsProvider.cs` — Bridge 的 Project Settings 面板实现（只负责界面构建与交互）。
- `Editor/Scripts/BridgeConfigEditorAccess.cs` — 编辑器统一数据源入口：决定"当前编辑哪一份配置"并提供匹配的保存逻辑。
- `Editor/Scripts/BridgeSettings.cs` — 编辑器配置，保存 BridgeConfigAsset 引用与配置提供器选择到 ProjectSettings。
- `Editor/Scripts/BridgeEnvironmentMenu.cs` — 编辑器菜单。
- `Editor/Scripts/BridgeEnvironmentQuickSwitchWindow.cs` — 环境快速切换窗口。
- `Editor/Styles/` — UXML 布局与 USS 样式。

### Samples
- `Samples~/Basic/` — 示例项目，包含完整的 WebClient 实现和多端点测试用例。
- 在 Package Manager 中点击 Import 即可导入到 `Assets/Samples/` 目录。

### Tests
- `Tests/Runtime/` — PlayMode 测试，覆盖完整 URL、默认端点（urlKey 解析）、自定义端点、响应拦截器回写四种场景。
- 测试直接访问公网地址（jsonplaceholder.typicode.com / httpbin.org），离线环境下会失败。

## 配置规则

### 环境
- 新建配置时默认提供 `dev` 环境。
- 用户可以自由新增环境。
- `dev` 作为保底环境始终保留，不能删除，始终排在列表第一项。

### 端点
- 每个环境可以维护多条端点配置，通过键值对区分（如 `default`、`webapi`、`socket`）。
- 如果同一主机只是端口不同，推荐直接用不同端点键来区分。

## 快速开始

### 1. 创建 WebClient

继承 `BridgeClient<T>`，指定配置提供器和默认端点键：

```csharp
public class WebClient : BridgeClient<WebClient>
{
    protected override IBridgeConfigProvider ConfigProvider => 
        new ResourcesBridgeConfigProvider("VoyageForge/Config/BridgeConfig");
    protected override string urlKey => "default";
}
```

- `ConfigProvider` 决定配置从哪里来：`ResourcesBridgeConfigProvider` 读取 Resources 下的 `BridgeConfigAsset`（该实现位于 `Samples~/Basic`，可直接复用或自行实现 `IBridgeConfigProvider`）。
- `urlKey` 是**默认端点键**：请求未显式设置 `endpointKey` 时使用它，默认值为 `"default"`。
  Bridge 只会从「当前环境的端点列表」中按这个键取地址，不会用环境键去当端点键。

### 2. 发起请求

全部请求方法都返回 `UniTask<Response<R>>`，用 `await` 直接取结果：

```csharp
// 默认端点（使用子类的 urlKey）
var response = await WebClient.GetAsync<UserDto>("api/user");

// 自定义端点
var response = await WebClient.GetAsync<StatusDto>("api/status", "webapi");
```

### 3. 多端点支持

通过 `Request.endpointKey` 指定端点，或使用便捷方法重载：

```csharp
// 方式一：Request 对象
var request = new Request
{
    url = "api/status",
    method = "GET",
    endpointKey = "webapi"
};
var response = await WebClient.SendAsync<StatusDto>(request);

// 方式二：便捷方法重载
var response = await WebClient.GetAsync<StatusDto>("api/status", "webapi");
```

也可以直接使用完整 URL（以 `http` 开头则跳过端点拼接）：

```csharp
var response = await WebClient.GetAsync<PostDto>("https://jsonplaceholder.typicode.com/posts/1");
```

## API 参考

### 核心请求

| 方法 | 说明 |
|------|------|
| `SendAsync<R>(Request)` | 核心异步请求，自动解析端点、拼接 baseUrl、合并 headers、执行拦截器链 |

### 请求方法（返回 `UniTask<Response<R>>`）

| 方法 | 说明 |
|------|------|
| `GetAsync<R>(url, headers?, timeout?, ct?)` | GET 请求 |
| `GetAsync<R>(url, endpointKey, headers?, timeout?, ct?)` | GET 请求 + 指定端点 |
| `PostAsync<R>(url, bodyJson, headers?, timeout?, ct?)` | POST 请求 |
| `PostAsync<R>(url, bodyJson, endpointKey, headers?, timeout?, ct?)` | POST + 指定端点 |
| `PutAsync<R>(url, bodyJson, headers?, timeout?, ct?)` | PUT 请求 |
| `PutAsync<R>(url, bodyJson, endpointKey, headers?, timeout?, ct?)` | PUT + 指定端点 |
| `DeleteAsync<R>(url, headers?, timeout?, ct?)` | DELETE 请求 |
| `DeleteAsync<R>(url, endpointKey, headers?, timeout?, ct?)` | DELETE + 指定端点 |

### 拦截器

请求拦截器在「URL 拼接、请求头合并」之后、发送之前执行，可修改 URL、方法、请求体、请求头。

响应拦截器只在 HTTP 请求成功返回后执行，操作的是**原始文本形式**的响应（`Response<string>`）：
修改 `statusCode`、`statusText`、`headers` 会直接体现在返回的响应上；
修改 `data`（响应文本）时会按新文本重新反序列化，并同步更新 `RawText`。

```csharp
// 请求拦截器：修改请求参数
WebClient.UseRequestInterceptor(request =>
{
    request.headers["Authorization"] = "Bearer xxx";
    return request;
});

// 响应拦截器：剥离服务端 envelope（示例：{"code":0,"data":{...}}），修改会被回写到最终响应
WebClient.UseResponseInterceptor(response =>
{
    if (!string.IsNullOrEmpty(response.data))
    {
        var envelope = JObject.Parse(response.data);
        if (envelope["data"] != null)
            response.data = envelope["data"].ToString(Formatting.None);
    }

    response.headers["X-Intercepted"] = "1";
    return response;
});

// 移除拦截器
WebClient.RemoveRequestInterceptor(interceptor);
WebClient.RemoveResponseInterceptor(interceptor);
```

### 全局 Headers

```csharp
WebClient.DefaultHeaders["Authorization"] = "Bearer xxx";
WebClient.DefaultHeaders["X-App-Version"] = Application.version;
```

### 环境切换

```csharp
// 运行时切换环境
WebClient.Instance.SetEnvironmentKey("prod");

// 不传参数则回到保底环境 dev
WebClient.Instance.SetEnvironmentKey();

// 读取默认端点键 / 指定端点地址
var defaultEndpointKey = WebClient.UrlKey;
var baseUrl = WebClient.Instance.GetBaseUrl();          // 默认端点
var webApiBaseUrl = WebClient.Instance.GetBaseUrl("webapi");
```

### 请求配置

```csharp
var request = new Request
{
    url = "api/user",
    method = "GET",
    bodyJson = "{...}",
    headers = new Dictionary<string, string> { { "X-Custom", "value" } },
    timeoutSeconds = 30,
    endpointKey = "webapi",
    cancellationToken = token
};
```

## 设计说明
- 使用字符串环境键与端点键管理远程服务入口，避免把地址结构写死在代码里。
- 配置面板基于 UI Toolkit、UXML、USS 实现，适合在 Unity 编辑器内集中维护。
- 配置资源会优先全项目搜索；若不存在，则自动创建到 `Assets/Resources/VoyageForge/Config/BridgeConfig.asset`。
- 无论用户如何新增或删除环境，`dev` 都会被强制保留并保持在第一位。
- 相对路径自动拼接当前端点的 baseUrl；完整 URL（`http` 开头）直接请求，不拼接。
- 「默认端点键」只有一个判定入口：`BridgeClient<T>.urlKey`。请求未设置 `endpointKey`（null/空白）时使用它，
  绝不会把**环境键**当作端点键使用。
- 环境与端点的增删改统一走 `IBridgeConfig` 的编辑方法（`AddEnvironment` / `RemoveEnvironment` /
  `AddEndpoint` / `SetEndpoint` / `RemoveEndpoint`），由配置实现类统一保证去空白、去重、`dev` 保留并置顶等规则。
- 如果项目后续扩展新的服务类型，优先新增端点键，而不是继续堆叠专用字段。

## 维护建议
- 如果需要增加新的配置来源，实现 `IBridgeConfigProvider` 即可接入。
- 建议项目中只保留一份主配置资源，避免搜索到多份配置时产生歧义。
- 编辑器侧的所有入口（Project Settings 面板、菜单、环境快捷切换窗口）统一通过 `BridgeConfigEditorAccess`
  读写配置，保证「改的就是运行时读的那一份」；新增编辑器入口时请沿用该入口，不要直接操作配置列表。
- 通过 `BridgeClient<T>` 泛型单例模式，不同服务可继承各自独立的客户端实例；
  拦截器链与 `DefaultHeaders` 按泛型类型各自独立，互不影响。