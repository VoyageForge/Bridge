using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 直接用裸 <see cref="UnityWebRequest"/> 验证超时行为的对照实验（不经过 BridgeClient）。
    ///
    /// <para>
    /// 改造说明：
    /// 1. 原实现依赖公网 httpbin.org，无法判定时只能 Inconclusive；
    ///    现改用 <see cref="LocalDelayServer"/> 提供本机可控延迟，断言全部确定。
    /// 2. 等待方式保持 [UnityTest] + IEnumerator：
    ///    当前项目 Unity Test Framework 1.1.33 不支持 async Task 测试，
    ///    所以这里用 UnityWebRequestAsyncOperation 协程等待，而不是 await。
    /// </para>
    /// </summary>
    public class UnityWebRequestTimeoutTest
    {
        /// <summary>短请求应正常成功（对照组，证明本机回环链路可用）。</summary>
        [UnityTest]
        public IEnumerator TestUnityWebRequestTimeout_ShortRequestSucceeds()
        {
            // 本地服务器：仅延迟 300ms，几乎瞬时返回。
            using var server = new LocalDelayServer(delayMs: 300);
            using var uwr = UnityWebRequest.Get(server.Url);
            uwr.timeout = 30;

            float start = Time.time;
            yield return uwr.SendWebRequest();
            float elapsed = Time.time - start;

            Assert.AreEqual(UnityWebRequest.Result.Success, uwr.result,
                $"Expected success, got {uwr.result}, error: {uwr.error}");
            Assert.AreEqual(LocalDelayServer.ResponseBody, uwr.downloadHandler.text);
            Assert.IsTrue(elapsed < 15f, $"请求耗时 {elapsed:F2}s 过长");
            Debug.Log($"✅ 短请求在 {elapsed:F2}s 成功完成（裸 UnityWebRequest）");
        }

        /// <summary>
        /// 慢响应不应被提前截断：本地服务器延迟 2 秒响应，超时设为 30 秒，
        /// 请求应按服务端的 2 秒延迟正常完成。
        /// </summary>
        [UnityTest]
        public IEnumerator TestUnityWebRequestTimeout_SlowResponseIsNotTruncated()
        {
            // 本地服务器：固定延迟 2 秒后返回 200。
            using var server = new LocalDelayServer(delayMs: 2000);
            using var uwr = UnityWebRequest.Get(server.Url);
            uwr.timeout = 30;

            float start = Time.time;
            yield return uwr.SendWebRequest();
            float elapsed = Time.time - start;

            Debug.Log($"⏱️ Finished after {elapsed:F2}s, result: {uwr.result}, error: '{uwr.error}'");

            Assert.AreEqual(UnityWebRequest.Result.Success, uwr.result,
                $"Expected success, got {uwr.result}, error: '{uwr.error}'");
            Assert.AreEqual(LocalDelayServer.ResponseBody, uwr.downloadHandler.text);

            // 核心断言：耗时下界证明服务端 2 秒延迟被完整等待，未被提前截断。
            Assert.IsTrue(elapsed >= 1.5f,
                $"请求在 {elapsed:F2}s 就结束，服务端 2s 延迟未生效（可能被提前超时）");

            // 上界给足余量，只排除明显异常。
            Assert.IsTrue(elapsed < 15f, $"请求耗时 {elapsed:F2}s 过长，可能未正常完成");
            Debug.Log($"✅ 慢响应在 {elapsed:F2}s 正常完成，未被提前截断。");
        }
    }
}
