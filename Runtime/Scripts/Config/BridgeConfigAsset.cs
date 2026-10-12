using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// Bridge 网络配置资源。
    /// 使用字符串环境键管理多环境、多端点地址。
    /// </summary>
    [CreateAssetMenu(fileName = "BridgeConfig", menuName = "VoyageForge/Bridge/Config")]
    public class BridgeConfigAsset : ScriptableObject, IBridgeConfig
    {
        /// <summary>
        /// 默认保底环境键。
        /// dev 环境始终保留，避免所有环境被误删后无法快速恢复。
        /// </summary>
        public const string ReservedEnvironmentKey = "dev";

        /// <summary>
        /// 旧版保底环境键，用于把历史配置迁移到 dev。
        /// </summary>
        private const string LegacyReservedEnvironmentKey = "测试";

        /// <summary>
        /// 新建配置时自动创建的默认环境列表。
        /// </summary>
        private static readonly string[] DefaultEnvironmentKeys = { ReservedEnvironmentKey };

        [SerializeField] private string environmentKey;
        [SerializeField] private List<string> environmentKeys = new();
        [SerializeField] private List<EndpointConfig> endpointEntries = new();
        [SerializeField] private bool defaultsInitialized;

        /// <summary>
        /// 获取或设置当前启用的环境键。
        /// 设置时会自动补入环境列表。
        /// </summary>
        public string EnvironmentKey
        {
            get
            {
                EnsureConfigData();
                return environmentKey;
            }
            set
            {
                EnsureConfigData();
                environmentKey = NormalizeKey(value);
                AddEnvironmentIfMissing(environmentKey);
            }
        }

        /// <summary>
        /// 获取全部环境键列表。
        /// </summary>
        public IReadOnlyList<string> EnvironmentKeys
        {
            get
            {
                EnsureConfigData();
                return environmentKeys;
            }
        }

        /// <summary>
        /// 获取全部端点配置。
        /// </summary>
        public IReadOnlyList<EndpointConfig> EndpointEntries
        {
            get
            {
                EnsureConfigData();
                return endpointEntries;
            }
        }

        /// <summary>
        /// 添加环境。
        /// </summary>
        public bool AddEnvironment(string key)
        {
            EnsureConfigData();
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(normalizedKey) || ContainsEnvironment(normalizedKey))
            {
                return false;
            }

            environmentKeys.Add(normalizedKey);
            EnsureReservedEnvironmentFirst();
            if (string.IsNullOrWhiteSpace(environmentKey))
            {
                environmentKey = normalizedKey;
            }

            return true;
        }

        /// <summary>
        /// 删除环境，并同步删除该环境下的全部端点。
        /// </summary>
        public bool RemoveEnvironment(string key)
        {
            EnsureConfigData();
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return false;
            }

            if (string.Equals(normalizedKey, ReservedEnvironmentKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool removed = false;
            for (int index = environmentKeys.Count - 1; index >= 0; index--)
            {
                if (string.Equals(environmentKeys[index], normalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    environmentKeys.RemoveAt(index);
                    removed = true;
                }
            }

            for (int index = endpointEntries.Count - 1; index >= 0; index--)
            {
                var entry = endpointEntries[index];
                if (entry != null &&
                    string.Equals(entry.EnvironmentKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    endpointEntries.RemoveAt(index);
                }
            }

            if (string.Equals(environmentKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                environmentKey = ReservedEnvironmentKey;
            }

            EnsureReservedEnvironmentFirst();
            return removed;
        }

        /// <summary>
        /// 在指定环境下添加一条默认端点配置。
        /// </summary>
        public void AddEndpoint(string key)
        {
            EnsureConfigData();
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return;
            }

            AddEnvironmentIfMissing(normalizedKey);
            endpointEntries.Add(new EndpointConfig
            {
                EnvironmentKey = normalizedKey,
                EndpointKey = "default",
                Url = string.Empty
            });
        }

        // ============================================================
        // 编辑方法（编辑器通过 IBridgeConfig 接口调用）
        // ============================================================

        /// <summary>
        /// 删除指定环境下的第 localIndex 条端点。
        /// </summary>
        /// <param name="environmentKey">所属环境键，按不区分大小写匹配。</param>
        /// <param name="localIndex">该环境内的端点序号（从 0 开始）。</param>
        public void RemoveEndpoint(string environmentKey, int localIndex)
        {
            EnsureConfigData();

            string normalizedEnvironmentKey = NormalizeKey(environmentKey);
            int n = 0;
            for (int i = 0; i < endpointEntries.Count; i++)
            {
                var e = endpointEntries[i];
                if (e != null && string.Equals(e.EnvironmentKey, normalizedEnvironmentKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (n == localIndex) { endpointEntries.RemoveAt(i); return; }
                    n++;
                }
            }
        }

        /// <summary>
        /// 修改指定环境下第 <paramref name="localIndex"/> 条端点的键与地址。
        /// </summary>
        /// <param name="environmentKey">所属环境键，按不区分大小写匹配。</param>
        /// <param name="localIndex">该环境内的端点序号（从 0 开始）。</param>
        /// <param name="endpointKey">新的端点键；为空时回退为 "default"。</param>
        /// <param name="url">新的端点地址；会去掉首尾空白。</param>
        /// <returns>是否找到并更新了目标端点。</returns>
        public bool SetEndpoint(string environmentKey, int localIndex, string endpointKey, string url)
        {
            EnsureConfigData();

            string normalizedEnvironmentKey = NormalizeKey(environmentKey);
            if (string.IsNullOrWhiteSpace(normalizedEnvironmentKey)) return false;

            int localCounter = 0;
            foreach (var entry in endpointEntries)
            {
                if (entry == null ||
                    !string.Equals(entry.EnvironmentKey, normalizedEnvironmentKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (localCounter == localIndex)
                {
                    entry.EndpointKey = NormalizeEndpointKey(endpointKey);
                    entry.Url = NormalizeUrl(url);
                    return true;
                }

                localCounter++;
            }

            return false;
        }

        // ============================================================
        // 显式接口实现（IBridgeConfig.EnvironmentKeys / Endpoints 要求 List<>）
        // ============================================================

        List<string> IBridgeConfig.EnvironmentKeys
        {
            get { EnsureConfigData(); return environmentKeys; }
        }

        List<EndpointConfig> IBridgeConfig.Endpoints
        {
            get { EnsureConfigData(); return endpointEntries; }
        }

        /// <summary>
        /// 获取当前环境下指定端点的基础地址。
        /// </summary>
        public string GetBaseUrl(string endpointKey = "default")
        {
            EnsureConfigData();

            var entry = endpointEntries.FirstOrDefault(item =>
                item != null &&
                string.Equals(item.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.EndpointKey, endpointKey, StringComparison.OrdinalIgnoreCase));

            if (entry != null && !string.IsNullOrWhiteSpace(entry.Url))
            {
                return entry.Url;
            }

            throw new InvalidOperationException($"未找到环境“{environmentKey}”下端点“{endpointKey}”的地址配置。");
        }

        /// <summary>
        /// 构建完整请求地址。
        /// </summary>
        public string BuildFullUrl(string endpointKey, string path, Dictionary<string, string> query = null)
        {
            var baseUri = new Uri(GetBaseUrl(endpointKey));
            var fullUri = new Uri(baseUri, path.TrimStart('/'));
            string url = fullUri.ToString();

            if (query == null || query.Count == 0)
            {
                return url;
            }

            var queryParts = new List<string>();
            foreach (var item in query)
            {
                queryParts.Add($"{item.Key}={UnityWebRequest.EscapeURL(item.Value)}");
            }

            return $"{url}?{string.Join("&", queryParts)}";
        }

        public void SetEnvironment(string environmentKey)
        {
            EnvironmentKey =  environmentKey;
        }

        private void OnEnable()
        {
            EnsureConfigData();
        }

        private void OnValidate()
        {
            EnsureConfigData();
        }

        /// <summary>
        /// 初始化并清理配置数据。
        /// 新建资源时默认创建 dev 环境，
        /// 之后仅强制保留 dev 环境并让它始终位于第一项。
        /// </summary>
        private void EnsureConfigData()
        {
            environmentKeys ??= new List<string>();
            endpointEntries ??= new List<EndpointConfig>();
            bool useDefaultEnvironment = string.IsNullOrWhiteSpace(environmentKey) ||
                                         string.Equals(environmentKey, LegacyReservedEnvironmentKey,
                                             StringComparison.OrdinalIgnoreCase);

            if (!defaultsInitialized)
            {
                foreach (string defaultKey in DefaultEnvironmentKeys)
                {
                    if (!ContainsEnvironment(defaultKey))
                    {
                        environmentKeys.Add(defaultKey);
                    }
                }

                defaultsInitialized = true;
            }

            CleanupEnvironmentKeys();
            CleanupEndpointEntries();
            AddEnvironmentIfMissing(ReservedEnvironmentKey);
            EnsureReservedEnvironmentFirst();

            if (!string.IsNullOrWhiteSpace(environmentKey))
            {
                AddEnvironmentIfMissing(environmentKey);
                EnsureReservedEnvironmentFirst();
            }

            environmentKey = NormalizeKey(environmentKey);
            if (useDefaultEnvironment || string.IsNullOrWhiteSpace(environmentKey) ||
                !ContainsEnvironment(environmentKey))
            {
                environmentKey = ReservedEnvironmentKey;
            }
        }

        /// <summary>
        /// 清理环境列表中的空值与重复值。
        /// </summary>
        private void CleanupEnvironmentKeys()
        {
            var cleanedKeys = new List<string>();
            foreach (string key in environmentKeys)
            {
                string normalizedKey = NormalizeKey(key);
                if (string.IsNullOrWhiteSpace(normalizedKey))
                {
                    continue;
                }

                if (cleanedKeys.Any(item => string.Equals(item, normalizedKey, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                cleanedKeys.Add(normalizedKey);
            }

            environmentKeys = cleanedKeys;
        }

        /// <summary>
        /// 清理端点列表中的空值与无效值，并自动补齐环境。
        /// </summary>
        private void CleanupEndpointEntries()
        {
            for (int index = endpointEntries.Count - 1; index >= 0; index--)
            {
                var entry = endpointEntries[index];
                if (entry == null)
                {
                    endpointEntries.RemoveAt(index);
                    continue;
                }

                entry.EnvironmentKey = NormalizeKey(entry.EnvironmentKey);
                entry.EndpointKey = NormalizeEndpointKey(entry.EndpointKey);
                entry.Url = NormalizeUrl(entry.Url);

                if (string.IsNullOrWhiteSpace(entry.EnvironmentKey))
                {
                    endpointEntries.RemoveAt(index);
                    continue;
                }

                AddEnvironmentIfMissing(entry.EnvironmentKey);
            }
        }

        /// <summary>
        /// 在环境不存在时自动补入环境列表。
        /// </summary>
        private void AddEnvironmentIfMissing(string key)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return;
            }

            if (!ContainsEnvironment(normalizedKey))
            {
                environmentKeys.Add(normalizedKey);
            }
        }

        /// <summary>
        /// 确保 dev 保底环境始终位于环境列表第一项。
        /// </summary>
        private void EnsureReservedEnvironmentFirst()
        {
            int reservedIndex = environmentKeys.FindIndex(item =>
                string.Equals(item, ReservedEnvironmentKey, StringComparison.OrdinalIgnoreCase));

            if (reservedIndex < 0)
            {
                return;
            }

            environmentKeys.RemoveAt(reservedIndex);
            environmentKeys.Insert(0, ReservedEnvironmentKey);
        }

        /// <summary>
        /// 判断环境列表中是否已存在指定环境。
        /// </summary>
        private bool ContainsEnvironment(string key)
        {
            return environmentKeys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 规范化环境键，去除空白字符。
        /// </summary>
        private static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }

        /// <summary>
        /// 规范化端点键：null 或空白视为未设置，回退为 "default"。
        /// 端点键是同一环境内区分多条地址的唯一依据，不允许为空。
        /// </summary>
        private static string NormalizeEndpointKey(string endpointKey)
        {
            return string.IsNullOrWhiteSpace(endpointKey) ? "default" : endpointKey.Trim();
        }

        /// <summary>
        /// 规范化端点地址：null 视为空字符串，并去掉首尾空白。
        /// </summary>
        private static string NormalizeUrl(string url)
        {
            return url?.Trim() ?? string.Empty;
        }
    }
}