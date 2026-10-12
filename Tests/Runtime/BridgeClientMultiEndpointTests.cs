using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 多端点场景的 PlayMode 测试：完整 URL、默认端点、自定义端点。
    /// 依赖的公网地址：jsonplaceholder.typicode.com（default 端点）与 httpbin.org（webapi 端点）。
    /// </summary>
    public class BridgeClientMultiEndpointTests
    {
        /// <summary>完整 URL 请求：不应拼接任何端点 baseUrl。</summary>
        [UnityTest]
        public IEnumerator TestFullUrl()
        {
            var task = TestBridgeClient.GetAsync<string>("https://jsonplaceholder.typicode.com/posts/1");
            var awaiter = task.GetAwaiter();
            while (!awaiter.IsCompleted) yield return null;

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode, $"StatusCode: {response.statusCode}");
            Assert.IsNotNull(response.data);
            // 兼容 JSON 格式化（可能带空格）
            Assert.IsTrue(response.data.Contains("\"id\": 1") || response.data.Contains("\"id\":1"), $"Response missing 'id:1': {response.data}");
        }

        /// <summary>
        /// 默认端点请求：未指定 endpointKey，应由 urlKey（"default"）解析端点。
        /// 若解析逻辑退化为使用环境键（"test"），严格版 TestBridgeConfig 会直接抛异常，测试失败。
        /// </summary>
        [UnityTest]
        public IEnumerator TestDefaultEndpoint()
        {
            var task = TestBridgeClient.GetAsync<string>("/users/1");
            var awaiter = task.GetAwaiter();
            while (!awaiter.IsCompleted) yield return null;

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.IsNotNull(response.data);
            Assert.IsTrue(response.data.Contains("\"id\": 1") || response.data.Contains("\"id\":1"));
        }

        /// <summary>自定义端点请求：显式指定 endpointKey = "webapi"。</summary>
        [UnityTest]
        public IEnumerator TestCustomEndpoint()
        {
            var task = TestBridgeClient.GetAsync<string>("/get", "webapi");
            var awaiter = task.GetAwaiter();
            while (!awaiter.IsCompleted) yield return null;

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.IsNotNull(response.data);
            // 检查包含 "url" 字段和 "httpbin.org"
            Assert.IsTrue(response.data.Contains("\"url\"") && response.data.Contains("httpbin.org"), $"Response missing url or host: {response.data}");
        }

        // [UnityTest]
        // public IEnumerator TestTimeout_ShouldFailFast()
        // {
        //     var request = new Request
        //     {
        //         url = "https://httpbin.org/delay/5",
        //         method = "GET",
        //         timeoutSeconds = 2
        //     };
        //     var task = TestBridgeClient.SendAsync<string>(request);
        //     var awaiter = task.GetAwaiter();
        //     float start = Time.time;
        //     while (!awaiter.IsCompleted) yield return null;
        //     float elapsed = Time.time - start;
        //
        //     Assert.IsTrue(elapsed < 3f, $"Timeout took {elapsed}s, expected <3s");
        //
        //     var response = awaiter.GetResult();
        //     Assert.IsNotNull(response);
        //     Assert.IsFalse(response.IsSuccessStatusCode, "Should fail due to timeout");
        //     Assert.IsNull(response.data);
        // }
        //
        // [UnityTest]
        // public IEnumerator TestLongRunningRequest_ShouldSucceed()
        // {
        //     var request = new Request
        //     {
        //         url = "https://httpbin.org/delay/3",
        //         method = "GET",
        //         timeoutSeconds = 30
        //     };
        //     var task = TestBridgeClient.SendAsync<string>(request);
        //     var awaiter = task.GetAwaiter();
        //     float start = Time.time;
        //     while (!awaiter.IsCompleted) yield return null;
        //     float elapsed = Time.time - start;
        //
        //     // 允许网络波动，放宽到 2~12 秒
        //     Assert.IsTrue(elapsed >= 2f && elapsed < 12f, $"Request took {elapsed}s, expected ~3s");
        //
        //     var response = awaiter.GetResult();
        //     Assert.IsNotNull(response);
        //     Assert.IsTrue(response.IsSuccessStatusCode, $"StatusCode: {response.statusCode}, StatusText: {response.statusText}");
        //     Assert.IsNotNull(response.data);
        //
        //     // 验证响应是包含 'url' 字段的 JSON（httpbin /delay 返回的就是 /get 的响应）
        //     Assert.IsTrue(response.data.Contains("\"url\"") && response.data.Contains("httpbin.org"), $"Response missing url or host: {response.data}");
        // }


        /// <summary>
        /// 长超时不应被提前掐断：服务端延迟 5 秒、超时设置为 120 秒，应在 5 秒左右完成。
        /// </summary>
        [UnityTest]
        public IEnumerator TestLongTimeout_ShouldNotTimeoutPrematurely()
        {
            // 设置超时 120 秒，远大于之前 UnityWebRequest 的 60 秒限制
            var request = new Request
            {
                url = "https://httpbin.org/delay/5", // 服务端延迟 5 秒
                method = "GET",
                timeoutSeconds = 120
            };

            var task = TestBridgeClient.SendAsync<string>(request);
            var awaiter = task.GetAwaiter();

            float start = Time.time;
            while (!awaiter.IsCompleted)
                yield return null;
            float elapsed = Time.time - start;

            // 请求应在 5 秒左右完成（允许网络波动），而不是 60 秒
            Assert.IsTrue(elapsed >= 4f && elapsed < 15f,
                $"Request took {elapsed:F2}s, expected around 5s (should not hit 60s timeout)");

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode, $"StatusCode: {response.statusCode}");
            Assert.IsNotNull(response.data);

            // 验证返回的是有效 JSON（包含 "url" 字段）
            Assert.IsTrue(response.data.Contains("\"url\""),
                $"Response missing 'url' field: {response.data}");
        }
    }
}
