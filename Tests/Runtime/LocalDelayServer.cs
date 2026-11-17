using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VoyageForge.Bridge.Tests
{
    /// <summary>
    /// 测试用的本地最小 HTTP 服务器：收到请求后延迟固定时间再返回 200。
    ///
    /// <para>
    /// 为什么需要它：超时相关用例原先依赖公网 httpbin.org 的 /delay 与 /drip，
    /// 服务端实际耗时受网络与代理影响，导致断言只能"能判定才断言、不能判定就 Inconclusive"，
    /// 测试结果因此不稳定。这里用本机回环地址提供可控延迟，
    /// 让"超时设置是否生效""请求是否被提前掐断"变成确定性断言。
    /// </para>
    ///
    /// <para>
    /// 实现说明：Unity 2022.3 的 .NET Standard 2.1 没有 HttpListener，
    /// 因此直接基于 TcpListener 手写最小 HTTP 响应（只处理 GET，固定 200 + 固定 body）。
    /// </para>
    /// </summary>
    internal sealed class LocalDelayServer : IDisposable
    {
        /// <summary>返回给调用方的固定响应体，方便调用方断言内容。</summary>
        public const string ResponseBody = "{\"ok\":true}";

        private readonly TcpListener _listener;

        /// <summary>内部取消源：Dispose 时用它让后台循环退出。</summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>响应前要等待的毫秒数（模拟慢服务端）。</summary>
        private readonly int _delayMs;

        /// <summary>后台接受连接与处理请求的任务。</summary>
        private readonly Task _loop;

        /// <summary>
        /// 当前仍在处理的连接任务。
        /// Dispose 时等待它们结束，避免端口被系统快速复用后，旧连接还在写响应而干扰后续测试的耗时测量。
        /// </summary>
        private readonly List<Task> _activeConnections = new();

        /// <summary>本服务器监听的端口（随机分配，避免与其它测试冲突）。</summary>
        public int Port { get; }

        /// <summary>可直接传给被测客户端的完整 URL（指向本服务器）。</summary>
        public string Url => $"http://127.0.0.1:{Port}/";

        /// <summary>
        /// 启动一个本地延迟服务器。
        /// </summary>
        /// <param name="delayMs">响应前等待的毫秒数，必须 &gt;= 0。</param>
        public LocalDelayServer(int delayMs)
        {
            if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));

            _delayMs = delayMs;

            // 端口传 0 让系统分配空闲端口，端口冲突时不会让测试互相干扰。
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            _loop = Task.Run(LoopAsync);
        }

        /// <summary>
        /// 后台循环：接受连接 → 读掉请求 → 延迟 → 写回最小 HTTP 响应。
        /// 任何异常都直接结束循环，避免后台线程把异常抛到 Unity 主线程。
        /// </summary>
        private async Task LoopAsync()
        {
            CancellationToken token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    // AcceptTcpClientAsync 不支持取消，Dispose 时靠 Stop() 让它抛异常退出。
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch
                {
                    return; // 监听已停止（Dispose）或发生致命错误
                }

                Task serveTask = Task.Run(() => ServeOneAsync(client, token), token);

                lock (_activeConnections)
                {
                    // 顺带清理已完成的任务，避免列表无限增长。
                    _activeConnections.RemoveAll(item => item.IsCompleted);
                    _activeConnections.Add(serveTask);
                }
            }
        }

        /// <summary>
        /// 处理单个连接：延迟后返回固定 HTTP 响应。
        /// </summary>
        private async Task ServeOneAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                try
                {
                    using NetworkStream stream = client.GetStream();

                    // 读掉请求头（本服务器不解析请求内容，只要求把请求读走避免 RST）。
                    byte[] buffer = new byte[4096];
                    await stream.ReadAsync(buffer, 0, buffer.Length, token);

                    // 模拟"服务端处理很慢"：这一段就是被测客户端超时设置的对照。
                    await Task.Delay(_delayMs, token);

                    byte[] body = Encoding.UTF8.GetBytes(ResponseBody);
                    string head =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: application/json\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Connection: close\r\n\r\n";
                    byte[] headBytes = Encoding.ASCII.GetBytes(head);

                    await stream.WriteAsync(headBytes, 0, headBytes.Length, token);
                    await stream.WriteAsync(body, 0, body.Length, token);
                    await stream.FlushAsync(token);
                }
                catch
                {
                    // 连接被客户端提前断开（例如触发了超时）属于预期情况，忽略。
                }
            }
        }

        /// <summary>
        /// 停止监听并释放资源。
        /// </summary>
        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }

            try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { }

            // 等所有连接任务真正结束：否则端口可能被系统立即复用，
            // 旧连接还会往已关闭的 socket 写响应，从而干扰后续用例的耗时测量。
            Task[] pending;
            lock (_activeConnections)
            {
                pending = _activeConnections.Where(item => !item.IsCompleted).ToArray();
            }

            try { Task.WaitAll(pending, TimeSpan.FromSeconds(3)); } catch { }

            lock (_activeConnections)
            {
                _activeConnections.Clear();
            }

            _cts.Dispose();
        }
    }
}
