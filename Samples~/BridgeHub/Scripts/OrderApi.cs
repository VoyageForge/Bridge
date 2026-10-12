using System.Threading;
using Cysharp.Threading.Tasks;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Sample.Hub
{
    /// <summary>
    /// 【第三方包视角】对外的静态 API 封装。
    ///
    /// 这是本示例要演示的重点：依赖 Bridge 的包**只需要写这样的静态封装函数**，
    /// 全部通过 <see cref="BridgeHub"/> 调用即可，不需要（也不应该）在包里声明
    /// <c>BridgeClient&lt;T&gt;</c> 子类，更不要 <c>new</c> 宿主的客户端。
    ///
    /// 端点键由包自己约定（这里用 "orders"），地址由应用侧的配置决定。
    /// </summary>
    public static class OrderApi
    {
        /// <summary>
        /// 本包约定的端点键；应用侧需要在配置里为它准备一条端点地址。
        /// </summary>
        public const string EndpointKey = "orders";

        /// <summary>
        /// 查询一条订单，并把响应体反序列化为 <see cref="OrderDto"/>。
        /// </summary>
        /// <param name="id">订单 id。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>响应容器；网络类失败不会抛异常，而是以 statusCode 为 0 的形式返回。</returns>
        public static UniTask<Response<OrderDto>> GetOrderAsync(int id, CancellationToken cancellationToken = default)
            => BridgeHub.GetAsync<OrderDto>($"posts/{id}", EndpointKey, cancellationToken: cancellationToken);

        /// <summary>
        /// 查询一条订单的原始响应文本（<c>R</c> 传 string 时 Bridge 不做 JSON 反序列化）。
        /// 用于演示"需要自己解析"或排查问题时怎么取到原文。
        /// </summary>
        /// <param name="id">订单 id。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public static UniTask<Response<string>> GetOrderRawAsync(int id, CancellationToken cancellationToken = default)
            => BridgeHub.GetAsync<string>($"posts/{id}", EndpointKey, cancellationToken: cancellationToken);
    }
}
