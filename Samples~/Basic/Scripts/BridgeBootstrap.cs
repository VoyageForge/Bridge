using UnityEngine;

namespace VoyageForge.Bridge.Sample
{
    /// <summary>
    /// 示例引导：让导入本示例的工程"开箱即可用"。
    ///
    /// 背景：依赖 Bridge 的第三方包通过 <c>BridgeHub</c> 发请求，而 BridgeHub 需要宿主
    /// 已经"使用过"某个 BridgeClient&lt;T&gt; 实例（实例化时会自动登记）。
    /// 本类在运行时启动阶段主动触碰一次示例 WebClient 单例，完成这次登记。
    ///
    /// 正式工程不必使用本文件：只要自身的启动流程里切换过环境或发过第一个请求，
    /// 效果相同（例如在游戏启动时调用 <c>WebClient.SetEnvironmentKey("prod")</c>）。
    /// </summary>
    public static class BridgeBootstrap
    {
        /// <summary>
        /// 在场景加载前触碰一次示例客户端，使其登记到 BridgeHub。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // 只读取单例即可触发实例创建与 BridgeHub 自动登记；这里不需要保留引用。
            _ = WebClient.Instance;
        }
    }
}
