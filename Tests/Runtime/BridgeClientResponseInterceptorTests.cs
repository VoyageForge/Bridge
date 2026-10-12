using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 响应拦截器回写能力的测试。
    /// 背景：历史实现把拦截器结果保存到临时对象后直接丢弃，
    /// 拦截器对响应体/状态描述的修改不会体现在调用方拿到的响应上。
    /// </summary>
    public class BridgeClientResponseInterceptorTests
    {
        /// <summary>
        /// 拦截器改写响应文本与状态描述后：
        /// 1) data 必须按新文本重新反序列化；
        /// 2) RawText 必须与新文本一致；
        /// 3) statusText 必须体现拦截器的修改；
        /// 4) 原状态码不受影响。
        /// </summary>
        [UnityTest]
        public IEnumerator TestResponseInterceptorChangesAreWrittenBack()
        {
            const string RewrittenBody = "{\"url\":\"interceptor-rewritten\"}";

            // 拦截器：替换响应体（原始文本）并改写状态描述。
            Func<Response<string>, Response<string>> interceptor = response =>
            {
                response.Data = RewrittenBody;
                response.StatusText = "intercepted";
                return response;
            };

            TestBridgeClient.UseResponseInterceptor(interceptor);
            try
            {
                var request = new Request
                {
                    url = "/get",
                    method = "GET",
                    endpointKey = "webapi"
                };

                var task = TestBridgeClient.SendAsync<InterceptedBodyDto>(request);
                var awaiter = task.GetAwaiter();
                while (!awaiter.IsCompleted) yield return null;

                var response = awaiter.GetResult();
                Assert.IsNotNull(response);
                Assert.IsTrue(response.IsSuccessStatusCode, $"StatusCode: {response.StatusCode}");

                // 1) 拦截器改写后的文本被重新反序列化
                Assert.IsNotNull(response.Data, "拦截器改写响应体后应重新反序列化");
                Assert.AreEqual("interceptor-rewritten", response.Data.url);

                // 2) RawText 与拦截器结果一致
                Assert.AreEqual(RewrittenBody, response.RawText);

                // 3) 状态描述回写生效
                Assert.AreEqual("intercepted", response.StatusText);

                // 4) HTTP 状态码仍来自真实响应
                Assert.AreEqual(200, (int)response.StatusCode);
            }
            finally
            {
                // 拦截器列表是静态的（按泛型类型各存一份），必须清理，避免影响其它测试。
                TestBridgeClient.RemoveResponseInterceptor(interceptor);
            }
        }
    }
}
