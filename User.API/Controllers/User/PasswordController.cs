using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;
using User.API.Controllers.Login;
using User.API.DataContext.User;
using User.API.Filters;
using User.API.ReusableClass;

namespace User.API.Controllers.User
{
    public class ChangePasswordRequestData
    {
        public ChangePasswordRequestData(string newPassword)
        {
            NewPassword = newPassword;
        }

        public string NewPassword { get; set; }
    }

    [ApiController]
    [Route("/user/password")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class PasswordController : Controller
    {
        //依赖注入
        private readonly UserContext _userContext;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<PasswordController> _logger;

        public PasswordController(UserContext userContext, IDistributedCache distributedCache, ILogger<PasswordController> logger)
        {
            _userContext = userContext;
            _distributedCache = distributedCache;
            _logger = logger;
        }

        [HttpGet("rsaPublicKey")]
        public async Task<IActionResult> GetRSAPublicKey([FromHeader] string JWT, [FromHeader] int UUID)
        {
            //生成RSA公钥和私钥（PKCS#1）
            RSACryptoServiceProvider rsaProvider = new(2048);
            string publicKey = rsaProvider.ExportRSAPublicKeyPem();
            string privateKey = rsaProvider.ExportRSAPrivateKeyPem();

            //设置RSA私钥在Redis中的过期时间
            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
            options.SetSlidingExpiration(TimeSpan.FromMinutes(5));

            //将RSA私钥存入Redis
            await _distributedCache.SetStringAsync(UUID.ToString() + "RSAPrivateKeyUsedToChangePassword", privateKey, options);
            return Ok(new ResponseT<string>(0,"RSA公钥有效时间为5分钟",publicKey));
        }

        [HttpPut]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestData requestData,[FromHeader] string JWT, [FromHeader] int UUID) 
        {
            //从Redis中获取RSA私钥
            string? rsaPrivateKey = await _distributedCache.GetStringAsync(UUID + "RSAPrivateKeyUsedToChangePassword");
            if (rsaPrivateKey == null)
            {
                _logger.LogWarning("Warning：用户[ {uuid} ]修改密码时无法获取RSA私钥，可能原因为修改密码耗时过久，RSA私钥已过期，或用户正在尝试绕过前端进行操作。", UUID);
                return Ok(new ResponseT<string>(2, "修改密码超时，请重新尝试"));
            }

            //将加密后的密码转化为byte[]
            byte[] byteRSAPassword = Encoding.Unicode.GetBytes(requestData.NewPassword).Where((item, index) => index % 2 == 0).ToArray();

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
                _logger.LogError("Error：用户[ {uuid} ]修改密码时使用了错误的RSA加密结果，导致无法解密，用户可能正在尝试绕过前端进行操作。报错信息为[ {ex} ]。", UUID, ex);
            }

            //对密码进行MD5加密
            byte[] byteMD5Password = MD5.HashData(bytePassword);
            StringBuilder builder = new();
            foreach (var item in byteMD5Password)
            {
                builder.Append(item.ToString("X2"));
            }
            string MD5Password = builder.ToString();

            var userAccount = await _userContext.UserAccounts.FindAsync(UUID);
            userAccount!.Password = MD5Password;
            _userContext.SaveChanges();

            //从Redis中删除RSA私钥
            _ = _distributedCache.RemoveAsync(UUID + "RSAPrivateKeyUsedToChangePassword");
            //从Redis中删除JWT
            _ = _distributedCache.RemoveAsync(UUID.ToString());

            return Ok(new ResponseT<string>(0, "修改密码成功，请重新登录"));
        }
    }
}
