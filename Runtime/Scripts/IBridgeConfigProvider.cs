using Cysharp.Threading.Tasks;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// Bridge 网络配置提供器接口。
    /// 负责加载、保存配置以及返回当前环境。
    /// 每个实现自行管理配置文件的路径和格式（Resources/ScriptableObject、StreamingAssets/JSON 等）。
    /// </summary>
    public interface IBridgeConfigProvider
    {
        /// <summary>
        /// 加载网络配置对象。
        /// </summary>
        /// <returns>网络配置实例。</returns>
        UniTask<IBridgeConfig> LoadConfig();

        /// <summary>
        /// 保存网络配置对象。
        /// </summary>
        /// <param name="config">网络配置实例。</param>
        void SaveConfig(IBridgeConfig config);

        /// <summary>
        /// 获取当前环境键。
        /// </summary>
        /// <returns>当前环境键。</returns>
        UniTask<string> GetEnvironment(string key = null);
    }
}
