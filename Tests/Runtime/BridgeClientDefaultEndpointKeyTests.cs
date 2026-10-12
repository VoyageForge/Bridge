using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 默认端点键（urlKey）解析的回归测试。
    /// 背景：历史实现在请求未指定 endpointKey 时错误地回退到"环境键"，
    /// 导致真实配置（环境 dev、端点 default）下直接抛"未找到端点"异常。
    /// 这里通过一个默认端点键为 "webapi" 的客户端来确认 urlKey 确实被采用。
    /// </summary>
    public class BridgeClientDefaultEndpointKeyTests
    {
        /// <summary>
        /// 未指定 endpointKey 时，应由子类 urlKey（"webapi"）决定端点，
        /// 即请求打到 httpbin.org，而不是打到 default 端点（jsonplaceholder）或环境键 "test"。
        /// </summary>
        [UnityTest]
        public IEnumerator TestUrlKeyDrivesDefaultEndpoint()
        {
            var task = TestWebApiBridgeClient.GetAsync<string>("/get");
            var awaiter = task.GetAwaiter();
            while (!awaiter.IsCompleted) yield return null;

            var response = awaiter.GetResult();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode,
                $"StatusCode: {response.StatusCode}，urlKey 可能未生效（环境键被当成了端点键？）");
            Assert.IsNotNull(response.Data);
            Assert.IsTrue(response.Data.Contains("httpbin.org"),
                $"默认端点未按 urlKey 解析，响应内容: {response.Data}");
        }
    }
}
