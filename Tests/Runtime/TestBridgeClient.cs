using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 默认使用 "default" 端点的测试客户端。
    /// 注意：这里没有显式指定 endpointKey 的请求，都必须落到 "default" 端点，
    /// 而不是落到配置的环境键（"test"）上——这是端点键解析的回归保护点。
    /// </summary>
    internal class TestBridgeClient : BridgeClient<TestBridgeClient>
    {
        /// <summary>使用内存配置提供器。</summary>
        protected override IBridgeConfigProvider ConfigProvider => new TestBridgeConfigProvider();

        /// <summary>默认端点键。</summary>
        protected override string urlKey => "default";
    }
}
