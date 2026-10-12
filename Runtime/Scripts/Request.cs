using System.Collections.Generic;
using System.Threading;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// Bridge 请求数据模型。
    /// 只描述"要发什么请求"，不含任何发送逻辑；发送由 <see cref="BridgeClient{T}"/> 完成。
    /// </summary>
    public class Request
    {
        /// <summary>
        /// 请求地址。
        /// 相对路径（如 "api/user"）会拼接所选端点的 baseUrl；
        /// 以 http 开头的完整地址会被直接使用，不做端点拼接。
        /// </summary>
        public string url;

        /// <summary>
        /// HTTP 方法，如 "GET"、"POST"、"PUT"、"DELETE"。
        /// </summary>
        public string method;

        /// <summary>
        /// 请求体 JSON 文本。为空表示不带请求体；
        /// 非空时会自动以 UTF-8 编码上传并设置 Content-Type: application/json。
        /// </summary>
        public string bodyJson;

        /// <summary>
        /// 本次请求专属的请求头。
        /// 发送前会与 <c>BridgeClient.DefaultHeaders</c> 合并，同名时以本字典中的值为准；
        /// 注意该字典会被发送流程就地补全（不会替换字典实例本身）。
        /// </summary>
        public Dictionary<string, string> headers;

        /// <summary>
        /// 超时秒数，小于等于 0 表示不设置超时（使用 UnityWebRequest 的默认行为）。
        /// </summary>
        public int timeoutSeconds = 30;

        /// <summary>
        /// 指定本次请求使用的端点键（对应配置中同一环境下的某条端点，如 "default"、"webapi"、"socket"）。
        /// 为 null 或空白时，回退到 <c>BridgeClient&lt;T&gt;</c> 子类通过 urlKey 声明的默认端点键。
        /// </summary>
        public string endpointKey;

        /// <summary>
        /// 取消令牌，默认不取消。
        /// 取消会被 <c>BridgeClient.SendAsync</c> 捕获并归一化为失败响应，不会向外抛异常。
        /// </summary>
        public CancellationToken cancellationToken = CancellationToken.None;
    }
}
