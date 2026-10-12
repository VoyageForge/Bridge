using System.Collections.Generic;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// Bridge 网络配置访问接口。
    /// 同时覆盖两类使用场景：
    /// 1) 运行时查询：<see cref="EnvironmentKey"/>、<see cref="GetBaseUrl"/>、<see cref="BuildFullUrl"/>；
    /// 2) 编辑器维护：<see cref="AddEnvironment"/>、<see cref="RemoveEnvironment"/>、<see cref="AddEndpoint"/>、
    ///    <see cref="SetEndpoint"/>、<see cref="RemoveEndpoint"/>。
    /// 所有增删改都必须走这些方法，由实现类统一保证数据规则（去空白、去重、dev 环境保留并置顶、
    /// 端点键为空时回退 default），避免调用方直接改列表导致规则被绕过。
    /// </summary>
    public interface IBridgeConfig
    {
        /// <summary>
        /// 保底环境键。该环境始终存在、不可删除，且始终位于环境列表第一项。
        /// </summary>
        const string DefaultEnvironmentKey = "dev";

        /// <summary>
        /// 获取或设置当前启用的环境键。设置时自动写入环境列表。
        /// </summary>
        string EnvironmentKey { get; set; }

        /// <summary>
        /// 环境键列表（只读视图；增删请使用 <see cref="AddEnvironment"/> / <see cref="RemoveEnvironment"/>）。
        /// </summary>
        List<string> EnvironmentKeys { get; }

        /// <summary>
        /// 端点配置列表（只读视图；增删改请使用 <see cref="AddEndpoint"/> / <see cref="SetEndpoint"/> / <see cref="RemoveEndpoint"/>）。
        /// </summary>
        List<EndpointConfig> Endpoints { get; }

        /// <summary>
        /// 获取当前环境下指定端点的基础地址。
        /// </summary>
        /// <param name="endpointKey">端点键；默认 "default"。</param>
        /// <exception cref="System.InvalidOperationException">当前环境下不存在该端点或地址为空时抛出。</exception>
        string GetBaseUrl(string endpointKey = "default");

        /// <summary>
        /// 根据端点键、路径和查询参数构建完整请求地址（查询参数会做 URL 转义）。
        /// </summary>
        /// <param name="endpointKey">端点键。</param>
        /// <param name="path">相对路径。</param>
        /// <param name="query">查询参数，可为 null。</param>
        string BuildFullUrl(string endpointKey, string path, Dictionary<string, string> query = null);

        /// <summary>
        /// 设置当前启用的环境键。
        /// </summary>
        /// <param name="environmentKey">目标环境键；默认回到保底环境 dev。</param>
        void SetEnvironment(string environmentKey = DefaultEnvironmentKey);

        /// <summary>
        /// 新增环境（已存在或键为空时不做任何改动）。
        /// 新增后实现类会保证 dev 仍位于列表首位。
        /// </summary>
        /// <param name="key">环境键。</param>
        /// <returns>是否真的新增成功。</returns>
        bool AddEnvironment(string key);

        /// <summary>
        /// 删除环境及其名下的全部端点。保底环境 dev 不可删除。
        /// 若删除的是当前环境，则自动切回 dev。
        /// </summary>
        /// <param name="key">环境键。</param>
        /// <returns>是否真的删除成功。</returns>
        bool RemoveEnvironment(string key);

        /// <summary>
        /// 在指定环境下新增一条默认端点（端点键为 "default"，地址为空）。
        /// 环境不存在时会自动补入环境列表。
        /// </summary>
        /// <param name="environmentKey">所属环境键。</param>
        void AddEndpoint(string environmentKey);

        /// <summary>
        /// 修改指定环境下第 <paramref name="localIndex"/> 条端点的键与地址。
        /// 端点键为空时回退为 "default"，地址会去掉首尾空白。
        /// </summary>
        /// <param name="environmentKey">所属环境键。</param>
        /// <param name="localIndex">该环境内的端点序号（从 0 开始，按列表先后顺序）。</param>
        /// <param name="endpointKey">新的端点键。</param>
        /// <param name="url">新的端点地址。</param>
        /// <returns>是否找到并更新了目标端点。</returns>
        bool SetEndpoint(string environmentKey, int localIndex, string endpointKey, string url);

        /// <summary>
        /// 删除指定环境下第 <paramref name="localIndex"/> 条端点。
        /// </summary>
        /// <param name="environmentKey">所属环境键。</param>
        /// <param name="localIndex">该环境内的端点序号（从 0 开始）。</param>
        void RemoveEndpoint(string environmentKey, int localIndex);
    }
}
