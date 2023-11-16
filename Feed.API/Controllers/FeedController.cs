using Feed.API.Filters;
using Feed.API.MongoDBServices.Feed;
using Feed.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;

namespace Feed.API.Controllers
{
    public class GetFeedsResponseData 
    {
        public GetFeedsResponseData(List<Models.Feed.Feed> dataList)
        {
            DataList = dataList;
        }

        public List<Models.Feed.Feed> DataList { get; set; }
    }

    [ApiController]
    [Route("/feed")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class FeedController : Controller
    {
        private readonly FeedService _feedService;
        private readonly ILogger<FeedController> _logger;
        private readonly TrendManager.TrendManager _trendManager;

        public FeedController(FeedService feedService, ILogger<FeedController> logger, TrendManager.TrendManager trendManager)
        {
            _feedService = feedService;
            _logger = logger;
            _trendManager = trendManager;
        }

        [HttpGet]
        [Route("{rank}")]
        public async Task<IActionResult> GetFeedsByRank([FromRoute] int rank, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            var feedIdWithTrendValueList = await _trendManager.GetTrendRankWithTrendValueByRange(rank, rank + 20);
            var feeds = await _feedService.GetFeedsByIdListAsync(feedIdWithTrendValueList.feeds);

            GetFeedsResponseData getFeedsResponseData = new(feeds);
            ResponseT<GetFeedsResponseData> getFeedsSucceed = new(0, "获取成功", getFeedsResponseData);
            return Ok(getFeedsSucceed);
        }

        [HttpGet]
        [Route("read/{id}")]
        public IActionResult ReadFeed([FromRoute] string id, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            _ = _trendManager.ReadAction(id, UUID);
            return Ok();
        }
    }
}
