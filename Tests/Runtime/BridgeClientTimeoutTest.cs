using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// BridgeClient 超时行为测试：验证"慢响应不会被提前掐断"与"正常请求按预期完成"。
    ///
    /// <para>
    /// 改造说明：
    /// 1. 原实现依赖公网 httpbin.org 的 /delay 与 /drip，结果不可判定时只能 Inconclusive；
    ///    现改用 <see cref="LocalDelayServer"/> 提供本机可控延迟，断言全部确定。
    /// 2. 等待方式保持 [UnityTest] + IEnumerator 手动轮询：
    ///    当前项目的 Unity Test Framework 是 1.1.33，官方明确不支持 async Task 测试
    ///    （会报 "Method has non-void return value"），因此这里不使用 async/await。
    /// </para>
    /// </summary>
    public class BridgeClientTimeoutTest
    {
        /// <summary>短请求应正常成功（不涉及超时，仅作为正常路径对照）。</summary>
        [UnityTest]
        public IEnumerator TestShortRequest_ShouldSucceed()
        {
            // 本地服务器：仅延迟 300ms，几乎瞬时完成，作为「正常路径」对照。
            using var server = new LocalDelayServer(delayMs: 300);

            var request = new Request
            {
                url = server.Url,
                method = "GET",
                timeoutSeconds = 30
            };

            var task = TestBridgeClient.SendAsync<string>(request);
            var awaiter = task.GetAwaiter();
            float start = Time.time;
            while (!awaiter.IsCompleted)
                yield return null;
            float elapsed = Time.time - start;

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode, $"Status: {response.StatusCode}");
            Assert.AreEqual(LocalDelayServer.ResponseBody, response.Data);

            // 上界给足余量：只排除「明显异常地慢」，不把机器负载绑进断言。
            Assert.IsTrue(elapsed < 15f, $"短请求耗时 {elapsed:F2}s 过长");
            Debug.Log($"✅ 短请求在 {elapsed:F2}s 成功完成（BridgeClient）");
        }

        /// <summary>
        /// 慢响应不应被提前掐断：本地服务器延迟 2 秒响应，客户端超时设为 30 秒，
        /// 请求应按服务端的 2 秒延迟正常完成，而不是被更短的超时提前截断。
        /// </summary>
        [UnityTest]
        public IEnumerator TestLongRequest_ShouldNotTimeoutPrematurely()
        {
            // 本地回环服务器：固定延迟 2 秒后返回 200，不依赖任何公网服务。
            using var server = new LocalDelayServer(delayMs: 2000);

            var request = new Request
            {
                // 完整 URL：BridgeClient 对完整 URL 不拼接任何端点 baseUrl（见 TestFullUrl）。
                url = server.Url,
                method = "GET",
                timeoutSeconds = 30
            };

            var task = TestBridgeClient.SendAsync<string>(request);
            var awaiter = task.GetAwaiter();
            float start = Time.time;
            while (!awaiter.IsCompleted)
                yield return null;
            float elapsed = Time.time - start;

            Debug.Log($"⏱️ Long request finished after {elapsed:F2}s");

            var response = awaiter.GetResult();
            Assert.IsNotNull(response, "SendAsync 不应返回 null");
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"Status: {response.StatusCode}, StatusText: {response.StatusText}");
            Assert.AreEqual(LocalDelayServer.ResponseBody, response.Data,
                "响应体应来自本地延迟服务器");

            // 核心断言：耗时下界证明服务端 2 秒延迟确实被完整等待，没有被更短的超时提前掐断。
            Assert.IsTrue(elapsed >= 1.5f,
                $"请求在 {elapsed:F2}s 就结束，服务端 2s 延迟未生效（可能被提前超时）");

            // 上界给足余量：只排除「明显异常地慢」。
            Assert.IsTrue(elapsed < 15f,
                $"请求耗时 {elapsed:F2}s 过长，可能未正常完成");

            Debug.Log($"✅ 慢响应在 {elapsed:F2}s 正常完成，未被提前超时。");
        }
    }
}
