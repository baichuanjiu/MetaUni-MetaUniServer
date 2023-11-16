using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using User.API.DataContext.User;
using User.API.ReusableClass;

namespace User.API.Controllers.Login
{
    public class LoginForm
    {
        public LoginForm(string account, string password)
        {
            Account = account;
            Password = password;
        }

        public string Account { get; set; }
        public string Password { get; set; }
    }
    public class LoginResponseData
    {
        public LoginResponseData(string JWT, int UUID, string avatar, string nickname, DateTime updatedTime)
        {
            this.JWT = JWT;
            this.UUID = UUID;
            Avatar = avatar;
            Nickname = nickname;
            UpdatedTime = updatedTime;
        }

        public string JWT { get; set; }
        public int UUID { get; set; }
        public string Avatar { get; set; }
        public string Nickname { get; set; }
        public DateTime UpdatedTime { get; set; }
    }
    public class CheckAccountResponseData
    {
        public CheckAccountResponseData(string account, string avatar, string privateNickname, string RSAPublicKey)
        {
            Account = account;
            Avatar = avatar;
            PrivateNickname = privateNickname;
            this.RSAPublicKey = RSAPublicKey;
        }

        public string Account { get; set; }
        public string Avatar { get; set; }
        public string PrivateNickname { get; set; }
        public string RSAPublicKey { get; set; }
    }

    [ApiController]
    [Route("/login")]
    public class LoginController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<LoginController> _logger;

        public LoginController(UserContext userContext, IConfiguration configuration, IDistributedCache distributedCache, ILogger<LoginController> logger)
        {
            _userContext = userContext;
            _configuration = configuration;
            _distributedCache = distributedCache;
            _logger = logger;
        }

        //用户登录
        [HttpPost]
        public async Task<IActionResult> Login(LoginForm loginForm)
        {
            //查找数据库
            var targetAccount = await _userContext.UserAccounts.Select(account => new { account.Account, account.Password }).FirstOrDefaultAsync(account => account.Account == loginForm.Account);
            if (targetAccount != null)
            {
                //从Redis中获取RSA私钥
                string? rsaPrivateKey = await _distributedCache.GetStringAsync(loginForm.Account + "RSAPrivateKey");
                if (rsaPrivateKey == null)
                {
                    _logger.LogWarning("Warning：账号[ {account} ]登录时无法获取RSA私钥，可能原因为登录耗时过久，RSA私钥已过期，或用户正在尝试绕过前端进行操作。", loginForm.Account);
                    return Ok(new ResponseT<string>(1, "登录超时，请重新尝试"));
                }

                //将加密后的密码转化为byte[]
                byte[] byteRSAPassword = Encoding.Unicode.GetBytes(loginForm.Password).Where((item, index) => index % 2 == 0).ToArray();

                //使用RSA私钥进行解密
                RSACryptoServiceProvider rsaProvider = new(2048);
                rsaProvider.ImportFromPem(rsaPrivateKey);

                byte[] bytePassword = Array.Empty<byte>();
                try
                {
                    bytePassword = rsaProvider.Decrypt(byteRSAPassword, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error：账号[ {account} ]登录时使用了错误的RSA加密结果，导致无法解密，可能同时存在多个终端尝试使用此账号进行登录，或用户正在尝试绕过前端进行操作。报错信息为[ {ex} ]。", loginForm.Account, ex);
                }

                //对密码进行MD5加密
                byte[] byteMD5Password = MD5.HashData(bytePassword);
                StringBuilder builder = new();
                foreach (var item in byteMD5Password)
                {
                    builder.Append(item.ToString("X2"));
                }
                string MD5Password = builder.ToString();

                if (targetAccount.Password == MD5Password)
                {
                    //从Redis中删除RSA私钥
                    await _distributedCache.RemoveAsync(loginForm.Account + "RSAPrivateKey");

                    var targetProfile = await _userContext.UserProfiles.Select(profile => new { profile.Account, profile.UUID, profile.Avatar, profile.Nickname, profile.UpdatedTime }).FirstOrDefaultAsync(profile => profile.Account == loginForm.Account);
                    if (targetProfile != null)
                    {
                        //生成JWT
                        List<Claim> claims = new();
                        int UUID = targetProfile.UUID;
                        claims.Add(new Claim("UUID", UUID.ToString()));
                        string key = _configuration["JWT:Key"]!;
                        DateTime expires = DateTime.Now.AddDays(365);
                        byte[] secBytes = Encoding.Unicode.GetBytes(key);
                        SymmetricSecurityKey secKey = new(secBytes);
                        SigningCredentials credentials = new(secKey, SecurityAlgorithms.HmacSha256Signature);
                        JwtSecurityToken tokenDescriptor = new(claims: claims, expires: expires, signingCredentials: credentials);
                        string JWT = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

                        //设置JWT在Redis中的过期时间
                        DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromDays(365));
                        options.SetSlidingExpiration(TimeSpan.FromDays(30));

                        //将JWT存入Redis
                        await _distributedCache.SetStringAsync(UUID.ToString(), JWT, options);

                        ResponseT<LoginResponseData> loginSucceed = new(0, "登录成功", new LoginResponseData(JWT, UUID, targetProfile.Avatar, targetProfile.Nickname, targetProfile.UpdatedTime));

                        return Ok(loginSucceed);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Warning：账号[ {account} ]登录时使用了不存在的账号，可能原因为用户正在尝试绕过前端进行操作。", loginForm.Account);
            }

            ResponseT<string> loginFailed = new(2, "账号或密码错误");
            return Ok(loginFailed);
        }

        //检查登录状态
        [HttpGet("check")]
        public async Task<IActionResult> CheckLoginStatus([FromHeader] string JWT, [FromHeader] int UUID)
        {
            string? CurrentJWT = await _distributedCache.GetStringAsync(UUID.ToString());
            if (CurrentJWT == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]检查登录状态时，Redis中不存在此用户的Token，可能原因为用户Token已过期，或用户正在尝试绕过前端进行操作。", UUID);
                ResponseT<string> invalidLoginStatus = new(1, "用户Token已过期，请重新登录");
                return Ok(invalidLoginStatus);
            }
            else if (CurrentJWT != JWT)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]检查登录状态时，Token不匹配，可能原因为该账号已在另一台设备上登录，或用户正在尝试绕过前端进行操作。", UUID);
                ResponseT<string> invalidLoginStatus = new(2, "账号在另外一台设备登录，若并非您的操作，请及时联系客服反馈情况");
                return Ok(invalidLoginStatus);
            }
            else
            {
                ResponseT<string> validLoginStatus = new(0, "该Token有效");
                return Ok(validLoginStatus);
            }
        }

        //查找数据库中是否存在该账号
        [HttpGet("check/{checkAccount}")]
        public async Task<IActionResult> CheckAccount(string checkAccount)
        {
            //查找数据库
            var targetAccount = await _userContext.UserAccounts.Select(account => account.Account).FirstOrDefaultAsync(account => account == checkAccount);
            if (targetAccount != null)
            {
                var targetProfile = await _userContext.UserProfiles.Select(profile => new { profile.Account, profile.Avatar, profile.Nickname }).FirstOrDefaultAsync(profile => profile.Account == checkAccount);
                if (targetProfile != null)
                {
                    string nickname = targetProfile.Nickname;
                    string privateNickname;
                    if (nickname.Length == 1)
                    {
                        privateNickname = "*";
                    }
                    else if (nickname.Length == 2)
                    {
                        privateNickname = nickname[0] + "*";
                    }
                    else
                    {
                        privateNickname = nickname[..1];
                        for (int i = 1; i < nickname.Length - 1; i++)
                        {
                            privateNickname += "*";
                        }
                        privateNickname += nickname[^1];
                    }

                    //生成RSA公钥和私钥（PKCS#1）
                    RSACryptoServiceProvider rsaProvider = new(2048);
                    string publicKey = rsaProvider.ExportRSAPublicKeyPem();
                    string privateKey = rsaProvider.ExportRSAPrivateKeyPem();

                    //设置RSA私钥在Redis中的过期时间
                    DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                    options.SetSlidingExpiration(TimeSpan.FromMinutes(5));

                    //将RSA私钥存入Redis
                    await _distributedCache.SetStringAsync(checkAccount + "RSAPrivateKey", privateKey, options);

                    ResponseT<CheckAccountResponseData> checkSucceed = new(0, "该账号有效", new CheckAccountResponseData(checkAccount, targetProfile.Avatar, privateNickname, publicKey));
                    return Ok(checkSucceed);
                }
            }
            ResponseT<string> checkFailed = new(1, "该账号不存在");
            return Ok(checkFailed);
        }
    }
}
