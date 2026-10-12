using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Sample.Hub
{
    /// <summary>
    /// 应用侧的客户端（宿主自己的客户端）。
    /// 注意这是**应用侧**的代码：每个应用只应有一个（或少数几个）这样的客户端，
    /// 依赖 Bridge 的第三方包不应该在包里再声明一个。
    ///
    /// 该实例首次被使用时（读取 <c>Instance</c>）会自动登记到 <see cref="BridgeHub"/>，
    /// 之后第三方包就能通过 BridgeHub 发请求了。
    /// </summary>
    public sealed class DemoWebClient : BridgeClient<DemoWebClient>
    {
        /// <summary>配置来源：本示例专用的 Resources 配置资源。</summary>
        private readonly DemoConfigProvider _configProvider = new();

        /// <summary>配置提供器。</summary>
        protected override IBridgeConfigProvider ConfigProvider => _configProvider;

        /// <summary>默认端点键：请求未指定 endpointKey 时使用它。</summary>
        protected override string urlKey => "default";
    }
}
