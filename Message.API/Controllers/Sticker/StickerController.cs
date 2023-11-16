using Message.API.Filters;
using Message.API.MongoDBServices;
using Message.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;

namespace Message.API.Controllers.Sticker
{
    public class GetStickerSeriesResponseData 
    {
        public GetStickerSeriesResponseData(List<StickerSeriesDataForClient> dataList)
        {
            DataList = dataList;
        }

        public List<StickerSeriesDataForClient> DataList { get; set; }
    }

    public class GetStickersByRangeResponseData 
    {
        public GetStickersByRangeResponseData(List<Models.Sticker> dataList)
        {
            DataList = dataList;
        }

        public List<Models.Sticker> DataList { get; set; }
    }

    [ApiController]
    [Route("/sticker")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class StickerController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly StickerSeriesService _stickerSeriesService;
        private readonly ILogger<StickerController> _logger;

        public StickerController(IConfiguration configuration, StickerSeriesService stickerSeriesService, ILogger<StickerController> logger)
        {
            _configuration = configuration;
            _stickerSeriesService = stickerSeriesService;
            _logger = logger;
        }

        [HttpGet("urlPrefix")]
        public IActionResult GetStickerUrlPrefix([FromHeader] string JWT, [FromHeader] int UUID) 
        {
            return Ok(new ResponseT<string>(0, "获取成功", _configuration["StickerUrlPrefix"]));
        }

        [HttpGet("series")]
        public async Task<IActionResult> GetStickerSeries([FromHeader] string JWT, [FromHeader] int UUID)
        {
            return Ok(new ResponseT<GetStickerSeriesResponseData>(0, "获取成功", new(await _stickerSeriesService.GetAllStickerSeries())));
        }

        [HttpGet("{seriesId}/{start}&{count}")]
        public IActionResult GetStickersByRange([FromRoute] string seriesId, [FromRoute] int start, [FromRoute] int count,[FromHeader] string JWT, [FromHeader] int UUID)
        {
            return Ok(new ResponseT<GetStickersByRangeResponseData>(0, "获取成功", new(_stickerSeriesService.GetStickersByRange(seriesId,start,count))));
        }
    }
}
