using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Sample
{
    /// <summary>
    /// 
    /// </summary>
    public class WebClient : BridgeClient<WebClient>
    {
        /// <summary>
        /// 配置提供器：从 Resources 读取 BridgeConfigAsset。
        ///
        /// <para>
        /// 这里刻意用「私有实例字段 + 属性返回该字段」而不是表达式体 <c>=&gt; new ...</c>：
        /// 表达式体在语法上每次访问都会 new 一个实例；虽然
        /// <see cref="BridgeClient{T}.GetConfigAsync"/> 有缓存、整个生命周期只会访问本属性一次，
        /// 但用字段能保证「无论被访问多少次都只创建一次」，语义更稳。
        /// </para>
        ///
        /// <para>
        /// 也**不能**改成 static 字段来缓存：C# 静态成员不能 override，
        /// 那样派生类就失去「各自决定配置来源」的能力。
        /// 若将来新增 Reload / 加载失败重试（会二次访问），实例字段同样安全。
        /// </para>
        /// </summary>
        protected override IBridgeConfigProvider ConfigProvider => _resourcesBridgeConfigProvider;

        /// <summary>
        /// 从 Resources 中加载配置文件（实例字段：每个客户端实例只创建一次）。
        /// </summary>
        private readonly ResourcesBridgeConfigProvider _resourcesBridgeConfigProvider = new("VoyageForge/Config/BridgeConfig");
        
        protected override string urlKey => "default";
        
    }
}