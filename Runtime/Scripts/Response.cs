using System.Collections.Generic;
using System.Net;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// 响应数据容器。
    /// 无论请求成功还是失败，<see cref="BridgeClient{T}"/> 都返回本类型的实例：
    /// 网络异常、超时或被取消时 <see cref="StatusCode"/> 为 0，失败原因放在 <see cref="StatusText"/>。
    /// </summary>
    /// <typeparam name="T">
    /// 响应体类型：普通 DTO 时是 JSON 反序列化结果；为 <c>string</c> 时直接是原始响应文本（跳过反序列化）。
    /// </typeparam>
    public class Response<T>
    {
        /// <summary>
        /// 响应体。
        /// 成功时为 <typeparamref name="T"/> 的反序列化结果（T 为 string 时即原始文本）；
        /// 失败、响应体为空或反序列化失败时为 null。
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// HTTP 状态码（直接使用 <see cref="HttpStatusCode"/> 枚举，如 OK / NotFound）。
        /// 网络层失败（超时、取消、连接错误）时为 0。
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// 状态描述。
        /// HTTP 请求正常结束时来自 <c>UnityWebRequest.result</c>（如 "Success"、"ProtocolError"）；
        /// 抛出异常时记录异常类型名；取消/超时则记录 "Canceled" / "Timeout"。
        /// </summary>
        public string StatusText { get; set; }

        /// <summary>
        /// 响应头。
        /// 注意：当前实现只保证成功路径下不为 null，但内容是空字典（尚未从 UnityWebRequest 回填真实响应头）；
        /// 需要读取真实响应头时，请在拦截器里自行读取底层请求。
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// 原始响应文本（未反序列化的原文）。
        /// 仅在 HTTP 成功返回时赋值，失败路径为 null；被响应拦截器改写时会同步刷新。
        /// </summary>
        public string RawText { get; set; }

        /// <summary>
        /// 简单判断是否成功：状态码落在 2xx 区间。
        /// 注意网络层失败时 <see cref="StatusCode"/> 为 0，同样会被判为失败。
        /// </summary>
        public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode < 300;
    }
}
