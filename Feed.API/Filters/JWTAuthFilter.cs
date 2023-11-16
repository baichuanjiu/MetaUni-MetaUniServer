using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Feed.API.ReusableClass;

namespace Feed.API.Filters
{
    public class JWTAuthFilterService : IAsyncActionFilter
    {
        //依赖注入
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<JWTAuthFilterService> _logger;

        public JWTAuthFilterService(IDistributedCache distributedCache, ILogger<JWTAuthFilterService> logger)
        {
            _distributedCache = distributedCache;
            _logger = logger;
        }

        async Task IAsyncActionFilter.OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            //验证JWT
            string? currentJWT = await _distributedCache.GetStringAsync(context.ActionArguments["UUID"]!.ToString()!);
            if (currentJWT != context.ActionArguments["JWT"] as string)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在访问[ {controller} ]时使用了无效的JWT。", context.ActionArguments["UUID"]!.ToString()!, context.Controller.ToString());
                ResponseT<string> authorizationFailed = new(1, "使用了无效的JWT，请重新登录");
                context.Result = new ContentResult
                {
                    StatusCode = 200,
                    ContentType = "application/json",
                    Content = JsonSerializer.Serialize(authorizationFailed, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                };
            }
            else
            {
                await next();
            }
        }
    }
}
