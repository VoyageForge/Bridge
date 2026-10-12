using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// <see cref="BridgeHub"/>（第三方包调用门面）的行为测试。
    /// 覆盖：未初始化时的明确异常、客户端首次实例化自动登记、先到先得、显式覆盖，
    /// 以及请求确实经由宿主登记的客户端发出。
    /// </summary>
    public class BridgeHubTests
    {
        /// <summary>
        /// 每个用例结束后复位全局状态，避免 BridgeHub 的静态槽位跨用例污染。
        /// </summary>
        [TearDown]
        public void ResetHub()
        {
            BridgeHub.ClearDefaultClient();
        }

        /// <summary>
        /// 未登记任何客户端时，调用应同步抛出带指引的 <see cref="BridgeNotInitializedException"/>，
        /// 而不是 NullReferenceException 或静默失败。
        /// </summary>
        [Test]
        public void TestNotInitializedThrowsHelpfulException()
        {
            BridgeHub.ClearDefaultClient();
            Assert.IsFalse(BridgeHub.HasClient, "前置条件：门面应为空");

            var ex = Assert.Throws<BridgeNotInitializedException>(() => BridgeHub.GetAsync<string>("/users/1"));

            Assert.IsNotNull(ex);
            Assert.IsTrue(ex.Message.Contains("BridgeClient"),
                $"异常提示应说明应用侧需要做什么（实现并使用 BridgeClient<T> 子类），实际为: {ex.Message}");
        }

        /// <summary>
        /// 客户端首次实例化（读取单例）时应自动登记自己，并且默认端点键来自它的 urlKey。
        /// </summary>
        [Test]
        public void TestClientRegistersItselfOnInitialize()
        {
            // Destroy + ResetDestroyState 保证下一次访问 Instance 会重新创建实例，
            // 从而稳定地触发一次 Initialize（该用例被重复执行时也成立）。
            TestAutoRegisterBridgeClient.Destroy();
            TestAutoRegisterBridgeClient.ResetDestroyState();
            BridgeHub.ClearDefaultClient();
            Assert.IsFalse(BridgeHub.HasClient, "前置条件：门面应为空");

            // 只是读取单例即可触发实例创建与自动登记。
            _ = TestAutoRegisterBridgeClient.Instance;

            Assert.IsTrue(BridgeHub.HasClient, "客户端首次实例化时应自动登记到 BridgeHub");
            Assert.IsNotNull(BridgeHub.Current);
            Assert.AreEqual("default", BridgeHub.Current.DefaultEndpointKey);
            Assert.IsInstanceOf<TestBridgeConfig>(BridgeHub.Current.Config, "门面应能读到该客户端的配置对象");
        }

        /// <summary>
        /// 先到先得：已经有默认客户端时，<see cref="BridgeHub.TrySetDefaultClient"/> 不应顶掉它；
        /// 传 null 同样不生效。
        /// </summary>
        [Test]
        public void TestFirstRegistrationWins()
        {
            BridgeHub.ClearDefaultClient();
            BridgeHub.SetDefaultClient(TestBridgeClient.Instance);
            var occupied = BridgeHub.Current;

            Assert.IsFalse(BridgeHub.TrySetDefaultClient(TestWebApiBridgeClient.Instance),
                "已有默认客户端时 TrySetDefaultClient 不应生效");
            Assert.AreSame(occupied, BridgeHub.Current, "默认客户端不应被替换");

            Assert.IsFalse(BridgeHub.TrySetDefaultClient(null), "传 null 不应生效");
            Assert.AreSame(occupied, BridgeHub.Current);
        }

        /// <summary>
        /// 显式指定（<see cref="BridgeHub.SetDefaultClient"/>）应能覆盖已有客户端，
        /// 供多客户端场景决定"第三方包该用哪一个"。
        /// </summary>
        [Test]
        public void TestSetDefaultClientOverrides()
        {
            BridgeHub.ClearDefaultClient();

            BridgeHub.SetDefaultClient(TestBridgeClient.Instance);
            Assert.AreEqual("default", BridgeHub.Current.DefaultEndpointKey);

            BridgeHub.SetDefaultClient(TestWebApiBridgeClient.Instance);
            Assert.AreEqual("webapi", BridgeHub.Current.DefaultEndpointKey, "显式指定应覆盖已登记的客户端");
        }

        /// <summary>
        /// 传 null 给 <see cref="BridgeHub.SetDefaultClient"/> 应抛 ArgumentNullException，
        /// 并提示使用 ClearDefaultClient 清空。
        /// </summary>
        [Test]
        public void TestSetNullClientThrows()
        {
            Assert.Throws<ArgumentNullException>(() => BridgeHub.SetDefaultClient(null));
        }

        /// <summary>
        /// 第三方包的调用应真正经由登记的客户端发出（默认端点键 webapi → httpbin）。
        /// </summary>
        [UnityTest]
        public IEnumerator TestCallGoesThroughRegisteredClient()
        {
            BridgeHub.ClearDefaultClient();
            BridgeHub.SetDefaultClient(TestWebApiBridgeClient.Instance);

            var task = BridgeHub.GetAsync<string>("/get", "webapi");
            var awaiter = task.GetAwaiter();
            while (!awaiter.IsCompleted)
                yield return null;

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"StatusCode: {response.statusCode}, StatusText: {response.statusText}");
            Assert.IsNotNull(response.data);
            Assert.IsTrue(response.data.Contains("httpbin.org"),
                $"请求未走登记的客户端（webapi 端点应指向 httpbin）: {response.data}");
        }
    }
}
