namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 响应拦截器回写测试使用的响应体模型。
    /// 单独成文件（一个类型一个文件），供 <c>BridgeClientResponseInterceptorTests</c> 使用。
    /// </summary>
    public class InterceptedBodyDto
    {
        /// <summary>httpbin /get 返回的请求地址字段。</summary>
        public string url { get; set; }
    }
}
