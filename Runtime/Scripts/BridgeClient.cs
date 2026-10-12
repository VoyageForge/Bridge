using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using VoyageForge.Depot.Runtime.Utilities;

namespace VoyageForge.Bridge.Runtime
{
    /// <summary>
    /// Bridge 运行时请求入口（泛型单例基类）。
    /// 负责：端点 baseUrl 拼接、全局请求头合并、请求/响应拦截器链、
    /// UnityWebRequest 发送、失败归一化与响应体反序列化。
    /// 派生类只需要提供配置来源（<see cref="ConfigProvider"/>）与默认端点键（<see cref="urlKey"/>）。
    ///
    /// 这是**应用侧**（宿主工程）使用的类型：每个应用实现自己的客户端（如 WebClient）。
    /// **依赖 Bridge 的第三方包不要在自己的包里声明本类的子类**——那会与宿主的客户端重复并各自加载一份配置；
    /// 包应当通过静态门面 <see cref="BridgeHub"/> 调用，本类型首次实例化时会自动登记到该门面。
    /// </summary>
    /// <typeparam name="T">
    /// 派生类型自身。每个封闭泛型类型拥有各自的单例与静态拦截器列表，因此不同服务互不干扰。
    /// </typeparam>
    public abstract class BridgeClient<T> : Singleton<T>, IBridgeClient where T : BridgeClient<T>, new()
    {
        /// <summary>
        /// 配置提供器：决定当前环境与端点地址从何处加载（Resources 资产、StreamingAssets JSON 等）。
        /// </summary>
        protected abstract IBridgeConfigProvider ConfigProvider { get; }

        /// <summary>
        /// 当前使用的配置对象。首次访问时通过 <see cref="ConfigProvider"/> 加载并缓存。
        /// </summary>
        public IBridgeConfig Config
        {
            get
            {
                _config ??= ConfigProvider.LoadConfig();
                return _config;
            }
        }
        private IBridgeConfig _config;

        /// <summary>
        /// 默认端点键的静态快捷访问，等价于 <c>Instance.urlKey</c>。
        /// </summary>
        public static string UrlKey => Instance.urlKey;

        /// <summary>
        /// 默认端点键：请求未显式指定 <see cref="Request.endpointKey"/> 时使用该端点键。
        /// 默认值为 "default"；派生类可重写为自身约定的端点键（如 "webapi"、"socket"）。
        /// </summary>
        protected virtual string urlKey => "default";

        /// <summary>
        /// <see cref="IBridgeClient"/> 实现：把默认端点键暴露给 <see cref="BridgeHub"/>（第三方包）使用。
        /// 这里用显式实现，避免在公开 API 上再增加一个与 <see cref="UrlKey"/> 语义重复的成员。
        /// </summary>
        string IBridgeClient.DefaultEndpointKey => urlKey;

        /// <summary>
        /// 请求失败时触发（网络异常、超时、取消、非 2xx 结果、JSON 反序列化失败），参数为错误描述。
        /// </summary>
        public event Action<string> OnError;

        /// <summary>
        /// 单例实例创建后自动把自己登记为 <see cref="BridgeHub"/> 的默认客户端（先到先得，只在没有默认客户端时生效）。
        /// 这样第三方包无需知道宿主客户端的具体类型即可发请求；只是"读取单例"就会触发这次登记。
        /// </summary>
        protected override void Initialize()
        {
            BridgeHub.TrySetDefaultClient(this);
        }

        /// <summary>
        /// 读取当前环境下指定端点的基础地址；<paramref name="key"/> 为空时使用默认端点键。
        /// </summary>
        /// <param name="key">端点键；为空表示使用 <see cref="urlKey"/>。</param>
        public string GetBaseUrl(string key = null)
            => Config.GetBaseUrl(string.IsNullOrWhiteSpace(key) ? urlKey : key);

        /// <summary>
        /// 切换当前环境。
        /// </summary>
        /// <param name="environmentKey">要切换到的环境键；为空表示回到保底环境 dev。</param>
        public void SetEnvironmentKey(string environmentKey = "")
        {
            if (string.IsNullOrEmpty(environmentKey))
                Config.SetEnvironment();
            else
                Config.SetEnvironment(environmentKey);
        }

        /// <summary>
        /// 全局默认请求头，所有请求发送前都会合并（请求自身的同名头优先，不会被覆盖）。
        /// </summary>
        public static Dictionary<string, string> DefaultHeaders { get; set; } = new();

        // 拦截器列表是静态字段：C# 中泛型类的静态字段按"封闭泛型类型"各存一份，
        // 因此 BridgeClient<A> 与 BridgeClient<B> 的拦截器链天然隔离，互不影响。
        private static readonly List<Func<Request, Request>> requestInterceptors = new();
        private static readonly List<Func<Response<string>, Response<string>>> responseInterceptors = new();

        /// <summary>注册请求拦截器（在 URL 拼接、请求头合并之后，发送之前执行）。</summary>
        public static void UseRequestInterceptor(Func<Request, Request> interceptor) => requestInterceptors.Add(interceptor);

        /// <summary>移除已注册的请求拦截器。</summary>
        public static void RemoveRequestInterceptor(Func<Request, Request> interceptor) => requestInterceptors.Remove(interceptor);

        /// <summary>
        /// 注册响应拦截器（HTTP 成功返回且响应体反序列化之后执行）。
        /// 拦截器操作的是原始文本形式的响应（<see cref="Response{T}"/> 的 T 为 string），
        /// 对状态码、状态描述、响应头和响应文本的修改都会回写到最终返回的响应上。
        /// </summary>
        public static void UseResponseInterceptor(Func<Response<string>, Response<string>> interceptor) => responseInterceptors.Add(interceptor);

        /// <summary>移除已注册的响应拦截器。</summary>
        public static void RemoveResponseInterceptor(Func<Response<string>, Response<string>> interceptor) => responseInterceptors.Remove(interceptor);

        // ======================== 核心发送方法 ========================

        /// <summary>
        /// <see cref="IBridgeClient"/> 的显式实现：第三方包通过 <see cref="BridgeHub"/> 调到实例管线。
        /// </summary>
        UniTask<Response<R>> IBridgeClient.SendAsync<R>(Request request) => SendCoreAsync<R>(request);

        /// <summary>
        /// 核心异步请求（实例管线）：
        /// 解析端点键 → 拼接 baseUrl → 合并请求头 → 执行请求拦截器 → 发送 →
        /// 归一化失败结果 / 反序列化响应体 → 执行响应拦截器。
        /// 该方法不会向外抛出网络异常，所有失败都以 <see cref="Response{T}"/> 形式返回（statusCode 为 0 或原始状态码）。
        /// </summary>
        /// <remarks>
        /// 之所以叫 SendCoreAsync 而不是把发送逻辑直接做成公开实例方法：
        /// C# 不允许"静态成员与实例成员仅以 static 区分"的重载，
        /// 而 <see cref="SendAsync{R}(Request)"/> 这个静态入口必须为既有调用方保留。
        /// 因此管线放在私有实例方法里，由静态入口、<see cref="IBridgeClient"/> 与 <see cref="BridgeHub"/> 共同复用。
        /// </remarks>
        /// <typeparam name="R">响应体反序列化目标类型；传 string 时直接返回原始响应文本。</typeparam>
        /// <param name="request">请求参数。注意：url 会被补全为完整地址，headers 字典会被就地合并全局请求头。</param>
        private async UniTask<Response<R>> SendCoreAsync<R>(Request request) where R : class
        {
            // 1. 解析本次请求使用的端点键。
            //    请求未指定时回退到本客户端的默认端点键（urlKey）。
            //    历史上这里误用了配置的环境键（EnvironmentKey），导致 "不指定端点就请求失败"。
            string endpointKey = string.IsNullOrWhiteSpace(request.endpointKey) ? urlKey : request.endpointKey;

            // 2. BaseURL：相对路径拼接所选端点的 baseUrl；
            //    完整 URL（http 开头，忽略大小写）直接请求，不做任何拼接。
            if (!request.url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                request.url = Config.GetBaseUrl(endpointKey).TrimEnd('/') + "/" + request.url.TrimStart('/');

            // 3. Headers：请求头为空则新建，再把全局默认头补齐（同名不覆盖请求自身设置的值）。
            request.headers ??= new Dictionary<string, string>();
            foreach (var kv in DefaultHeaders)
                if (!request.headers.ContainsKey(kv.Key))
                    request.headers[kv.Key] = kv.Value;

            // 4. 请求拦截器：可修改 URL、方法、请求体、请求头等。
            //    拦截器返回 null 时保留当前请求对象，避免链路被意外中断。
            foreach (var interceptor in requestInterceptors)
            {
                if (interceptor == null) continue;
                request = interceptor.Invoke(request) ?? request;
            }

            // 5. 创建并填充 UnityWebRequest。
            using var uwr = new UnityWebRequest(request.url, request.method);
            uwr.downloadHandler = new DownloadHandlerBuffer();

            // 超时秒数大于 0 时才显式设置（<=0 表示交给 UnityWebRequest 默认处理）。
            if (request.timeoutSeconds > 0)
                uwr.timeout = request.timeoutSeconds;

            // 有请求体时以 UTF-8 原始字节上传，并声明 JSON 内容类型。
            if (!string.IsNullOrEmpty(request.bodyJson))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(request.bodyJson);
                uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                uwr.SetRequestHeader("Content-Type", "application/json");
            }

            if (request.headers != null)
            {
                foreach (var kv in request.headers)
                    uwr.SetRequestHeader(kv.Key, kv.Value);
            }

            // 6. 发送并捕获所有异常（超时、取消、网络错误等），统一归一化为失败响应。
            try
            {
                await uwr.SendWebRequest().ToUniTask(cancellationToken: request.cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BridgeClient] 请求异常: {ex.Message} (URL: {request.url})");
                var errorResponse = new Response<R>
                {
                    statusCode = 0,
                    statusText = ex.GetType().Name,
                    headers = new Dictionary<string, string>(),
                    data = null
                };

                // UnityWebRequestException 携带了底层请求，可从中取回真实状态码与结果枚举。
                if (ex is UnityWebRequestException webEx && webEx.UnityWebRequest != null)
                {
                    errorResponse.statusCode = (HttpStatusCode)webEx.UnityWebRequest.responseCode;
                    errorResponse.statusText = webEx.UnityWebRequest.result.ToString();
                }

                // 用实例事件而不是 Instance.OnError：即使单例已被 Destroy，这里也不会再抛 NullReferenceException。
                OnError?.Invoke(ex.Message);
                return errorResponse;
            }

            // 7. 构造响应容器；非 Success 结果直接返回（不执行响应拦截器）。
            var response = new Response<R>
            {
                statusCode = (HttpStatusCode)uwr.responseCode,
                statusText = uwr.result.ToString(),
                headers = new Dictionary<string, string>()
            };

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                OnError?.Invoke(uwr.error);
                return response;
            }

            // 8. 成功响应：保留原始文本并反序列化为 R。
            string responseText = uwr.downloadHandler.text;
            response.RawText = responseText;
            response.data = DeserializeBody<R>(responseText);

            // 9. 响应拦截器：其修改会回写到最终响应（见方法注释）。
            ApplyResponseInterceptors(response, responseText);

            return response;
        }

        /// <summary>
        /// 把响应文本反序列化为 <typeparamref name="R"/>。
        /// R 为 string 时直接返回原文；反序列化失败时触发 <see cref="OnError"/> 并返回 null，不抛异常。
        /// </summary>
        /// <typeparam name="R">目标类型。</typeparam>
        /// <param name="text">响应文本，可能为 null。</param>
        private R DeserializeBody<R>(string text) where R : class
        {
            // 目标类型就是 string 时无需 JSON 解析，直接返回原始文本。
            if (typeof(R) == typeof(string))
                return (R)(object)text;

            try
            {
                return JsonConvert.DeserializeObject<R>(text);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"JSON 反序列化失败: {ex.Message}\n响应文本: {text}");
                return null;
            }
        }

        /// <summary>
        /// 执行响应拦截器链，并把拦截器对响应所做的修改回写到最终响应对象上。
        /// 拦截器看到的是"原始文本形式"的响应：修改 statusCode / statusText / headers 会直接生效；
        /// 修改 data（响应文本）时会按新文本重新反序列化，并同步刷新 RawText，保证两者一致。
        /// </summary>
        /// <typeparam name="R">最终响应体的目标类型。</typeparam>
        /// <param name="response">待回写的最终响应对象。</param>
        /// <param name="originalText">反序列化时使用的原始响应文本，用于判断拦截器是否改写了响应体。</param>
        private void ApplyResponseInterceptors<R>(Response<R> response, string originalText) where R : class
        {
            // 没有拦截器时不做任何包装，避免无谓的分配。
            if (responseInterceptors.Count == 0) return;

            var raw = new Response<string>
            {
                data = originalText,
                statusCode = response.statusCode,
                statusText = response.statusText,
                headers = response.headers,
                RawText = originalText
            };

            foreach (var interceptor in responseInterceptors)
            {
                if (interceptor == null) continue;
                // 拦截器返回 null 时保留上一步结果，保证链路上的修改不会丢失。
                raw = interceptor.Invoke(raw) ?? raw;
            }

            // 回写状态码、状态描述与响应头（headers 为 null 时补一个空字典，避免调用方拿到 null）。
            response.statusCode = raw.statusCode;
            response.statusText = raw.statusText;
            response.headers = raw.headers ?? new Dictionary<string, string>();

            // 响应文本被改写时重新反序列化，使 data、RawText 与拦截器结果保持一致。
            if (!string.Equals(raw.data, originalText, StringComparison.Ordinal))
            {
                response.RawText = raw.data;
                response.data = string.IsNullOrEmpty(raw.data) ? null : DeserializeBody<R>(raw.data);
            }
        }

        // ======================== 静态便捷入口（向后兼容） ========================

        /// <summary>
        /// 核心异步请求（静态入口，内部转发到当前单例的实例管线）。
        /// 保留该静态入口是为了兼容既有调用方；第三方包发请求请用 <see cref="BridgeHub"/>。
        /// 行为说明：解析端点键 → 拼接 baseUrl → 合并 headers → 执行拦截器链 → 发送 → 反序列化。
        /// 该方法不会向外抛出网络异常，所有失败都以 <see cref="Response{T}"/> 形式返回。
        /// </summary>
        /// <typeparam name="R">响应体反序列化目标类型；传 string 时直接返回原始响应文本。</typeparam>
        /// <param name="request">请求参数（url 与 headers 会被就地补全）。</param>
        public static UniTask<Response<R>> SendAsync<R>(Request request) where R : class
            => Instance.SendCoreAsync<R>(request);

        /// <summary>GET 请求（使用默认端点）。</summary>
        public static UniTask<Response<R>> GetAsync<R>(string url, Dictionary<string, string> headers = null,
            int timeoutSeconds = 30, CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "GET", null, headers, timeoutSeconds, cancellationToken);
            return Instance.SendCoreAsync<R>(req);
        }

        /// <summary>GET 请求（指定端点）。</summary>
        public static UniTask<Response<R>> GetAsync<R>(string url, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "GET", null, headers, timeoutSeconds, cancellationToken, endpointKey);
            return Instance.SendCoreAsync<R>(req);
        }

        /// <summary>POST 请求（使用默认端点）。</summary>
        public static UniTask<Response<R>> PostAsync<R>(string url, string bodyJson,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "POST", bodyJson, headers, timeoutSeconds, cancellationToken);
            return Instance.SendCoreAsync<R>(req);
        }

        /// <summary>POST 请求（指定端点）。</summary>
        public static UniTask<Response<R>> PostAsync<R>(string url, string bodyJson, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "POST", bodyJson, headers, timeoutSeconds, cancellationToken, endpointKey);
            return Instance.SendCoreAsync<R>(req);
        }

        /// <summary>PUT 请求（使用默认端点）。</summary>
        public static UniTask<Response<R>> PutAsync<R>(string url, string bodyJson,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "PUT", bodyJson, headers, timeoutSeconds, cancellationToken);
            return Instance.SendCoreAsync<R>(req);
        }

        /// <summary>PUT 请求（指定端点）。</summary>
        public static UniTask<Response<R>> PutAsync<R>(string url, string bodyJson, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "PUT", bodyJson, headers, timeoutSeconds, cancellationToken, endpointKey);
            return Instance.SendCoreAsync<R>(req);
        }

        /// <summary>DELETE 请求（使用默认端点）。</summary>
        public static UniTask<Response<R>> DeleteAsync<R>(string url, Dictionary<string, string> headers = null,
            int timeoutSeconds = 30, CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "DELETE", null, headers, timeoutSeconds, cancellationToken);
            return Instance.SendCoreAsync<R>(req);
        }

        /// <summary>DELETE 请求（指定端点）。</summary>
        public static UniTask<Response<R>> DeleteAsync<R>(string url, string endpointKey,
            Dictionary<string, string> headers = null, int timeoutSeconds = 30,
            CancellationToken cancellationToken = default) where R : class
        {
            var req = BridgeRequestFactory.Create(url, "DELETE", null, headers, timeoutSeconds, cancellationToken, endpointKey);
            return Instance.SendCoreAsync<R>(req);
        }
    }
}
