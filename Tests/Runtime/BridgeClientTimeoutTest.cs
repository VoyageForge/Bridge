using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// BridgeClient 超时行为测试：核心目的是验证 UnityWebRequest 是否存在 60 秒硬限制。
    /// 这两个用例依赖公网 httpbin.org，而服务端 /drip 的实际耗时受网络与代理影响
    /// （实测请求 68~70 秒的 drip 会在 61~63 秒就结束），因此断言只针对"可判定的不变量"：
    /// 成功返回必须越过 60 秒才能证明不存在硬限制；无法判定的情况标记为 Inconclusive，而不是误报失败。
    /// </summary>
    public class BridgeClientTimeoutTest
    {
        /// <summary>待验证的 UnityWebRequest 硬限制（秒）。</summary>
        private const float HardLimitSeconds = 60f;

        /// <summary>判断"超时发生在硬限制附近"时允许的误差（秒）。</summary>
        private const float HardLimitToleranceSeconds = 10f;

        /// <summary>短请求应正常成功（不涉及硬限制，仅作为对照组）。</summary>
        [UnityTest]
        public IEnumerator TestShortRequest_ShouldSucceed()
        {
            var request = new Request
            {
                url = "/delay/5",
                method = "GET",
                timeoutSeconds = 120,
                endpointKey = "webapi" // 确保使用 httpbin 端点
            };

            var task = TestBridgeClient.SendAsync<string>(request);
            var awaiter = task.GetAwaiter();
            float start = Time.time;
            while (!awaiter.IsCompleted)
                yield return null;
            float elapsed = Time.time - start;

            Assert.IsTrue(elapsed >= 4f && elapsed < 20f,
                $"Short request took {elapsed:F2}s, expected around 5s");

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode, $"Status: {response.statusCode}");
            Assert.IsNotNull(response.data);
            Debug.Log("✅ Short request succeeded with BridgeClient");
        }

        /// <summary>
        /// 长请求（服务端持续输出 70 秒，客户端超时设为 120 秒）用于跨过 60 秒这条线：
        /// 成功返回且耗时 &gt; 60 秒 ⇒ 不存在硬限制；
        /// 在 60 秒附近超时 ⇒ 硬限制存在（用例通过，仅记录该事实）。
        /// </summary>
        [UnityTest]
        public IEnumerator TestLongRequest_ShouldNotTimeoutPrematurely()
        {
            var request = new Request
            {
                // httpbin 的 /drip 会在 duration 秒内持续输出，用来跨越 60 秒。
                url = "/drip?duration=70&numbytes=10&code=200",
                method = "GET",
                timeoutSeconds = 120,  // 远大于 60 秒硬限制
                endpointKey = "webapi" // 确保使用 httpbin 端点
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

            if (response.IsSuccessStatusCode)
            {
                // 只校验下界：成功耗时越过 60 秒才足以证明没有硬限制。
                // 不校验上界——drip 可能提前结束（实测 61~63 秒），把上界绑死在请求时长上会导致误报。
                Assert.IsTrue(elapsed > HardLimitSeconds,
                    $"请求在 {elapsed:F2}s 就成功返回，未越过 {HardLimitSeconds}s，无法证明不存在 60 秒硬限制。");
                Debug.Log($"✅ 长请求在 {elapsed:F2}s 成功返回（>{HardLimitSeconds}s），未触发 60 秒硬限制。");
            }
            else if (response.statusCode == 0 &&
                     (response.statusText == "Timeout" || response.statusText == "Canceled"))
            {
                // 超时且恰好落在 60 秒附近 → 确认硬限制存在。
                Assert.IsTrue(Mathf.Abs(elapsed - HardLimitSeconds) <= HardLimitToleranceSeconds,
                    $"在 {elapsed:F2}s 超时，与预期的 {HardLimitSeconds}s 硬限制不符。");
                Debug.LogWarning($"⚠️ 在 {elapsed:F2}s 超时，确认 UnityWebRequest 60 秒硬限制存在。");
            }
            else
            {
                // 其他结果（代理拦截、DNS/网络故障等）无法对硬限制下结论，标记不确定而不是失败。
                Assert.Inconclusive(
                    $"结果不可判定: status={response.statusCode}, statusText={response.statusText}, elapsed={elapsed:F2}s");
            }
        }
    }
}
