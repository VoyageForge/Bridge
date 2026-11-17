using Cysharp.Threading.Tasks;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 测试用配置提供器：直接返回内存中的 <see cref="TestBridgeConfig"/>，不读写任何文件。
    /// 所有方法都同步完成（UniTask.FromResult），因此测试里可以安全地同步等待。
    /// </summary>
    internal class TestBridgeConfigProvider : IBridgeConfigProvider
    {
        /// <summary>被共享的配置替身实例。</summary>
        private readonly TestBridgeConfig _config = new();

        /// <summary>返回内存配置（已完成的 UniTask，不涉及任何 IO）。</summary>
        public UniTask<IBridgeConfig> LoadConfig() => UniTask.FromResult<IBridgeConfig>(_config);

        /// <summary>测试替身不做持久化。</summary>
        public void SaveConfig(IBridgeConfig config) { }

        /// <summary>返回内存配置的当前环境键（已完成的 UniTask）。</summary>
        public UniTask<string> GetEnvironment(string key = null) => UniTask.FromResult(_config.EnvironmentKey);
    }
}
