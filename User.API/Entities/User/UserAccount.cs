using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.User
{
    [Table("UserAccount")]
    public class UserAccount
    {
        [Key]
        public int UUID { get; set; } //UUID，用户唯一标识
        [MaxLength(10)]
        public string Account { get; set; } //账号，一般为学号或工号
        [MaxLength(32)]
        public string Password { get; set; } //密码，MD5加密，加密后为大写32位
    }
}
