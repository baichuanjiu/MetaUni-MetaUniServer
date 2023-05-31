using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MiniApp.API.Filters;
using MiniApp.API.Models.Introduction;
using MiniApp.API.Models.MiniApp;
using MiniApp.API.Models.Review;
using MiniApp.API.MongoDBServices.Introduction;
using MiniApp.API.MongoDBServices.MiniApp;
using MiniApp.API.MongoDBServices.Review;
using MiniApp.API.ReusableClass;
using System.Text.Json;
using System.Text.Json.Serialization;
using User.API.DataContext.User;

namespace MiniApp.API.Controllers
{
    public class MiniAppInformation
    {
        public MiniAppInformation(string id, string type, string name, string avatar, string description, string backgroundImage, double trendingValue)
        {
            Id = id;
            Type = type;
            Name = name;
            Avatar = avatar;
            Description = description;
            BackgroundImage = backgroundImage;
            TrendingValue = trendingValue;
        }

        [JsonPropertyName("_id")]
        public string Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
        public string Description { get; set; }
        public string BackgroundImage { get; set; }
        public double TrendingValue { get; set; }
    }

    public class MiniAppReviewDataForClient{
        public MiniAppReviewDataForClient(MiniAppReview miniAppReview,string nickname)
        {
            Id = miniAppReview.Id;
            MiniAppId = miniAppReview.MiniAppId;
            UUID = miniAppReview.UUID;
            Nickname = nickname;
            Stars = miniAppReview.Stars;
            Title = miniAppReview.Title;
            CreatedTime = miniAppReview.CreatedTime;
            Content = miniAppReview.Content;
        }

        public string? Id { get; set; } //MongoDB中存储的_Id

        public string MiniAppId { get; set; } //标识这条评价数据属于哪个MiniApp

        public int UUID { get; set; } //标识这条评价数据由哪个用户发布

        public string Nickname { get; set; } //用户名

        public int Stars { get; set; } //打分，1-5星

        public string Title { get; set; } //标题

        public DateTime CreatedTime { get; set; } //评价发布时间

        public string Content { get; set; } //正文内容
    }

    public class GetClientAppIntroductionResponse
    {
        public GetClientAppIntroductionResponse(ClientApp clientApp, MiniAppIntroduction miniAppIntroduction, MiniAppReviewDataForClient? latestReview)
        {
            ClientApp = clientApp;
            MiniAppIntroduction = miniAppIntroduction;
            LatestReview = latestReview;
        }

        public ClientApp ClientApp { get; set; }
        public MiniAppIntroduction MiniAppIntroduction { get; set; }
        public MiniAppReviewDataForClient? LatestReview { get; set; }
    }

    public class GetWebAppIntroductionResponse
    {
        public GetWebAppIntroductionResponse(WebApp webApp, MiniAppIntroduction miniAppIntroduction, MiniAppReviewDataForClient? latestReview)
        {
            WebApp = webApp;
            MiniAppIntroduction = miniAppIntroduction;
            LatestReview = latestReview;
        }

        public WebApp WebApp { get; set; }
        public MiniAppIntroduction MiniAppIntroduction { get; set; }

        public MiniAppReviewDataForClient? LatestReview { get; set; }
    }

    [ApiController]
    [Route("/miniApp")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class MiniAppController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly IDistributedCache _distributedCache;
        private readonly MiniAppService _miniAppService;
        private readonly MiniAppIntroductionService _miniAppIntroductionService;
        private readonly MiniAppReviewService _miniAppReviewService;
        private readonly ILogger<MiniAppController> _logger;

        public MiniAppController(UserContext userContext, IDistributedCache distributedCache, MiniAppService miniAppService, MiniAppIntroductionService miniAppIntroductionService, MiniAppReviewService miniAppReviewService, ILogger<MiniAppController> logger)
        {
            _userContext = userContext;
            _distributedCache = distributedCache;
            _miniAppService = miniAppService;
            _miniAppIntroductionService = miniAppIntroductionService;
            _miniAppReviewService = miniAppReviewService;
            _logger = logger;
        }

        [HttpGet]
        [Route("{rank}")]
        public IActionResult GetMiniAppsByRank([FromRoute] int rank, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            string json = _miniAppService.GetMiniAppsByRank(rank);
            List<MiniAppInformation> miniApps = JsonSerializer.Deserialize<List<MiniAppInformation>>(json)!;
            return Ok(new ResponseT<List<MiniAppInformation>>(code: 0, message: "获取成功", data: miniApps));
        }

        [HttpGet]
        [Route("introduction/{type}/{id}")]
        public IActionResult GetIntroductionById([FromRoute] string type, [FromRoute] string id, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            switch (type)
            {
                case "clientApp":
                    {
                        try
                        {
                            ClientApp clientApp = _miniAppService.GetClientAppById(id);
                            if (clientApp == null)
                            {
                                _logger.LogWarning("Warning：用户[ {UUID} ]尝试获取不存在的MiniApp介绍[ {type} ][ {id} ]，可能原因为用户正在尝试绕过前端进行操作。", UUID, type, id);
                                return Ok(new ResponseT<string>(2, "您正在尝试获取不存在的MiniApp介绍"));
                            }
                            MiniAppIntroduction miniAppIntroduction = _miniAppIntroductionService.GetIntroductionById(id);
                            MiniAppReview miniAppReview = _miniAppReviewService.GetLatestById(id);
                            GetClientAppIntroductionResponse response;
                            if (miniAppReview == null)
                            {
                                response = new(clientApp, miniAppIntroduction, null);
                            }
                            else
                            {
                                //优先查找Redis缓存中的数据
                                string? briefUserInformationJson = _distributedCache.GetString(miniAppReview.UUID.ToString() + "BriefUserInfo");
                                if (briefUserInformationJson != null)
                                {
                                    BriefUserInformation briefUserInformation = JsonSerializer.Deserialize<BriefUserInformation>(briefUserInformationJson)!;
                                    string nickname = briefUserInformation.Nickname;
                                    response = new(clientApp, miniAppIntroduction, new MiniAppReviewDataForClient(miniAppReview, nickname));
                                }
                                else 
                                {
                                    //查找数据库
                                    var targetInformation = _userContext.UserProfiles.Select(profile => new { profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime }).FirstOrDefault(profile => profile.UUID == miniAppReview.UUID);
                                    BriefUserInformation briefUserInformation = new(targetInformation!.UUID, targetInformation.Avatar, targetInformation.Nickname, targetInformation.UpdatedTime);

                                    //往Redis里做缓存
                                    //设置缓存在Redis中的过期时间
                                    DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(3));
                                    options.SetSlidingExpiration(TimeSpan.FromSeconds(60));
                                    //将数据存入Redis
                                    _distributedCache.SetString(miniAppReview.UUID.ToString() + "BriefUserInfo", JsonSerializer.Serialize(briefUserInformation), options);

                                    string nickname = briefUserInformation.Nickname;
                                    response = new(clientApp, miniAppIntroduction, new MiniAppReviewDataForClient(miniAppReview, nickname));
                                }
                            }
                            ResponseT<GetClientAppIntroductionResponse> getIntroductionSucceed = new(code: 0, message: "获取成功", data: response);
                            return Ok(getIntroductionSucceed);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Warning：用户[ {UUID} ]尝试获取不存在的MiniApp介绍[ {type} ][ {id} ]，可能原因为用户正在尝试绕过前端进行操作。", UUID, type, id);
                            _logger.LogError("Error：用户[ {UUID} ]尝试获取MiniApp介绍[ {type} ][ {id} ]时接口报错，报错信息为[ {ex} ]，可能原因为用户正在尝试绕过前端进行操作并输入了错误的参数。", UUID, type, id, ex);
                            return Ok(new ResponseT<string>(2, "您正在尝试获取不存在的MiniApp介绍"));
                        }
                    }
                case "webApp":
                    {
                        try
                        {
                            WebApp webApp = _miniAppService.GetWebAppById(id);
                            if (webApp == null)
                            {
                                _logger.LogWarning("Warning：用户[ {UUID} ]尝试获取不存在的MiniApp介绍[ {type} ][ {id} ]，可能原因为用户正在尝试绕过前端进行操作。", UUID, type, id);
                                return Ok(new ResponseT<string>(2, "您正在尝试获取不存在的MiniApp介绍"));
                            }
                            MiniAppIntroduction miniAppIntroduction = _miniAppIntroductionService.GetIntroductionById(id);
                            MiniAppReview miniAppReview = _miniAppReviewService.GetLatestById(id);
                            GetWebAppIntroductionResponse response;
                            if (miniAppReview == null)
                            {
                                response = new(webApp, miniAppIntroduction, null);
                            }
                            else
                            {
                                //优先查找Redis缓存中的数据
                                string? briefUserInformationJson = _distributedCache.GetString(miniAppReview.UUID.ToString() + "BriefUserInfo");
                                if (briefUserInformationJson != null)
                                {
                                    BriefUserInformation briefUserInformation = JsonSerializer.Deserialize<BriefUserInformation>(briefUserInformationJson)!;
                                    string nickname = briefUserInformation.Nickname;
                                    response = new(webApp, miniAppIntroduction, new MiniAppReviewDataForClient(miniAppReview, nickname));
                                }
                                else
                                {
                                    //查找数据库
                                    var targetInformation = _userContext.UserProfiles.Select(profile => new { profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime }).FirstOrDefault(profile => profile.UUID == miniAppReview.UUID);
                                    BriefUserInformation briefUserInformation = new(targetInformation!.UUID, targetInformation.Avatar, targetInformation.Nickname, targetInformation.UpdatedTime);

                                    //往Redis里做缓存
                                    //设置缓存在Redis中的过期时间
                                    DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(3));
                                    options.SetSlidingExpiration(TimeSpan.FromSeconds(60));
                                    //将数据存入Redis
                                    _distributedCache.SetString(miniAppReview.UUID.ToString() + "BriefUserInfo", JsonSerializer.Serialize(briefUserInformation), options);

                                    string nickname = briefUserInformation.Nickname;
                                    response = new(webApp, miniAppIntroduction, new MiniAppReviewDataForClient(miniAppReview, nickname));
                                }
                            }
                            ResponseT<GetWebAppIntroductionResponse> getIntroductionSucceed = new(code: 0, message: "获取成功", data: response);
                            return Ok(getIntroductionSucceed);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Warning：用户[ {UUID} ]尝试获取不存在的MiniApp介绍[ {type} ][ {id} ]，可能原因为用户正在尝试绕过前端进行操作。", UUID, type, id);
                            _logger.LogError("Error：用户[ {UUID} ]尝试获取MiniApp介绍[ {type} ][ {id} ]时接口报错，报错信息为[ {ex} ]，可能原因为用户正在尝试绕过前端进行操作并输入了错误的参数。", UUID, type, id, ex);
                            return Ok(new ResponseT<string>(2, "您正在尝试获取不存在的MiniApp介绍"));
                        }

                    }
                default:
                    {
                        _logger.LogWarning("Warning：用户[ {UUID} ]在获取MiniApp介绍[ {id} ]时输入了错误的参数[ {type} ]，可能原因为用户正在尝试绕过前端进行操作。", UUID, id, type);
                        return Ok(new ResponseT<string>(2, "您正在尝试获取不存在的MiniApp介绍"));
                    }
            }
        }
    }
}
