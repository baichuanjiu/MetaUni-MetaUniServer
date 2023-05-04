using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using User.API.DataContext.User;
using User.API.Entities.User;
using User.API.Filters;
using User.API.MinIO;
using User.API.ReusableClass;

namespace User.API.Controllers.Profile
{
    public class GetProfileResponseData
    {
        public GetProfileResponseData(int UUID, string account, List<string> roles, string gender, string nickname, string avatar, string campus, string department, string? major, string? grade)
        {
            this.UUID = UUID;
            Account = account;
            Roles = roles;
            Gender = gender;
            Nickname = nickname;
            Avatar = avatar;
            Campus = campus;
            Department = department;
            Major = major;
            Grade = grade;
        }

        public int UUID { get; set; } //UUID，用户唯一标识
        public string Account { get; set; } //账号，一般为学号或工号
        public List<string> Roles { get; set; } //角色，字符串数组，学生、教师、管理员等
        public string Gender { get; set; } //性别
        public string Nickname { get; set; } //昵称，用户自定义网名
        public string Avatar { get; set; } //头像，资源存储地址
        public string Campus { get; set; } //所在校区
        public string Department { get; set; } //所属院系
        public string? Major { get; set; } //所属专业
        public string? Grade { get; set; } //年级，可以为空
    }

    [ApiController]
    [Route("/profile")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class ProfileController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly UserContext _userContext;
        private readonly IMinIOService _minIOService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(IConfiguration configuration, UserContext userContext, IMinIOService minIOService, ILogger<ProfileController> logger)
        {
            _configuration = configuration;
            _userContext = userContext;
            _minIOService = minIOService;
            _logger = logger;
        }

        //获取个人资料
        //后续应当增加部分返回值：isFriend等
        [HttpGet("{queryUUID}")]
        public async Task<IActionResult> GetProfile(int queryUUID, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //查找数据库
            var targetProfile = await _userContext.UserProfiles.Select(profile => new { profile.UUID, profile.Account, profile.Roles, profile.Gender, profile.Nickname, profile.Avatar, profile.Campus, profile.Department, profile.Major, profile.Grade }).FirstOrDefaultAsync(profile => profile.UUID == queryUUID);
            if (targetProfile == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]企图查询一个不存在的用户[ {queryUUID} ]的个人信息。", UUID, queryUUID);
                ResponseT<string> getProfileFailed = new(2, "没有找到目标用户的个人信息");
                return Ok(getProfileFailed);
            }

            GetProfileResponseData getProfileResponseData = new(targetProfile.UUID, targetProfile.Account, JsonSerializer.Deserialize<List<string>>(targetProfile.Roles)!, targetProfile.Gender, targetProfile.Nickname, targetProfile.Avatar, targetProfile.Campus, targetProfile.Department, targetProfile.Major, targetProfile.Grade);
            ResponseT<GetProfileResponseData> getProfileSucceed = new(0, "获取成功", getProfileResponseData);
            return Ok(getProfileSucceed);
        }

        //用户上传头像
        [HttpPut("avatar")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            string extension = Path.GetExtension(avatar.FileName);
            //记得验证图片格式，防止上传恶意文件

            Stream stream = avatar.OpenReadStream();

            string timeStamp = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString()[..13];

            string fileName = UUID.ToString() + "/" + UUID.ToString() + "_" + timeStamp + extension;

            if (await _minIOService.UploadImageAsync(_configuration["MinIO:UserAvatarBucketName"]!, fileName, stream))
            {
                UserProfile? userprofile = await _userContext.UserProfiles.FirstOrDefaultAsync(profile => profile.UUID == UUID);
                userprofile!.Avatar = _configuration["MinIO:UserAvatarURLPrefix"]! + fileName;
                userprofile.UpdatedTime = DateTime.Now;
                await _userContext.SaveChangesAsync();
                ResponseT<string> uploadAvatarSucceed = new(0, "头像上传成功", userprofile.Avatar);
                return Ok(uploadAvatarSucceed);
            }
            else
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]上传头像时发生错误，头像上传失败。", UUID);
                ResponseT<string> uploadAvatarFailed = new(2, "发生错误，头像上传失败");
                return Ok(uploadAvatarFailed);
            }
        }

    }
}
