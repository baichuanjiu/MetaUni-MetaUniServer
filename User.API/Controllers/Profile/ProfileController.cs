using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        public FriendProfile(int UUID, int friendshipId, string account, List<string> roles, string gender, string nickname, string? remark, string avatar, int friendsGroupId, string campus, string department, string? major, string? grade, DateTime updatedTime)
        {
            this.UUID = UUID;
            FriendshipId = friendshipId;
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
        public int FriendshipId { get; set; } //好友记录Id
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

    public class UploadAvatarResponseData 
    {
        public UploadAvatarResponseData(string avatar, DateTime updatedTime)
        {
            Avatar = avatar;
            UpdatedTime = updatedTime;
        }

        public string Avatar { get; set; }
        public DateTime UpdatedTime { get; set; }
    }
    public class EditNicknameResponseData
    {
        public EditNicknameResponseData(string nickname, DateTime updatedTime)
        {
            Nickname = nickname;
            UpdatedTime = updatedTime;
        }

        public string Nickname { get; set; }
        public DateTime UpdatedTime { get; set; }
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
        private readonly UserAvatarMinIOService _userAvatarMinIOService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(IConfiguration configuration, UserContext userContext, IDistributedCache distributedCache, UserAvatarMinIOService userAvatarMinIOService, ILogger<ProfileController> logger)
        {
            _configuration = configuration;
            _userContext = userContext;
            _distributedCache = distributedCache;
            _userAvatarMinIOService = userAvatarMinIOService;
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
            List<string> roles = await _userContext.UserRoles.Select(role => new { role.UUID, role.Role }).Where(role => role.UUID == queryUUID).Select(role => role.Role).ToListAsync();
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
                .Select(ship => new { ship.Id,ship.UUID, ship.FriendId, ship.Remark, ship.FriendsGroupId,ship.IsDeleted })
                .FirstOrDefaultAsync(ship => ship.UUID == UUID && ship.FriendId == queryUUID && !ship.IsDeleted);
            if (friendship != null)
            {
                //查询对象是好友
                GetProfileResponseData<FriendProfile> getProfileResponseData = new(1, new FriendProfile(targetProfile.UUID,friendship.Id, targetProfile.Account, roles, targetProfile.Gender, targetProfile.Nickname, friendship.Remark, targetProfile.Avatar, friendship.FriendsGroupId, targetProfile.Campus, targetProfile.Department, targetProfile.Major, targetProfile.Grade, targetProfile.UpdatedTime));
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
            string? briefUserInformationJson = _distributedCache.GetString(queryUUID.ToString() + "BriefUserInfo");
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
                _ = _distributedCache.SetStringAsync(queryUUID.ToString() + "BriefUserInfo", JsonSerializer.Serialize(briefUserInformation), options);

                ResponseT<BriefUserInformation> getInformationSucceed = new(0, "获取成功", briefUserInformation);
                return Ok(getInformationSucceed);
            }
        }

        //用户上传头像
        [HttpPut("avatar")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //记得验证图片格式，防止上传恶意文件
            if (!avatar.ContentType.Contains("image")) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]上传头像失败，原因为用户上传了图片以外的媒体文件，疑似正绕过前端进行操作。", UUID);
                ResponseT<string> uploadAvatarFailed = new(2, "禁止上传规定格式以外的头像文件");
                return Ok(uploadAvatarFailed);
            }

            string extension = Path.GetExtension(avatar.FileName);

            Stream stream = avatar.OpenReadStream();

            string timestamp = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

            string fileName = UUID.ToString() + "/" + UUID.ToString() + "_" + timestamp + extension;

            if (await _userAvatarMinIOService.UploadImageAsync(fileName, stream))
            {
                UserProfile? userprofile = await _userContext.UserProfiles.FirstOrDefaultAsync(profile => profile.UUID == UUID);
                userprofile!.Avatar = _configuration["MinIO:UserAvatarURLPrefix"]! + fileName;
                DateTime now = DateTime.Now;
                userprofile.UpdatedTime = now;
                await _userContext.SaveChangesAsync();
                ResponseT<UploadAvatarResponseData> uploadAvatarSucceed = new(0, "头像上传成功", new(userprofile.Avatar,now));
                return Ok(uploadAvatarSucceed);
            }
            else
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]上传头像时发生错误，头像上传失败。", UUID);
                ResponseT<string> uploadAvatarFailed = new(3, "发生错误，头像上传失败");
                return Ok(uploadAvatarFailed);
            }
        }

        //用户修改昵称
        [HttpPut("nickname/{nickname}")]
        public async Task<IActionResult> EditNickname([FromRoute] string nickname, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            var check = Regex.Split(nickname, " +").ToList();
            check.RemoveAll(key => key == "");

            if (check.Count == 0) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]修改昵称失败，原因为上传了空的昵称[ {nickname} ]。", UUID,nickname);
                ResponseT<string> editNicknameFailed = new(2, "昵称不可为空");
                return Ok(editNicknameFailed);
            }

            if (nickname.Length > 15) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]修改昵称失败，原因为上传了超过长度限制的昵称[ {nickname} ]。", UUID,nickname);
                ResponseT<string> editNicknameFailed = new(3, "昵称长度超过限制");
                return Ok(editNicknameFailed);
            }

            UserProfile? userprofile = await _userContext.UserProfiles.FirstOrDefaultAsync(profile => profile.UUID == UUID);
            userprofile!.Nickname = nickname;
            DateTime now = DateTime.Now;
            userprofile.UpdatedTime = now;
            await _userContext.SaveChangesAsync();
            ResponseT<EditNicknameResponseData> editNicknameSucceed = new(0, "修改昵称成功",new(nickname,now));
            return Ok(editNicknameSucceed);
        }

    }
}
