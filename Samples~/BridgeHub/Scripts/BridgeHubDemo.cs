using Cysharp.Threading.Tasks;
using UnityEngine;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Sample.Hub
{
    /// <summary>
    /// 【应用侧视角 / 演示入口】把本组件挂到场景中任意 GameObject 上，进入 Play 模式即可看到结果。
    ///
    /// 演示了两件事：
    /// 1. 应用侧只需要"使用一次"自己的客户端（这里读一次 <c>DemoWebClient.Instance</c>），
    ///    它就会自动登记到 <see cref="BridgeHub"/>；
    /// 2. 之后像第三方包那样调用 <see cref="OrderApi"/> 即可拿到数据，包完全不需要知道宿主客户端是什么类型。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BridgeHubDemo : MonoBehaviour
    {
        /// <summary>要查询的订单 id（对应 jsonplaceholder 的 /posts/{id}）。</summary>
        [SerializeField] private int orderId = 1;

        /// <summary>是否额外演示一次"取原始响应文本"。</summary>
        [SerializeField] private bool logRawText = true;

        /// <summary>
        /// 进入 Play 模式后执行演示。
        /// 用 UniTask 的 <c>UniTaskVoid</c> 而不是 <c>async void</c>，避免异常被静默吞掉。
        /// </summary>
        private async UniTaskVoid Start()
        {
            // 应用侧职责：确保自己的客户端已经被创建。
            // 只是读取单例就会触发实例化，并把该客户端自动登记为 BridgeHub 的默认客户端。
            _ = DemoWebClient.Instance;

            // 正式工程通常还会在启动流程里决定环境，例如：
            // DemoWebClient.Instance.SetEnvironmentKey("prod");

            Debug.Log($"[BridgeHubDemo] BridgeHub.HasClient={BridgeHub.HasClient}, " +
                      $"DefaultEndpointKey={BridgeHub.Current?.DefaultEndpointKey}");

            try
            {
                var response = await OrderApi.GetOrderAsync(orderId);

                if (response.IsSuccessStatusCode && response.data != null)
                {
                    Debug.Log($"[BridgeHubDemo] 请求成功: id={response.data.id}, " +
                              $"userId={response.data.userId}, title={response.data.title}");
                }
                else
                {
                    Debug.LogWarning($"[BridgeHubDemo] 请求失败: statusCode={response.statusCode}, " +
                                     $"statusText={response.statusText}");
                }

                if (logRawText)
                {
                    var raw = await OrderApi.GetOrderRawAsync(orderId);
                    Debug.Log($"[BridgeHubDemo] RawText={raw.RawText}");
                }
            }
            catch (BridgeNotInitializedException ex)
            {
                // 没有登记客户端时会走到这里：异常信息本身已经说明了应用侧需要做什么。
                Debug.LogError($"[BridgeHubDemo] {ex.Message}");
            }
        }
    }
}
