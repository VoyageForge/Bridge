using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// Bridge 的统一调用门面（静态）。
    ///
    /// 用途：让"依赖 Bridge 的第三方包"只写静态封装函数就能调用 Web API，
    /// 而不需要（也不应该）在自己的包里声明 <see cref="BridgeClient{T}"/> 子类——
    /// 那会与宿主自己的客户端重复，并各自加载一份配置。
    ///
    /// 包作者写法：
    /// <code>
    /// public static UniTask&lt;Response&lt;OrderDto&gt;&gt; GetOrderAsync(int id, CancellationToken ct = default)
    ///     => BridgeHub.GetAsync&lt;OrderDto&gt;($"api/orders/{id}", "orders", cancellationToken: ct);
    /// </code>
    ///
    /// 应用作者前提：应用侧必须实现至少一个 <see cref="BridgeClient{T}"/> 子类（例如自己的 WebClient），
    /// 或直接导入 Packages/Bridge/Samples~/Basic 示例；
    /// 该客户端首次被实例化时会自动登记为本门面的默认客户端。
    /// 尚未登记时调用会同步抛出 <see cref="BridgeNotInitializedException"/>，而不是静默失败。
    ///
    /// 行为说明：所有请求最终都由宿主那个客户端实例发出，
    /// 因此宿主的全局 Headers 与请求/响应拦截器（例如统一加鉴权头）会自动作用于第三方包的请求。
    ///
    /// 线程说明：本类型只在主线程使用（UnityWebRequest 与单例创建都要求主线程）。
    /// 多客户端场景用 <see cref="SetDefaultClient"/> 显式指定；测试复位用 <see cref="ClearDefaultClient"/>。
    /// </summary>
    public static class BridgeHub
    {
        /// <summary>
        /// 当前登记的默认客户端；null 表示尚未初始化。
        /// </summary>
        private static IBridgeClient current;

        /// <summary>
        /// 当前登记的默认客户端；null 表示尚未初始化。
        /// </summary>
        public static IBridgeClient Current => current;

        /// <summary>
        /// 是否已经有可用的客户端。
        /// </summary>
        public static bool HasClient => current != null;

        /// <summary>
        /// 登记默认客户端，但仅在没有默认客户端时生效。
        /// 该方法供 <see cref="BridgeClient{T}"/> 首次实例化时自动调用（先到先得），
        /// 也可由宿主在启动早期主动调用以"抢占"默认位。
        /// </summary>
        /// <param name="client">要登记的客户端；传 null 不做任何事。</param>
        /// <returns>是否被采纳（false 表示已有默认客户端或传入为 null）。</returns>
        public static bool TrySetDefaultClient(IBridgeClient client)
        {
            if (client == null || current != null) return false;

            current = client;
            return true;
        }

        /// <summary>
        /// 显式指定（或替换）默认客户端，用于多客户端场景下决定"第三方包该用哪一个"。
        /// </summary>
        /// <param name="client">要指定的客户端，不能为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> 为 null 时抛出。</exception>
        public static void SetDefaultClient(IBridgeClient client)
        {
            // 清空语义交给 ClearDefaultClient，避免 set null 被误当成"设置了一个空客户端"。
            current = client ?? throw new ArgumentNullException(nameof(client),
                $"client 不能为 null；若要清空默认客户端，请调用 {nameof(ClearDefaultClient)}()。");
        }

        /// <summary>
        /// 清空默认客户端（主要供测试复位使用；下一个被实例化的客户端会重新登记）。
        /// </summary>
        public static void ClearDefaultClient() => current = null;

        // ======================== 发送入口 ========================

        /// <summary>
        /// 核心发送入口：把请求交给当前登记的客户端。
        /// </summary>
        /// <typeparam name="R">响应体反序列化目标类型；传 string 时返回原始响应文本。</typeparam>
        /// <param name="request">请求参数。</param>
        /// <exception cref="BridgeNotInitializedException">尚未登记默认客户端时同步抛出（便于调用方立即定位问题）。</exception>
        public static UniTask<Response<R>> SendAsync<R>(Request request) where R : class
            => Resolve().SendAsync<R>(request);

        /// <summary>GET 请求（使用客户端默认端点键）。</summary>
        public static UniTask<Response<R>> GetAsync<R>(string url, Dictionary<string, string> headers = null,
            int timeoutSeconds = 30, CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "GET", null, headers, timeoutSeconds, cancellationToken));

        /// <summary>GET 请求（指定端点键）。</summary>
        public static UniTask<Response<R>> GetAsync<R>(string url, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "GET", null, headers, timeoutSeconds, cancellationToken, endpointKey));

        /// <summary>POST 请求（使用客户端默认端点键）。</summary>
        public static UniTask<Response<R>> PostAsync<R>(string url, string bodyJson,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "POST", bodyJson, headers, timeoutSeconds, cancellationToken));

        /// <summary>POST 请求（指定端点键）。</summary>
        public static UniTask<Response<R>> PostAsync<R>(string url, string bodyJson, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "POST", bodyJson, headers, timeoutSeconds, cancellationToken, endpointKey));

        /// <summary>PUT 请求（使用客户端默认端点键）。</summary>
        public static UniTask<Response<R>> PutAsync<R>(string url, string bodyJson,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "PUT", bodyJson, headers, timeoutSeconds, cancellationToken));

        /// <summary>PUT 请求（指定端点键）。</summary>
        public static UniTask<Response<R>> PutAsync<R>(string url, string bodyJson, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "PUT", bodyJson, headers, timeoutSeconds, cancellationToken, endpointKey));

        /// <summary>DELETE 请求（使用客户端默认端点键）。</summary>
        public static UniTask<Response<R>> DeleteAsync<R>(string url, Dictionary<string, string> headers = null,
            int timeoutSeconds = 30, CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "DELETE", null, headers, timeoutSeconds, cancellationToken));

        /// <summary>DELETE 请求（指定端点键）。</summary>
        public static UniTask<Response<R>> DeleteAsync<R>(string url, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
            => Resolve().SendAsync<R>(BridgeRequestFactory.Create(url, "DELETE", null, headers, timeoutSeconds, cancellationToken, endpointKey));

        /// <summary>
        /// 取出当前客户端；尚未初始化时抛 <see cref="BridgeNotInitializedException"/>。
        /// 采用同步抛出（而不是返回失败任务），调用方能立刻看到明确原因。
        /// </summary>
        private static IBridgeClient Resolve() => current ?? throw new BridgeNotInitializedException();
    }
}
