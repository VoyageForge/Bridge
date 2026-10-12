# BridgeHub 示例（第三方包如何依赖 Bridge）

本示例演示「一个依赖 Bridge 的包」与「应用宿主」之间的分工，核心是：
**包只写静态封装函数，客户端由应用侧提供。**

## 文件说明

| 文件 | 视角 | 说明 |
|---|---|---|
| `Scripts/OrderApi.cs` | 第三方包 | 对外的静态 API 封装，全部通过 `BridgeHub` 调用；不声明任何 `BridgeClient<T>` 子类 |
| `Scripts/OrderDto.cs` | 第三方包 | 响应模型 |
| `Scripts/DemoWebClient.cs` | 应用侧 | 宿主自己的客户端；首次被使用时自动登记到 `BridgeHub` |
| `Scripts/DemoConfigProvider.cs` | 应用侧 | 配置来源（本示例从 Resources 读取自带配置） |
| `Scripts/BridgeHubDemo.cs` | 应用侧 | 演示入口（MonoBehaviour） |
| `Resources/VoyageForge/Config/BridgeHubDemoConfig.asset` | 应用侧 | 示例配置：环境 `dev`，端点 `default` 与 `orders` |

## 运行步骤

1. 在 Package Manager 中选中 Bridge → Samples → **BridgeHub** → Import；
2. 把 `BridgeHubDemo` 组件挂到场景里任意 GameObject 上；
3. 进入 Play 模式，查看 Console。

## 预期输出

```
[BridgeHubDemo] BridgeHub.HasClient=True, DefaultEndpointKey=default
[BridgeHubDemo] 请求成功: id=1, userId=1, title=sunt aut facere repellat provident occaecati excepturi optio reprehenderit
[BridgeHubDemo] RawText={"userId": 1, "id": 1, "title": "...", "body": "..."}
```

## 关键点

- **包不碰客户端**：`OrderApi` 里没有任何 `WebClient` / `DemoWebClient` 的引用，只有 `BridgeHub`；
  端点键 `"orders"` 由包自己约定，具体地址由应用侧的配置决定。
- **应用侧零负担**：只要自己的客户端被「使用过一次」，`BridgeHub` 就自动拿到它；
  多客户端场景可用 `BridgeHub.SetDefaultClient(...)` 显式指定包用哪一个。
- **错误可读**：如果应用侧还没登记客户端，包的调用会同步抛出 `BridgeNotInitializedException`，
  示例里演示了如何捕获并打印指引（而不是抛 NullReferenceException）。
- **宿主拦截器自动生效**：包发出的请求走的是宿主的客户端，
  因此宿主通过 `BridgeClient<T>.UseRequestInterceptor(...)` 加的鉴权头对包的请求同样有效。
- **测试友好**：单元测试里可用 `BridgeHub.ClearDefaultClient()` 复位全局状态。

## 真实包的写法

真实包通常把 `OrderApi` 这类封装放进自己的 asmdef，并在 asmdef 的 `references` 中加入
`VoyageForge.Bridge.Runtime`。本示例为了让导入后直接可编译，没有额外的 asmdef。

本示例的配置资源路径（`VoyageForge/Config/BridgeHubDemoConfig`）刻意与 `Basic` 示例
（`VoyageForge/Config/BridgeConfig`）区分开，两个示例同时导入也不会互相干扰。
