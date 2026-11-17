using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// 从 StreamingAssets 中加载 JSON 格式网络配置的提供器。
    ///
    /// 平台差异说明（为什么统一走 UnityWebRequest 而不是 System.IO）：
    /// - PC / 编辑器：StreamingAssets 是磁盘上的普通目录，File.ReadAllText 可用；
    /// - Android：StreamingAssets 被打进 APK，路径形如 jar:file:///.../base.apk!/assets，
    ///   System.IO 完全访问不到（File.Exists 恒为 false，File.ReadAllText 直接抛异常）；
    /// - WebGL：StreamingAssets 通过 HTTP 提供，路径形如 http://host/StreamingAssets，
    ///   System.IO 同样不可用。
    /// 因此读取统一走 UnityWebRequest：PC 上把本地路径转成 file:// URI，
    /// 三端共用同一条读取路径，避免出现“编辑器能跑、真机白屏”的问题。
    ///
    /// 在编辑器模式下，如果配置文件不存在会自动创建默认配置。
    /// </summary>
    public class JsonBridgeConfigProvider : IBridgeConfigProvider
    {
        /// <summary>
        /// StreamingAssets 下的默认配置相对路径（不含 StreamingAssets 前缀）。
        /// 注意：这里只保存相对路径，绝对路径在运行时根据 Application.streamingAssetsPath 拼出来。
        /// 如果把绝对路径写死进字段，Android 的 "jar:file://" 和 WebGL 的 "http://" 前缀会被
        /// Path.Combine 拼坏（Path.Combine 只认识 Windows 盘符风格的绝对路径）。
        /// </summary>
        public const string DefaultRelativePath = "VoyageForge/Config/BridgeConfig.json";

        /// <summary>实际使用的相对路径，构造时确定后不再变化。</summary>
        private readonly string _relativePath;

        /// <summary>已加载的配置缓存；非 null 时直接返回，避免重复 IO 与反序列化。</summary>
        private IBridgeConfig _cachedConfig;

        /// <summary>
        /// 无参构造，使用默认路径。
        /// </summary>
        public JsonBridgeConfigProvider() : this(null)
        {
        }

        /// <summary>
        /// 创建 JSON 配置提供器。
        /// </summary>
        /// <param name="relativePath">
        /// StreamingAssets 下的 JSON 文件相对路径。
        /// 例如 "VoyageForge/Config/BridgeConfig.json" 对应
        /// StreamingAssets/VoyageForge/Config/BridgeConfig.json。
        /// 留空或为 null 时使用 <see cref="DefaultRelativePath"/>。
        /// </param>
        public JsonBridgeConfigProvider(string relativePath)
        {
            _relativePath = string.IsNullOrWhiteSpace(relativePath)
                ? DefaultRelativePath
                : relativePath;
        }

        /// <summary>
        /// 从 StreamingAssets 加载 JSON 配置文件并反序列化为 BridgeConfig。
        /// 首次加载后缓存结果，后续调用返回同一实例。
        /// 在编辑器模式下，文件不存在时会自动创建默认配置并直接返回，省掉一次读取。
        /// </summary>
        /// <returns>网络配置实例；文件缺失、请求失败或解析失败时返回 null。</returns>
        public async UniTask<IBridgeConfig> LoadConfig()
        {
            // 1. 命中缓存直接返回，避免重复读取文件。
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            // 2. 编辑器模式：文件不存在时先落一份默认配置。
            //    这一步只能用 System.IO，因为编辑器跑在真实磁盘上，且需要「创建」文件；
            //    创建完成后直接返回，不再走下面的网络读取。
            //    非编辑器平台不会进入这个分支，所以 Android/WebGL 上不存在 File 调用。
#if UNITY_EDITOR
            string localPath = Path.Combine(Application.streamingAssetsPath, _relativePath);
            if (!File.Exists(localPath))
            {
                EnsureDirectoryExists(localPath);
                var defaultConfig = BridgeConfig.CreateDefault();
                File.WriteAllText(localPath, defaultConfig.ToJson());
                Debug.Log($"[Bridge] 已在 StreamingAssets 中创建默认 JSON 配置文件: {_relativePath}");

                _cachedConfig = defaultConfig;
                return _cachedConfig;
            }
#endif

            // 3. 统一用 UnityWebRequest 读取：PC(file://)、Android(jar:file://)、WebGL(http://)
            //    三端共用这条路径，不再区分平台。
            string url = GetStreamingAssetsUrl(_relativePath);

            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            // 4. 请求失败（文件不存在、路径非法、网络错误等）统一记日志并返回 null。
            //    注意：这里刻意不用 File.Exists 做前置判断——它在 Android/WebGL 上不可靠。
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[Bridge] 读取 JSON 配置文件失败: {url}\n错误: {request.error}");
                return null;
            }

            // 5. 从 downloadHandler 取内容，这是唯一跨平台的读取方式。
            //    不能再用 File.ReadAllText —— 那正是 Android/WebGL 读不到配置的原因。
            string json = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError($"[Bridge] JSON 配置文件为空: {url}");
                return null;
            }

            // 6. 反序列化并缓存结果。
            //    FromJson 对非法 JSON 会抛异常（ArgumentException / InvalidOperationException /
            //    JsonException），这里统一兜住并返回 null，与「解析失败返回 null」的约定保持一致，
            //    避免一份坏配置直接把调用方（启动流程）中断。
            BridgeConfig config;
            try
            {
                config = BridgeConfig.FromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bridge] JSON 配置解析失败: {url}\n错误: {e.Message}");
                return null;
            }

            _cachedConfig = config;
            return _cachedConfig;
        }

        /// <summary>
        /// 获取当前环境键。
        /// </summary>
        /// <param name="key">未使用，保留参数以兼容接口。</param>
        /// <returns>当前环境键；配置缺失时返回保底环境 dev。</returns>
        public async UniTask<string> GetEnvironment(string key = null)
        {
            var config = await LoadConfig();
            if (config == null)
            {
                Debug.LogWarning("[Bridge] JSON 配置不可用，返回保底环境 dev。");
                return BridgeConfig.ReservedEnvironmentKey;
            }

            return config.EnvironmentKey;
        }

        /// <summary>
        /// 保存配置到 StreamingAssets JSON 文件。
        ///
        /// 平台限制：StreamingAssets 在构建后是只读的。
        /// - Android：内容被打包在 APK 内，运行时无法写入；
        /// - WebGL：StreamingAssets 是 HTTP 资源，同样不可写。
        /// 所以运行时的保存需求应改为写入 Application.persistentDataPath（当前未实现），
        /// 这里只在编辑器下真正落盘，其它平台给出明确错误提示而不是静默失败。
        /// </summary>
        public void SaveConfig(IBridgeConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[Bridge] 无法保存空的配置。");
                return;
            }

            var bridgeConfig = config as BridgeConfig;
            if (bridgeConfig == null)
            {
                Debug.LogError("[Bridge] JsonBridgeConfigProvider 仅支持保存 BridgeConfig 类型。");
                return;
            }

#if UNITY_EDITOR
            string localPath = Path.Combine(Application.streamingAssetsPath, _relativePath);
            EnsureDirectoryExists(localPath);
            File.WriteAllText(localPath, bridgeConfig.ToJson());

            // 保存后清空缓存，保证下次 LoadConfig 读到最新内容。
            _cachedConfig = null;
#else
            Debug.LogError(
                "[Bridge] 运行时无法写入 StreamingAssets（Android/WebGL 均不可写）。" +
                "保存配置请改用 Application.persistentDataPath，或仅在编辑器中操作。");
#endif
        }

        /// <summary>
        /// 清除缓存的配置，下次调用 LoadConfig 时重新从文件加载。
        /// </summary>
        public void ClearCache()
        {
            _cachedConfig = null;
        }

        /// <summary>
        /// 把 StreamingAssets 下的相对路径拼成 UnityWebRequest 可用的 URL。
        ///
        /// 三个平台 <see cref="Application.streamingAssetsPath"/> 的形态不同：
        /// - PC / 编辑器: "C:/.../StreamingAssets"           → 需要转成 file:///C:/...
        /// - Android:     "jar:file:///.../base.apk!/assets"  → 已带协议，直接拼
        /// - WebGL:       "http://host/StreamingAssets"       → 已带协议，直接拼
        /// </summary>
        /// <param name="relativePath">StreamingAssets 下的相对路径。</param>
        /// <returns>可直接交给 UnityWebRequest.Get 的 URL。</returns>
        private static string GetStreamingAssetsUrl(string relativePath)
        {
            // 统一使用正斜杠：UnityWebRequest 不接受 Windows 风格的反斜杠。
            string basePath = Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/');
            string url = basePath + "/" + relativePath.Replace('\\', '/').TrimStart('/');

            // 没有 "://" 说明是本地文件系统路径（PC/编辑器），转成 file:// URI。
            // Android 的 "jar:file://" 和 WebGL 的 "http://" 都包含 "://"，会跳过这一步。
            if (!url.Contains("://"))
            {
                url = new Uri(url).AbsoluteUri;
            }

            return url;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 确保配置文件所在目录存在（仅编辑器需要，因为只有编辑器会创建文件）。
        /// </summary>
        /// <param name="filePath">配置文件的本地绝对路径。</param>
        private static void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
#endif
    }
}
