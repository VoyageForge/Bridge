using System.Linq;
using UnityEngine;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Sample.Hub
{
    /// <summary>
    /// 示例用的配置提供器：从 Resources 中读取本示例专用的 BridgeConfigAsset。
    /// 真实的第三方包一般不需要自己写提供器——配置来源属于应用侧的职责，
    /// 这里只是为了示例能独立运行（自带一份配置资源）。
    /// </summary>
    public sealed class DemoConfigProvider : IBridgeConfigProvider
    {
        /// <summary>
        /// 本示例专用的配置资源路径（不含扩展名）。
        /// 刻意与 Basic 示例的 "VoyageForge/Config/BridgeConfig" 区分开，
        /// 这样两个示例同时导入时不会互相干扰。
        /// </summary>
        public const string ResourcesPath = "VoyageForge/Config/BridgeHubDemoConfig";

        /// <summary>缓存已加载的配置，避免每次访问都走一遍 Resources 查找。</summary>
        private IBridgeConfig _cachedConfig;

        /// <summary>
        /// 从 Resources 加载配置资源。
        /// </summary>
        /// <returns>配置实例；找不到资源时返回 null 并给出错误日志。</returns>
        public IBridgeConfig LoadConfig()
        {
            if (_cachedConfig != null) return _cachedConfig;

            var assets = Resources.LoadAll<BridgeConfigAsset>(ResourcesPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"[BridgeHubDemo] 未在 Resources 中找到配置资源：{ResourcesPath}");
                return null;
            }

            _cachedConfig = assets.First();
            return _cachedConfig;
        }

        /// <summary>
        /// 保存配置（示例里只标记脏，正式工程可以换成文件写入、远端下发等实现）。
        /// </summary>
        /// <param name="config">要保存的配置。</param>
        public void SaveConfig(IBridgeConfig config)
        {
#if UNITY_EDITOR
            if (config is BridgeConfigAsset asset)
            {
                UnityEditor.EditorUtility.SetDirty(asset);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif
        }

        /// <summary>
        /// 返回当前环境键；配置缺失时回退到保底环境 dev。
        /// </summary>
        /// <param name="key">未使用，保留参数以兼容接口。</param>
        public string GetEnvironment(string key = null)
        {
            var config = LoadConfig();
            return config != null ? config.EnvironmentKey : BridgeConfigAsset.ReservedEnvironmentKey;
        }
    }
}
