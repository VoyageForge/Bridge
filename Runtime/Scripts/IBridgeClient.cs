using Cysharp.Threading.Tasks;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// Bridge 请求客户端的抽象视图（非泛型）。
    /// 这是给"依赖 Bridge 的第三方包"使用的最小契约：包只需要拿到配置、默认端点键和发请求的能力，
    /// 不需要（也不应该）知道宿主具体用的是哪个 <see cref="BridgeClient{T}"/> 子类。
    ///
    /// 第三方包通常不直接使用本接口，而是通过静态门面 <see cref="BridgeHub"/> 调用；
    /// 只有在需要替换/模拟实现时（例如单元测试、或宿主想接入自定义发送实现）才直接面向本接口编程。
    /// </summary>
    public interface IBridgeClient
    {
        /// <summary>
        /// 当前使用的配置对象（环境与端点地址来源）。
        /// </summary>
        IBridgeConfig Config { get; }

        /// <summary>
        /// 默认端点键：请求未显式指定 <see cref="Request.endpointKey"/> 时使用它。
        /// </summary>
        string DefaultEndpointKey { get; }

        /// <summary>
        /// 发送请求。URL 拼接、请求头合并、拦截器链、失败归一化与响应体反序列化都由实现内部完成。
        /// </summary>
        /// <typeparam name="R">响应体反序列化目标类型；传 string 时返回原始响应文本。</typeparam>
        /// <param name="request">请求参数。</param>
        /// <returns>响应容器；网络类失败不会抛异常，而是以 statusCode 为 0 的形式返回。</returns>
        UniTask<Response<R>> SendAsync<R>(Request request) where R : class;
    }
}
