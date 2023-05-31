using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using User.API.DataContext.User;
using User.API.Entities.User;
using User.API.Filters;
using User.API.MinIO;
using User.API.ReusableClass;

namespace User.API.Controllers.Profile
{
    public class BaseProfile
    {
        public BaseProfile(int UUID, string account, List<string> roles, string gender, string nickname, string avatar, string campus, string department, string? major, string? grade, DateTime updatedTime)
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
            UpdatedTime = updatedTime;
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
        public DateTime UpdatedTime { get; set; } //个人资料更新时间
    }

    public class FriendProfile
    {
        public FriendProfile(int UUID, string account, List<string> roles, string gender, string nickname, string? remark, string avatar, int friendsGroupId, string campus, string department, string? major, string? grade, DateTime updatedTime)
        {
            this.UUID = UUID;
            Account = account;
            Roles = roles;
            Gender = gender;
            Nickname = nickname;
            Remark = remark;
            Avatar = avatar;
            FriendsGroupId = friendsGroupId;
            Campus = campus;
            Department = department;
            Major = major;
            Grade = grade;
            UpdatedTime = updatedTime;
        }

        public int UUID { get; set; } //UUID，用户唯一标识
        public string Account { get; set; } //账号，一般为学号或工号
        public List<string> Roles { get; set; } //角色，字符串数组，学生、教师、管理员等
        public string Gender { get; set; } //性别
        public string Nickname { get; set; } //昵称，用户自定义网名
        public string? Remark { get; set; } //备注
        public string Avatar { get; set; } //头像，资源存储地址
        public int FriendsGroupId { get; set; } //所在好友分组的好友分组Id
        public string Campus { get; set; } //所在校区
        public string Department { get; set; } //所属院系
        public string? Major { get; set; } //所属专业
        public string? Grade { get; set; } //年级，可以为空
        public DateTime UpdatedTime { get; set; } //个人资料更新时间
    }

    public class GetProfileResponseData<T>
    {
        public GetProfileResponseData(int profileType, T profile)
        {
            ProfileType = ((ProfileTypeEnum)profileType).ToString();
            Profile = profile;
        }

        public enum ProfileTypeEnum { me, friend, stranger };
        public string ProfileType { get; set; }
        public T Profile { get; set; }
    }

    public class BriefUserInformation
    {
        public BriefUserInformation(int UUID, string avatar, string nickname, DateTime updatedTime)
        {
            this.UUID = UUID;
            Avatar = avatar;
            Nickname = nickname;
            UpdatedTime = updatedTime;
        }

        public int UUID { get; set; }
        public string Avatar { get; set; }
        public string Nickname { get; set; }
        public DateTime UpdatedTime { get; set; }
    }

    [ApiController]
    [Route("/profile")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class ProfileController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly UserContext _userContext;
        private readonly IDistributedCache _distributedCache;
        private readonly IMinIOService _minIOService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(IConfiguration configuration, UserContext userContext, IDistributedCache distributedCache, IMinIOService minIOService, ILogger<ProfileController> logger)
        {
            _configuration = configuration;
            _userContext = userContext;
            _distributedCache = distributedCache;
            _minIOService = minIOService;
            _logger = logger;
        }

        //获取个人资料
        [HttpGet("{queryUUID}")]
        public async Task<IActionResult> GetProfile(int queryUUID, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //查找数据库
            var targetProfile = await _userContext.UserProfiles.Select(profile => new { profile.UUID, profile.Account, profile.Gender, profile.Nickname, profile.Avatar, profile.Campus, profile.Department, profile.Major, profile.Grade, profile.UpdatedTime }).FirstOrDefaultAsync(profile => profile.UUID == queryUUID);
            if (targetProfile == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]企图查询一个不存在的用户[ {queryUUID} ]的个人信息。", UUID, queryUUID);
                ResponseT<string> getProfileFailed = new(2, "没有找到目标用户的个人信息");
                return Ok(getProfileFailed);
            }
            List<string> roles = await _userContext.UserRoles.Select(role => new { role.UUID ,role.Role}).Where(role => role.UUID == queryUUID).Select(role => role.Role).ToListAsync();
            //在这里判断一轮，查询对象是 自己 / 好友 / 陌生人
            //然后返回不一样的结果
            if (queryUUID == UUID)
            {
                //查询对象是自己
                GetProfileResponseData<BaseProfile> getProfileResponseData = new(0, new BaseProfile(targetProfile.UUID, targetProfile.Account, roles, targetProfile.Gender, targetProfile.Nickname, targetProfile.Avatar, targetProfile.Campus, targetProfile.Department, targetProfile.Major, targetProfile.Grade, targetProfile.UpdatedTime));
                ResponseT<GetProfileResponseData<BaseProfile>> getProfileSucceed = new(0, "获取成功", getProfileResponseData);
                return Ok(getProfileSucceed);
            }
            var friendship = await _userContext.Friendships
                .Select(ship => new { ship.UUID, ship.FriendId, ship.Remark, ship.FriendsGroupId })
                .FirstOrDefaultAsync(ship => ship.UUID == UUID && ship.FriendId == queryUUID);
            if (friendship != null)
            {
                //查询对象是好友
                GetProfileResponseData<FriendProfile> getProfileResponseData = new(1, new FriendProfile(targetProfile.UUID, targetProfile.Account, roles, targetProfile.Gender, targetProfile.Nickname, friendship.Remark, targetProfile.Avatar, friendship.FriendsGroupId, targetProfile.Campus, targetProfile.Department, targetProfile.Major, targetProfile.Grade, targetProfile.UpdatedTime));
                ResponseT<GetProfileResponseData<FriendProfile>> getProfileSucceed = new(0, "获取成功", getProfileResponseData);
                return Ok(getProfileSucceed);
            }
            else
            {
                //查询对象是陌生人
                GetProfileResponseData<BaseProfile> getProfileResponseData = new(2, new BaseProfile(targetProfile.UUID, targetProfile.Account, roles, targetProfile.Gender, targetProfile.Nickname, targetProfile.Avatar, targetProfile.Campus, targetProfile.Department, targetProfile.Major, targetProfile.Grade, targetProfile.UpdatedTime));
                ResponseT<GetProfileResponseData<BaseProfile>> getProfileSucceed = new(0, "获取成功", getProfileResponseData);
                return Ok(getProfileSucceed);
            }
        }

        //获取简要版个人信息
        [HttpGet("brief/{queryUUID}")]
        public async Task<IActionResult> GetBriefUserInformation(int queryUUID, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //优先查找Redis缓存中的数据
            string? briefUserInformationJson = _distributedCache.GetString(queryUUID.ToString()+"BriefUserInfo");
            if (briefUserInformationJson != null)
            {
                BriefUserInformation briefUserInformation = JsonSerializer.Deserialize<BriefUserInformation>(briefUserInformationJson)!;
                ResponseT<BriefUserInformation> getInformationSucceed = new(0, "获取成功", briefUserInformation);
                return Ok(getInformationSucceed);
            }
            else 
            {
                //查找数据库
                var targetInformation = await _userContext.UserProfiles.Select(profile => new { profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime }).FirstOrDefaultAsync(profile => profile.UUID == queryUUID);
                if (targetInformation == null)
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]企图查询一个不存在的用户[ {queryUUID} ]的个人信息。", UUID, queryUUID);
                    ResponseT<string> getInformationFailed = new(2, "没有找到目标用户的个人信息");
                    return Ok(getInformationFailed);
                }

                BriefUserInformation briefUserInformation = new(targetInformation.UUID, targetInformation.Avatar, targetInformation.Nickname, targetInformation.UpdatedTime);

                //往Redis里做缓存
                //设置缓存在Redis中的过期时间
                DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(3));
                options.SetSlidingExpiration(TimeSpan.FromSeconds(60));
                //将数据存入Redis
                await _distributedCache.SetStringAsync(queryUUID.ToString() + "BriefUserInfo", JsonSerializer.Serialize(briefUserInformation), options);

                ResponseT<BriefUserInformation> getInformationSucceed = new(0, "获取成功", briefUserInformation);
                return Ok(getInformationSucceed);
            }
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
