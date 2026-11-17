using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// <see cref="JsonBridgeConfigProvider"/> 的真实文件读取测试。
    ///
    /// <para>
    /// 背景：其它测试用的都是内存替身 <see cref="TestBridgeConfigProvider"/>，
    /// 从未覆盖「从 StreamingAssets 读 JSON → 反序列化 → 缓存 → 保存」这条真实链路。
    /// 这里用独立的子目录 __BridgeTests__ 做现场隔离，用例结束即清理，不污染真实配置。
    /// </para>
    /// </summary>
    public class JsonBridgeConfigProviderTests
    {
        /// <summary>测试专用相对路径，避免动到项目里真实的 BridgeConfig.json。</summary>
        private const string RelativePath = "__BridgeTests__/BridgeConfig.json";

        /// <summary>测试文件的完整路径。</summary>
        private string _fullPath;

        [SetUp]
        public void SetUp()
        {
            _fullPath = Path.Combine(Application.streamingAssetsPath, RelativePath);
            DeleteConfigFile();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteConfigFile();
        }

        /// <summary>删除测试用的配置文件与目录。</summary>
        private void DeleteConfigFile()
        {
            if (File.Exists(_fullPath))
            {
                File.Delete(_fullPath);
            }

            string directory = Path.GetDirectoryName(_fullPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>把给定配置写成 JSON 文件（模拟「配置文件已存在」）。</summary>
        private void WriteConfigFile(BridgeConfig config)
        {
            string directory = Path.GetDirectoryName(_fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_fullPath, config.ToJson());
        }

        /// <summary>
        /// 编辑器下文件缺失时应自动创建默认配置，并直接返回该默认配置。
        /// </summary>
        [UnityTest]
        public IEnumerator LoadConfig_FileMissing_InEditor_CreatesDefaultConfig()
        {
            var provider = new JsonBridgeConfigProvider(RelativePath);

            IBridgeConfig result = null;
            yield return provider.LoadConfig().ToCoroutine(r => result = r);

            Assert.IsNotNull(result, "编辑器下文件缺失时应创建并返回默认配置");
            Assert.IsTrue(File.Exists(_fullPath), "应在 StreamingAssets 下创建默认配置文件");
            Assert.AreEqual(BridgeConfig.ReservedEnvironmentKey, result.EnvironmentKey,
                "自动创建的默认配置应使用保底环境 dev");
        }

        /// <summary>
        /// 文件已存在时应读取并反序列化，读到文件里写的内容。
        /// </summary>
        [UnityTest]
        public IEnumerator LoadConfig_ReadsExistingJsonFile()
        {
            var written = BridgeConfig.CreateDefault();
            // 注意：EnvironmentKey 必须同时存在于 EnvironmentKeys 里，
            // 否则 ToJson/FromJson 内部的 EnsureDefaults 会把它重置回保底环境 dev。
            written.AddEnvironment("test-env");
            written.SetEnvironment("test-env");
            WriteConfigFile(written);

            var provider = new JsonBridgeConfigProvider(RelativePath);

            IBridgeConfig result = null;
            yield return provider.LoadConfig().ToCoroutine(r => result = r);

            Assert.IsNotNull(result);
            Assert.AreEqual("test-env", result.EnvironmentKey, "应读回文件里的 environmentKey");
        }

        /// <summary>
        /// 多次调用 LoadConfig 应命中缓存，返回同一实例（不会重复读盘）。
        /// </summary>
        [UnityTest]
        public IEnumerator LoadConfig_CachesResult()
        {
            WriteConfigFile(BridgeConfig.CreateDefault());

            var provider = new JsonBridgeConfigProvider(RelativePath);

            IBridgeConfig first = null;
            IBridgeConfig second = null;
            yield return provider.LoadConfig().ToCoroutine(r => first = r);
            yield return provider.LoadConfig().ToCoroutine(r => second = r);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second, "第二次调用应命中缓存，返回同一实例");
        }

        /// <summary>
        /// GetEnvironment 应返回配置文件里的当前环境键。
        /// </summary>
        [UnityTest]
        public IEnumerator GetEnvironment_ReturnsConfiguredEnvironmentKey()
        {
            var written = BridgeConfig.CreateDefault();
            written.AddEnvironment("prod");
            written.SetEnvironment("prod");
            WriteConfigFile(written);

            var provider = new JsonBridgeConfigProvider(RelativePath);

            string environment = null;
            yield return provider.GetEnvironment().ToCoroutine(r => environment = r);

            Assert.AreEqual("prod", environment);
        }

        /// <summary>
        /// SaveConfig 应落盘并清空缓存，使下一次 LoadConfig 读到新写入的值。
        /// </summary>
        [UnityTest]
        public IEnumerator SaveConfig_WritesFileAndClearsCache()
        {
            WriteConfigFile(BridgeConfig.CreateDefault());

            var provider = new JsonBridgeConfigProvider(RelativePath);

            IBridgeConfig before = null;
            yield return provider.LoadConfig().ToCoroutine(r => before = r);
            Assert.IsNotNull(before);
            Assert.AreEqual(BridgeConfig.ReservedEnvironmentKey, before.EnvironmentKey);

            // 写入新配置：SaveConfig 内部会写文件并清缓存。
            var updated = BridgeConfig.CreateDefault();
            updated.AddEnvironment("staging");
            updated.SetEnvironment("staging");
            provider.SaveConfig(updated);

            Assert.IsTrue(File.Exists(_fullPath), "SaveConfig 应写入文件");

            IBridgeConfig after = null;
            yield return provider.LoadConfig().ToCoroutine(r => after = r);

            Assert.IsNotNull(after);
            Assert.AreEqual("staging", after.EnvironmentKey,
                "SaveConfig 应落盘并清缓存，下一次读写到新值");
        }

        /// <summary>
        /// ClearCache 后应重新从文件读取，拿到外部改动的新内容。
        /// </summary>
        [UnityTest]
        public IEnumerator ClearCache_ForcesReloadFromFile()
        {
            WriteConfigFile(BridgeConfig.CreateDefault());

            var provider = new JsonBridgeConfigProvider(RelativePath);

            IBridgeConfig first = null;
            yield return provider.LoadConfig().ToCoroutine(r => first = r);

            // 外部修改文件（绕过 provider），再用 ClearCache 让缓存失效。
            var changed = BridgeConfig.CreateDefault();
            changed.AddEnvironment("changed");
            changed.SetEnvironment("changed");
            WriteConfigFile(changed);

            provider.ClearCache();

            IBridgeConfig second = null;
            yield return provider.LoadConfig().ToCoroutine(r => second = r);

            Assert.IsNotNull(second);
            Assert.AreEqual("changed", second.EnvironmentKey, "ClearCache 后应重新读文件");
            Assert.AreNotSame(first, second, "清缓存后应产生新的配置实例");
        }
    }
}
