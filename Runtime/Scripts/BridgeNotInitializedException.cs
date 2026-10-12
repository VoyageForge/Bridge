using System;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// 在 Bridge 尚未初始化时通过 <see cref="BridgeHub"/> 发请求所抛出的异常。
    /// 抛出该异常说明应用侧还没有"使用过"任何 <see cref="BridgeClient{T}"/> 子类实例，
    /// 因此门面拿不到客户端，也拿不到配置。
    /// </summary>
    public sealed class BridgeNotInitializedException : InvalidOperationException
    {
        /// <summary>
        /// 默认提示：直接说明应用侧需要做什么，避免调用方拿到一个没有上下文的 NullReferenceException。
        /// </summary>
        public const string DefaultMessage =
            "Bridge 尚未初始化：应用侧需要实现并至少使用一次 BridgeClient<T> 子类（例如自己的 WebClient，" +
            "或导入 Packages/Bridge/Samples~/Basic 示例），第三方包才能通过 BridgeHub 发请求。";

        /// <summary>
        /// 使用默认提示构造异常。
        /// </summary>
        public BridgeNotInitializedException() : base(DefaultMessage) { }

        /// <summary>
        /// 使用自定义提示构造异常。
        /// </summary>
        /// <param name="message">异常提示。</param>
        public BridgeNotInitializedException(string message) : base(message) { }
    }
}
