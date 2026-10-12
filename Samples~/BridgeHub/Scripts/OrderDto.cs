namespace VoyageForge.Bridge.Sample.Hub
{
    /// <summary>
    /// "订单"响应模型。
    /// 示例借用 jsonplaceholder 的 /posts/{id} 接口来充当后端数据，字段与它返回的 JSON 对齐。
    /// </summary>
    public sealed class OrderDto
    {
        /// <summary>归属用户 id。</summary>
        public int userId { get; set; }

        /// <summary>记录 id。</summary>
        public int id { get; set; }

        /// <summary>标题。</summary>
        public string title { get; set; }

        /// <summary>正文。</summary>
        public string body { get; set; }
    }
}
