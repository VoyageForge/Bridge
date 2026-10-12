using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 测试用配置提供器：直接返回内存中的 <see cref="TestBridgeConfig"/>，不读写任何文件。
    /// </summary>
    internal class TestBridgeConfigProvider : IBridgeConfigProvider
    {
        /// <summary>被共享的配置替身实例。</summary>
        private readonly TestBridgeConfig _config = new();

        /// <summary>返回内存配置。</summary>
        public IBridgeConfig LoadConfig() => _config;

        /// <summary>测试替身不做持久化。</summary>
        public void SaveConfig(IBridgeConfig config) { }

        /// <summary>返回内存配置的当前环境键。</summary>
        public string GetEnvironment(string key = null) => _config.EnvironmentKey;
    }
}
