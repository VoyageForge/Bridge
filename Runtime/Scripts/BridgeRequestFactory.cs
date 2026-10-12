using System.Collections.Generic;
using System.Threading;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// 请求对象工厂：统一 <see cref="Request"/> 的构造口径（超时、取消令牌、端点键的默认处理）。
    /// 由 <see cref="BridgeClient{T}"/> 的静态便捷方法与 <see cref="BridgeHub"/> 共用，
    /// 避免两处各写一份导致行为漂移。
    /// </summary>
    internal static class BridgeRequestFactory
    {
        /// <summary>
        /// 创建请求对象。
        /// 刻意不对 <paramref name="endpointKey"/> 做兜底：留 null 交由发送侧统一回退到默认端点键，
        /// 保证"默认端点"只有一个判定入口。
        /// </summary>
        /// <param name="url">请求地址（相对路径或完整 URL）。</param>
        /// <param name="method">HTTP 方法。</param>
        /// <param name="bodyJson">请求体 JSON，可为 null。</param>
        /// <param name="headers">请求头，可为 null。</param>
        /// <param name="timeoutSeconds">超时秒数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="endpointKey">端点键；null 表示使用发送侧的默认端点键。</param>
        internal static Request Create(string url, string method, string bodyJson = null,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default, string endpointKey = null)
        {
            return new Request
            {
                url = url,
                method = method,
                bodyJson = bodyJson,
                headers = headers,
                timeoutSeconds = timeoutSeconds,
                endpointKey = endpointKey,
                // default(CancellationToken) 与 CancellationToken.None 等价，这里显式归一化，便于阅读与调试。
                cancellationToken = cancellationToken == default ? CancellationToken.None : cancellationToken
            };
        }
    }
}
