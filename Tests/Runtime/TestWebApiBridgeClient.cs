using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 默认端点键被重写为 "webapi" 的测试客户端。
    /// 用于验证"未指定 endpointKey 时使用子类 urlKey"这一约定确实生效，
    /// 而不是被硬编码成 "default" 或被环境键覆盖。
    /// </summary>
    internal class TestWebApiBridgeClient : BridgeClient<TestWebApiBridgeClient>
    {
        /// <summary>使用内存配置提供器。</summary>
        protected override IBridgeConfigProvider ConfigProvider => new TestBridgeConfigProvider();

        /// <summary>默认端点键。</summary>
        protected override string urlKey => "webapi";
    }
}
