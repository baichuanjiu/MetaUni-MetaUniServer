using Microsoft.AspNetCore.Mvc;
using Version.API.Filters;
using Version.API.MongoDBServices.Note;
using Version.API.ReusableClass;

namespace Version.API.Controllers
{
    public class GetAllNotesResponseData 
    {
        public GetAllNotesResponseData(List<Models.Note.Note> dataList)
        {
            DataList = dataList;
        }

        public List<Models.Note.Note> DataList { get; set; }
    }

    [ApiController]
    [Route("/note")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class NoteController : Controller
    {
        private readonly NoteService _noteService;
        private readonly ILogger<NoteController> _logger;

        public NoteController(NoteService noteService, ILogger<NoteController> logger)
        {
            _noteService = noteService;
            _logger = logger;
        }

        [HttpGet("latest")]
        public IActionResult GetLatestNote([FromHeader] string JWT, [FromHeader] int UUID) 
        {
            return Ok(new ResponseT<Models.Note.Note>(0,"获取成功",_noteService.GetLatestNote()));
        }

        [HttpGet("all")]
        public IActionResult GetAllNotes([FromHeader] string JWT, [FromHeader] int UUID)
        {
            return Ok(new ResponseT<GetAllNotesResponseData>(0, "获取成功", new(_noteService.GetAllNotes())));
        }
    }
}
