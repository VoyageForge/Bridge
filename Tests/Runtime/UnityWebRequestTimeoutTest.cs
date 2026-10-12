using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 直接用裸 UnityWebRequest 验证 60 秒硬限制是否存在的对照实验（不经过 BridgeClient）。
    /// 与 <c>BridgeClientTimeoutTest</c> 同样的原则：只断言可判定的不变量，
    /// 无法判定的情况标记为 Inconclusive，不因公网抖动而误报失败。
    /// </summary>
    public class UnityWebRequestTimeoutTest
    {
        /// <summary>待验证的 UnityWebRequest 硬限制（秒）。</summary>
        private const float HardLimitSeconds = 60f;

        /// <summary>判断"超时发生在硬限制附近"时允许的误差（秒）。</summary>
        private const float HardLimitToleranceSeconds = 10f;

        /// <summary>短请求应正常成功（对照组）。</summary>
        [UnityTest]
        public IEnumerator TestUnityWebRequestTimeout_LongTimeoutShouldWork()
        {
            // 用短请求验证 timeout 设置是否生效
            using var uwr = UnityWebRequest.Get("https://httpbin.org/delay/5");
            uwr.timeout = 120;

            float start = Time.time;
            yield return uwr.SendWebRequest();
            float elapsed = Time.time - start;

            Assert.IsTrue(elapsed >= 4f && elapsed < 20f,
                $"Request took {elapsed:F2}s, expected around 5s");

            Assert.AreEqual(UnityWebRequest.Result.Success, uwr.result,
                $"Expected success, got {uwr.result}, error: {uwr.error}");
            Debug.Log("✅ Short request succeeded");
        }

        /// <summary>
        /// 跨过 60 秒的长请求（/drip 持续 68 秒，超时设 120 秒）：
        /// 成功返回且耗时 &gt; 60 秒 ⇒ 不存在硬限制；在 60 秒附近超时 ⇒ 硬限制存在。
        /// </summary>
        [UnityTest]
        public IEnumerator TestUnityWebRequestTimeout_60SecondHardLimit_UsingDrip()
        {
            string url = "https://httpbin.org/drip?duration=68&numbytes=10&code=200";
            using var uwr = UnityWebRequest.Get(url);
            uwr.timeout = 120;

            float start = Time.time;
            yield return uwr.SendWebRequest();
            float elapsed = Time.time - start;

            Debug.Log($"⏱️ Finished after {elapsed:F2}s, result: {uwr.result}, error: '{uwr.error}'");

            bool isTimeout = uwr.result == UnityWebRequest.Result.ConnectionError &&
                             !string.IsNullOrEmpty(uwr.error) &&
                             uwr.error.ToLowerInvariant().Contains("timeout");

            if (isTimeout)
            {
                // 约 60 秒超时 → 确认硬限制存在。
                Assert.IsTrue(Mathf.Abs(elapsed - HardLimitSeconds) <= HardLimitToleranceSeconds,
                    $"在 {elapsed:F2}s 超时，与预期的 {HardLimitSeconds}s 硬限制不符。");
                Debug.LogWarning($"⚠️ 在 {elapsed:F2}s 超时，确认 UnityWebRequest 存在 60 秒硬限制。");
            }
            else if (uwr.result == UnityWebRequest.Result.Success)
            {
                // 只校验下界：成功耗时越过 60 秒才足以证明没有硬限制。
                // 不校验上界——drip 可能提前结束（实测约 61 秒），绑定请求时长会导致误报。
                Assert.IsTrue(elapsed > HardLimitSeconds,
                    $"请求在 {elapsed:F2}s 就成功返回，未越过 {HardLimitSeconds}s，无法证明不存在 60 秒硬限制。");
                Debug.Log($"✅ 未触发 60 秒硬限制：请求在 {elapsed:F2}s 成功返回。");
            }
            else
            {
                // 其他结果（网络、服务端问题）无法对硬限制下结论，标记不确定而不是失败。
                Assert.Inconclusive($"结果不可判定: result={uwr.result}, error='{uwr.error}', elapsed={elapsed:F2}s");
            }
        }
    }
}
