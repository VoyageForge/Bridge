using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 专门用于验证"客户端首次实例化时自动登记到 BridgeHub"的客户端类型。
    /// 之所以单独定义一个（而不复用 TestBridgeClient）：
    /// 自动登记只在单例**创建**时触发一次，如果一个类型已经被其它用例实例化过，
    /// 就再也观察不到这次登记了；专用类型可以精确控制"首次创建"的时机。
    /// </summary>
    internal class TestAutoRegisterBridgeClient : BridgeClient<TestAutoRegisterBridgeClient>
    {
        /// <summary>使用内存配置提供器。</summary>
        protected override IBridgeConfigProvider ConfigProvider => new TestBridgeConfigProvider();

        /// <summary>默认端点键。</summary>
        protected override string urlKey => "default";
    }
}
