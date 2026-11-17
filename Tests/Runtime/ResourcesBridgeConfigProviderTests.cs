using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// <see cref="ResourcesBridgeConfigProvider"/> 的真实 Resources 读取测试。
    ///
    /// <para>
    /// 该类原在 Samples~（不参与编译），现在移到 Runtime 成为正式可选实现，因此可以直接测试。
    /// 测试会在 Assets/Resources/__BridgeTests__ 下临时创建一个 BridgeConfigAsset，
    /// 用例结束即删除，不污染项目里的真实配置资源。
    /// </para>
    /// </summary>
    public class ResourcesBridgeConfigProviderTests
    {
        /// <summary>Resources 下的测试路径（不含扩展名）。</summary>
        private const string ResourcePath = "__BridgeTests__/BridgeConfig";

        /// <summary>测试资产的磁盘路径（必须在 Assets/Resources 下才能被 Resources 加载到）。</summary>
        private const string AssetPath = "Assets/Resources/__BridgeTests__/BridgeConfig.asset";

        /// <summary>资产所在目录，用于清理空目录。</summary>
        private const string AssetDirectory = "Assets/Resources/__BridgeTests__";

        [SetUp]
        public void SetUp()
        {
            DeleteAsset();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteAsset();
        }

        /// <summary>删除测试资产与目录，恢复干净现场。</summary>
        private static void DeleteAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<BridgeConfigAsset>(AssetPath) != null)
            {
                AssetDatabase.DeleteAsset(AssetPath);
            }

            if (AssetDatabase.IsValidFolder(AssetDirectory))
            {
                AssetDatabase.DeleteAsset(AssetDirectory);
            }
        }

        /// <summary>在 Resources 下创建一份测试用配置资产。</summary>
        private static BridgeConfigAsset CreateAsset()
        {
            if (!AssetDatabase.IsValidFolder(AssetDirectory))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/__BridgeTests__"));
                AssetDatabase.Refresh();
            }

            var asset = ScriptableObject.CreateInstance<BridgeConfigAsset>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return asset;
        }

        /// <summary>
        /// Resources 中找不到配置资源时应返回 null，并输出明确错误。
        /// </summary>
        [UnityTest]
        public IEnumerator LoadConfig_MissingResource_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("未在 Resources 目录中搜索到"));

            var provider = new ResourcesBridgeConfigProvider(ResourcePath);

            IBridgeConfig result = null;
            yield return provider.LoadConfig().ToCoroutine(r => result = r);

            Assert.IsNull(result, "资源缺失时应返回 null 而不是抛异常");
        }

        /// <summary>
        /// Resources 中存在配置资产时应加载并返回该资产实例。
        /// </summary>
        [UnityTest]
        public IEnumerator LoadConfig_ExistingAsset_ReturnsAsset()
        {
            var asset = CreateAsset();

            var provider = new ResourcesBridgeConfigProvider(ResourcePath);

            IBridgeConfig result = null;
            yield return provider.LoadConfig().ToCoroutine(r => result = r);

            Assert.IsNotNull(result, "存在配置资产时应成功加载");
            Assert.AreSame(asset, result, "应返回 Resources 中的同一个资产实例");
        }

        /// <summary>
        /// 配置缺失时 GetEnvironment 应回退到保底环境 dev，而不是抛异常。
        /// </summary>
        [UnityTest]
        public IEnumerator GetEnvironment_MissingResource_ReturnsReservedDev()
        {
            LogAssert.Expect(LogType.Error, new Regex("未在 Resources 目录中搜索到"));

            var provider = new ResourcesBridgeConfigProvider(ResourcePath);

            string environment = null;
            yield return provider.GetEnvironment().ToCoroutine(r => environment = r);

            Assert.AreEqual(BridgeConfigAsset.ReservedEnvironmentKey, environment,
                "配置缺失时应回退到保底环境 dev");
        }

        /// <summary>
        /// 存在配置资产时 GetEnvironment 应返回资产里配置的环境键。
        /// </summary>
        [UnityTest]
        public IEnumerator GetEnvironment_ExistingAsset_ReturnsEnvironmentKey()
        {
            var asset = CreateAsset();
            asset.AddEnvironment("prod");
            asset.SetEnvironment("prod");
            AssetDatabase.SaveAssets();

            var provider = new ResourcesBridgeConfigProvider(ResourcePath);

            string environment = null;
            yield return provider.GetEnvironment().ToCoroutine(r => environment = r);

            Assert.AreEqual("prod", environment, "应返回资产里配置的环境键");
        }
    }
}
