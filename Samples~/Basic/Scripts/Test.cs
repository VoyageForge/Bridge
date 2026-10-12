using System;
using System.Threading.Tasks;
using UnityEngine;
using VoyageForge.Bridge.Runtime;

namespace VoyageForge.Bridge.Sample
{
    public class Test : MonoBehaviour
    {
        private async void Start()
        {
            await TestDefaultEndpoint();
            await TestCustomEndpoint();
            await TestFullUrl();
        }

        private async Task TestDefaultEndpoint()
        {
            Debug.Log("=== Test 1: 默认端点 (不指定 endpointKey) ===");


            var response = await GetUserAsync("1");

            if (response != null && response.IsSuccessStatusCode)
            {
                Debug.Log($"[默认端点] 请求成功: id={response.Data?.id}, name={response.Data?.name}");
            }
            else
            {
                Debug.LogWarning($"[默认端点] 请求失败: {response?.StatusCode}");
            }
        }

        private async Task TestCustomEndpoint()
        {
            Debug.Log("=== Test 2: 自定义端点 (endpointKey = \"webapi\") ===");

            var request = new Request
            {
                url = "get",
                method = "GET",
                endpointKey = "webapi"
            };
            
            var response = await WebClient.SendAsync<HttpBinDto>(request);

            if (response != null && response.IsSuccessStatusCode)
            {
                Debug.Log($"[自定义端点 webapi] 请求成功: url={response.Data?.url}");
            }
            else
            {
                Debug.LogWarning($"[自定义端点 webapi] 请求失败: {response?.StatusCode}");
            }
        }

        private async Task TestFullUrl()
        {
            Debug.Log("=== Test 3: 完整 URL (不拼接 baseUrl) ===");

            var request = new Request
            {
                url = "https://jsonplaceholder.typicode.com/posts/1",
                method = "GET"
            };

            var response = await WebClient.SendAsync<PostDto>(request);

            if (response != null && response.IsSuccessStatusCode)
            {
                Debug.Log($"[完整 URL] 请求成功: id={response.Data?.id}, title={response.Data?.title}");
            }
            else
            {
                Debug.LogWarning($"[完整 URL] 请求失败: {response?.StatusCode}");
            }
        }

        public async Task<Response<UserDto>> GetUserAsync(string userId)
        {
            var request = new Request()
            {
                url = $"/users/{userId}",
                method = "GET",
            };

            return await WebClient.SendAsync<UserDto>(request);
        }
    }


    public class UserDto
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class HttpBinDto
    {
        public string url { get; set; }
    }

    public class PostDto
    {
        public int id { get; set; }
        public string title { get; set; }
    }
}