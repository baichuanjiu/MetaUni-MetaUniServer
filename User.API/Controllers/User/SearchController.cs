using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using User.API.DataContext.User;
using User.API.Filters;
using User.API.ReusableClass;

namespace User.API.Controllers.User
{
    public class BriefUserSearchResultData
    {
        public BriefUserSearchResultData(int UUID, string avatar, string nickname)
        {
            this.UUID = UUID;
            Avatar = avatar;
            Nickname = nickname;
        }

        public int UUID { get; set; }
        public string Avatar { get; set; }
        public string Nickname { get; set; }
    }

    public class SearchUserResponseData {
        public SearchUserResponseData(List<BriefUserSearchResultData> searchResult)
        {
            SearchResult = searchResult;
        }

        public List<BriefUserSearchResultData> SearchResult { get; set; }
    }

    [ApiController]
    [Route("/user/search")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class SearchController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly ILogger<SearchController> _logger;

        public SearchController(UserContext userContext, ILogger<SearchController> logger)
        {
            _userContext = userContext;
            _logger = logger;
        }

        [HttpGet("{searchKey}")]
        public async Task<IActionResult> SearchUser(int searchKey, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //先想想，要支持哪些搜索
            //UUID / Account
            //支持模糊搜索吗？
            //建议不要支持
            //搜索是为了？ 加好友
            //搜索返回 UUID Avatar Nickname 列表即可
            //前端点击后 调用 /profile/{queryUUID} 接口
            //前端可以在 个人资料 页发送好友请求（如果不是好友的话）
            //然后走正常流程
            //发送请求 => 等待确认或拒绝 => 返回结果

            List<BriefUserSearchResultData> searchResult = await _userContext.UserProfiles
                .Select(profile => new {profile.UUID,profile.Account,profile.Avatar,profile.Nickname})
                .Where(profile => profile.UUID == searchKey || profile.Account == searchKey.ToString())
                .Select(profile => new BriefUserSearchResultData(profile.UUID,profile.Avatar,profile.Nickname))
                .ToListAsync();

            if (searchResult.Count == 0)
            {
                ResponseT<string> getSearchResultSucceed = new(2, "未找到符合条件的结果");
                return Ok(getSearchResultSucceed);
            }
            else 
            {
                SearchUserResponseData searchUserResponseData = new(searchResult);
                ResponseT<SearchUserResponseData> getSearchResultSucceed = new(0, "查询成功", searchUserResponseData);
                return Ok(getSearchResultSucceed);
            }
        }
    }
}
