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
- `Runtime/Scripts/Response.cs` — 响应数据容器，包含 Data、StatusCode、StatusText、Headers、RawText 与 IsSuccessStatusCode。
- `Runtime/Scripts/Config/BridgeConfigAsset.cs` — ScriptableObject 形式的 Bridge 配置资源，负责环境与端点地址管理。
- `Runtime/Scripts/Config/BridgeConfig.cs` — 纯 C#（JSON）形式的配置实现，行为与 BridgeConfigAsset 一致。
- `Runtime/Scripts/Config/EndpointConfig.cs` — 单条端点配置（环境键 + 端点键 + 地址）。
- `Runtime/Scripts/Config/JsonBridgeConfigProvider.cs` — 从 StreamingAssets 读取 JSON 配置的提供器。
- `Runtime/Scripts/Config/BridgeConfigProviderFactory.cs` — 按类型名反射创建配置提供器。
- `Runtime/Scripts/BridgeHub.cs` — 第三方包调用 Bridge 的统一静态门面（见下文「作为依赖库使用 Bridge」）。
- `Runtime/Scripts/IBridgeClient.cs` — 非泛型客户端契约；包与测试面向它编程，而不是某个具体客户端类型。
- `Runtime/Scripts/BridgeNotInitializedException.cs` — 尚未登记客户端时的明确异常（带处理指引）。

### Editor
- `Editor/Scripts/BridgeSettingsProvider.cs` — Bridge 的 Project Settings 面板实现（只负责界面构建与交互）。
- `Editor/Scripts/BridgeConfigEditorAccess.cs` — 编辑器统一数据源入口：决定"当前编辑哪一份配置"并提供匹配的保存逻辑。
- `Editor/Scripts/BridgeSettings.cs` — 编辑器配置，保存 BridgeConfigAsset 引用与配置提供器选择到 ProjectSettings。
- `Editor/Scripts/BridgeEnvironmentMenu.cs` — 编辑器菜单。
- `Editor/Scripts/BridgeEnvironmentQuickSwitchWindow.cs` — 环境快速切换窗口。
- `Editor/Styles/` — UXML 布局与 USS 样式。

### Samples
- `Samples~/Basic/` — 示例项目，包含完整的 WebClient 实现和多端点测试用例。
- `Samples~/BridgeHub/` — 演示「第三方包依赖 Bridge」的用法：包只写静态封装（走 `BridgeHub`），
  应用侧提供客户端并自动登记；含示例配置、演示入口与说明文档。
- 在 Package Manager 中点击 Import 即可导入到 `Assets/Samples/` 目录。

### Tests
- `Tests/Runtime/` — PlayMode 测试，覆盖：完整 URL / 默认端点（urlKey 解析）/ 自定义端点 / 响应拦截器回写 /
  BridgeHub（客户端自动登记、先到先得、显式覆盖、未初始化异常、请求确实经由登记的客户端发出）。
- 超时类用例只断言可判定的不变量（成功必须越过 60 秒才说明没有硬限制；无法判定时标 Inconclusive），
  不把断言绑死在服务端 drip 的精确时长上。
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

## 作为依赖库使用 Bridge（第三方包）

如果某个 Unity 包依赖 Bridge 来调用 Web API，请遵守一条规则：

> **包内不要声明 `BridgeClient<T>` 子类，也不要 `new` 宿主的客户端。**
> 每个应用只应有一个（或少数几个）自己的客户端；包再声明一个会导致配置被重复加载、
> 环境切换不一致，并且和宿主的客户端冲突。

包只需要写静态封装函数，统一通过 `BridgeHub` 调用：

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using VoyageForge.Bridge.Runtime;

public static class OrderApi
{
    private const string EndpointKey = "orders";   // 包自己约定的端点键

    public static UniTask<Response<OrderDto>> GetOrderAsync(int id, CancellationToken ct = default)
        => BridgeHub.GetAsync<OrderDto>($"api/orders/{id}", EndpointKey, cancellationToken: ct);
}
```

应用侧（使用该包的工程）需要做的事：

- 实现至少一个 `BridgeClient<T>` 子类（例如自己的 `WebClient`），或直接导入 `Samples~/Basic` 示例；
- 想直接看可运行的完整示例，导入 `Samples~/BridgeHub`：包含「包侧静态封装」与「应用侧客户端」两侧代码、
  自带示例配置与演示入口；
- 该客户端**首次被使用**时会自动登记为 `BridgeHub` 的默认客户端，包的调用即可生效；
- 应用若有多个客户端、需要指定包用哪一个，显式指定一次即可（不指定则「先被实例化的客户端」胜出）：

```csharp
BridgeHub.SetDefaultClient(AdminClient.Instance);
```

尚未登记时，包的调用会**同步抛出** `BridgeNotInitializedException`，提示信息直接说明应用侧需要做什么，
不会静默失败或抛 NullReferenceException。

补充说明：

- 包的请求最终由宿主客户端发出，因此宿主的 `DefaultHeaders` 与请求/响应拦截器（例如统一加鉴权头）
  会自动作用于包的请求。
- 包要自定义请求头时按调用传 `headers` 即可，不必也不应该去改宿主的拦截器链。
- `Samples~/Basic` 中的 `BridgeBootstrap.cs` 会在运行时启动阶段触碰一次示例客户端完成登记，
  因此「导入示例即可开箱使用」。
- 单元测试里可用 `BridgeHub.ClearDefaultClient()` 复位全局状态。

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

### Response&lt;T&gt;

| 成员 | 说明 |
|------|------|
| `Data` | 响应体：DTO 的反序列化结果；`R` 为 `string` 时即原始文本。失败 / 空响应体 / 反序列化失败时为 null |
| `StatusCode` | HTTP 状态码（`HttpStatusCode` 枚举）；网络层失败（超时 / 取消 / 连接错误）为 0 |
| `StatusText` | 状态描述：正常结束时来自 `UnityWebRequest.result`；抛异常时为异常类型名；取消 / 超时为 "Canceled" / "Timeout" |
| `Headers` | 响应头；当前实现成功路径下是空字典（尚未从 UnityWebRequest 回填真实响应头） |
| `RawText` | 未反序列化的原始响应文本，仅成功路径有值；被响应拦截器改写时同步刷新 |
| `IsSuccessStatusCode` | 状态码是否落在 2xx 区间（网络层失败时为 false） |

> 注意：自 v0.0.16 起这些成员由**公共字段**改为**属性**（PascalCase）。
> 既有代码里的 `response.data` / `statusCode` / `statusText` / `headers` 需改为
> `response.Data` / `StatusCode` / `StatusText` / `Headers`。

### BridgeHub（第三方包调用入口）

静态门面，供依赖 Bridge 的包使用。请求方法与 `BridgeClient<T>` 的同名静态方法签名一致
（`GetAsync` / `PostAsync` / `PutAsync` / `DeleteAsync` 各带「不带 endpointKey」与「带 endpointKey」两种重载），
统一返回 `UniTask<Response<R>>`。

| 成员 | 说明 |
|------|------|
| `Current` / `HasClient` | 当前登记的客户端 / 是否已登记 |
| `TrySetDefaultClient(client)` | 仅在没有默认客户端时登记（客户端首次实例化会自动调用，先到先得） |
| `SetDefaultClient(client)` | 显式指定或替换默认客户端（多客户端场景）；传 null 抛 `ArgumentNullException` |
| `ClearDefaultClient()` | 清空默认客户端（测试复位用） |
| `SendAsync<R>(Request)` | 核心发送入口 |
| `GetAsync` / `PostAsync` / `PutAsync` / `DeleteAsync` | 便捷方法，签名与 `BridgeClient<T>` 的同名静态方法一致 |

未登记客户端时**同步抛出** `BridgeNotInitializedException`。用法与前提见上文
「作为依赖库使用 Bridge（第三方包）」。

### 拦截器

请求拦截器在「URL 拼接、请求头合并」之后、发送之前执行，可修改 URL、方法、请求体、请求头。

响应拦截器只在 HTTP 请求成功返回后执行，操作的是**原始文本形式**的响应（`Response<string>`）：
修改 `StatusCode`、`StatusText`、`Headers` 会直接体现在返回的响应上；
修改 `Data`（响应文本）时会按新文本重新反序列化，并同步更新 `RawText`。

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
    if (!string.IsNullOrEmpty(response.Data))
    {
        var envelope = JObject.Parse(response.Data);
        if (envelope["data"] != null)
            response.Data = envelope["data"].ToString(Formatting.None);
    }

    response.Headers["X-Intercepted"] = "1";
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
- 第三方包不声明 `BridgeClient<T>` 子类，统一通过静态门面 `BridgeHub` 调用；客户端首次实例化时自动登记，
  「谁是宿主的客户端」由应用侧决定，包只依赖 `IBridgeClient` 这个非泛型契约。
- 环境与端点的增删改统一走 `IBridgeConfig` 的编辑方法（`AddEnvironment` / `RemoveEnvironment` /
  `AddEndpoint` / `SetEndpoint` / `RemoveEndpoint`），由配置实现类统一保证去空白、去重、`dev` 保留并置顶等规则。
- 如果项目后续扩展新的服务类型，优先新增端点键，而不是继续堆叠专用字段。

## 维护建议
- 如果需要增加新的配置来源，实现 `IBridgeConfigProvider` 即可接入。
- 建议项目中只保留一份主配置资源，避免搜索到多份配置时产生歧义。
- 编辑器侧的所有入口（Project Settings 面板、菜单、环境快捷切换窗口）统一通过 `BridgeConfigEditorAccess`
  读写配置，保证「改的就是运行时读的那一份」；新增编辑器入口时请沿用该入口，不要直接操作配置列表。
- 新增第三方包时，包内**不要声明 `BridgeClient<T>` 子类**，统一用 `BridgeHub` 调用
  （见「作为依赖库使用 Bridge（第三方包）」）。
- 通过 `BridgeClient<T>` 泛型单例模式，不同服务可继承各自独立的客户端实例；
  拦截器链与 `DefaultHeaders` 按泛型类型各自独立，互不影响。