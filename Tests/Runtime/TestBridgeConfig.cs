using System;
using System.Collections.Generic;
using System.Linq;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 测试用配置替身。
    /// 关键点：<see cref="GetBaseUrl"/> 按生产实现（BridgeConfig / BridgeConfigAsset）的严格规则实现——
    /// 找不到匹配端点就抛异常，不做任何"回退到 default"的兜底。
    /// 这样一旦端点键解析逻辑回归（例如又用环境键当端点键），测试会直接失败，而不是被兜底掩盖。
    /// </summary>
    internal class TestBridgeConfig : IBridgeConfig
    {
        /// <summary>当前环境键。测试里所有端点都挂在 "test" 环境下。</summary>
        public string EnvironmentKey { get; set; } = "test";

        /// <summary>环境键列表。</summary>
        public List<string> EnvironmentKeys { get; } = new List<string> { "test" };

        /// <summary>端点列表：default 指向 jsonplaceholder，webapi 指向 httpbin。</summary>
        public List<EndpointConfig> Endpoints { get; } = new List<EndpointConfig>
        {
            new EndpointConfig { EnvironmentKey = "test", EndpointKey = "default", Url = "https://jsonplaceholder.typicode.com" },
            new EndpointConfig { EnvironmentKey = "test", EndpointKey = "webapi", Url = "https://httpbin.org" }
        };

        /// <summary>
        /// 严格版地址查询：当前环境下必须存在该端点且地址非空，否则抛异常。
        /// </summary>
        /// <param name="endpointKey">端点键；为空按 "default" 处理。</param>
        public string GetBaseUrl(string endpointKey = "default")
        {
            if (string.IsNullOrWhiteSpace(endpointKey)) endpointKey = "default";

            var match = Endpoints.FirstOrDefault(entry =>
                entry != null &&
                string.Equals(entry.EnvironmentKey, EnvironmentKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.EndpointKey, endpointKey, StringComparison.OrdinalIgnoreCase));

            if (match == null || string.IsNullOrWhiteSpace(match.Url))
            {
                throw new InvalidOperationException(
                    $"未找到环境\"{EnvironmentKey}\"下端点\"{endpointKey}\"的地址配置。");
            }

            return match.Url;
        }

        /// <summary>
        /// 拼接完整地址（测试替身不做 URL 转义，够用即可）。
        /// </summary>
        public string BuildFullUrl(string endpointKey, string path, Dictionary<string, string> query = null)
        {
            string baseUrl = GetBaseUrl(endpointKey).TrimEnd('/');
            string url = baseUrl + "/" + path.TrimStart('/');

            if (query != null && query.Count > 0)
            {
                url += "?" + string.Join("&", query.Select(kv => $"{kv.Key}={kv.Value}"));
            }

            return url;
        }

        /// <summary>
        /// 切换当前环境（替身不建模 dev 保底规则）。
        /// </summary>
        public void SetEnvironment(string environmentKey = "test")
        {
            if (!string.IsNullOrWhiteSpace(environmentKey))
            {
                EnvironmentKey = environmentKey;
            }
        }

        /// <summary>
        /// 新增环境（已存在或键为空时忽略）。
        /// </summary>
        public bool AddEnvironment(string key)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                EnvironmentKeys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            EnvironmentKeys.Add(key);
            return true;
        }

        /// <summary>
        /// 删除环境及其名下端点。
        /// </summary>
        public bool RemoveEnvironment(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            int removed = EnvironmentKeys.RemoveAll(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
            Endpoints.RemoveAll(entry => entry != null &&
                                         string.Equals(entry.EnvironmentKey, key, StringComparison.OrdinalIgnoreCase));

            if (string.Equals(EnvironmentKey, key, StringComparison.OrdinalIgnoreCase))
            {
                EnvironmentKey = EnvironmentKeys.FirstOrDefault() ?? "test";
            }

            return removed > 0;
        }

        /// <summary>
        /// 在指定环境下新增一条默认端点。
        /// </summary>
        public void AddEndpoint(string environmentKey)
        {
            if (string.IsNullOrWhiteSpace(environmentKey)) return;

            if (!EnvironmentKeys.Any(item => string.Equals(item, environmentKey, StringComparison.OrdinalIgnoreCase)))
            {
                EnvironmentKeys.Add(environmentKey);
            }

            Endpoints.Add(new EndpointConfig
            {
                EnvironmentKey = environmentKey,
                EndpointKey = "default",
                Url = string.Empty
            });
        }

        /// <summary>
        /// 修改指定环境下第 <paramref name="localIndex"/> 条端点。
        /// </summary>
        public bool SetEndpoint(string environmentKey, int localIndex, string endpointKey, string url)
        {
            var targets = Endpoints
                .Where(entry => entry != null &&
                                string.Equals(entry.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (localIndex < 0 || localIndex >= targets.Count) return false;

            targets[localIndex].EndpointKey = string.IsNullOrWhiteSpace(endpointKey) ? "default" : endpointKey.Trim();
            targets[localIndex].Url = url?.Trim() ?? string.Empty;
            return true;
        }

        /// <summary>
        /// 删除指定环境下第 <paramref name="localIndex"/> 条端点。
        /// </summary>
        public void RemoveEndpoint(string environmentKey, int localIndex)
        {
            var targets = Endpoints
                .Where(entry => entry != null &&
                                string.Equals(entry.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (localIndex < 0 || localIndex >= targets.Count) return;

            Endpoints.Remove(targets[localIndex]);
        }
    }
}
