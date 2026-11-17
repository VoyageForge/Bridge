using Cysharp.Threading.Tasks;
using UnityEngine;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Sample
{
    /// <summary>
    /// 从 Resources 中加载网络配置的默认提供器。
    /// 该实现只是一个可选默认方案，使用方也可以自行实现 IBridgeConfigProvider。
    ///
    /// 平台说明：Resources 在 PC / Android / WebGL 上都能用，
    /// 但同步的 Resources.Load 会阻塞主线程；这里统一用 Resources.LoadAsync 分帧加载。
    /// </summary>
    public class ResourcesBridgeConfigProvider : IBridgeConfigProvider
    {
        /// <summary>
        /// Resources 下的默认配置路径（不含扩展名）。
        /// </summary>
        public const string DefaultResourcesPath = "VoyageForge/Config/BridgeConfig";

        /// <summary>
        /// 配置资源路径。
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// 无参构造，使用默认路径。
        /// </summary>
        public ResourcesBridgeConfigProvider() : this(DefaultResourcesPath) { }

        /// <summary>
        /// 创建 Resources 配置提供器。
        /// </summary>
        /// <param name="path">Resources 目录下的配置路径（不含扩展名）。</param>
        public ResourcesBridgeConfigProvider(string path)
        {
            _path = path;
        }


        /// <summary>
        /// 异步加载 Resources 中的第一份网络配置资源。
        /// 注意：Resources.LoadAsync&lt;T&gt; 返回的是非泛型的 ResourceRequest，
        /// UniTask 对它的扩展固定返回 UniTask&lt;UnityEngine.Object&gt;，
        /// 所以这里必须显式转成 BridgeConfigAsset，不能直接依赖泛型参数。
        /// </summary>
        /// <returns>网络配置实例；找不到资源时返回 null。</returns>
        public async UniTask<IBridgeConfig> LoadConfig()
        {
            var config = await Resources.LoadAsync<BridgeConfigAsset>(_path).ToUniTask() as BridgeConfigAsset;

            if (config == null)
            {
                Debug.LogError("未在 Resources 目录中搜索到 BridgeConfig 配置资源。");
                return null;
            }

            return config;
        }


        /// <summary>
        /// 保存配置到 Resources 中的 ScriptableObject 资源。
        /// </summary>
        /// <param name="config">网络配置实例。</param>
        public void SaveConfig(IBridgeConfig config)
        {
#if UNITY_EDITOR
            var asset = config as BridgeConfigAsset;
            if (asset == null)
            {
                Debug.LogError($"[Bridge] ResourcesBridgeConfigProvider 仅支持保存 BridgeConfigAsset 类型。");
                return;
            }

            UnityEditor.EditorUtility.SetDirty(asset);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        /// <summary>
        /// 获取当前环境键。
        /// 当配置缺失时返回保底环境 dev。
        /// </summary>
        /// <returns>当前环境键。</returns>
        public async UniTask<string> GetEnvironment(string key = null)
        {
            var config = await LoadConfig();
            if (config == null)
            {
                // 与运行时保持一致：配置缺失时回退到保底环境 dev，而不是抛异常中断调用链。
                return BridgeConfigAsset.ReservedEnvironmentKey;
            }

            return config.EnvironmentKey;
        }
    }
}
