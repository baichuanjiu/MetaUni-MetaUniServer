using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace User.API.Entities.User
{
    [Table("UserProfile")]
    [Index(nameof(UUID))]
    [Index(nameof(Account))]
    public class UserProfile
    {
        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //逻辑外键，与UserAccount表关联
        [MaxLength(10)]
        public string Account { get; set; } //账号，一般为学号或工号
        [MaxLength(5)]
        public string Surname { get; set; } //真实姓名中的姓氏
        [MaxLength(5)]
        public string Name { get; set; } //真实姓名中的名
        public string Gender { get; set; } //性别
        [MaxLength(15)]
        public string Nickname { get; set; } //昵称，用户自定义网名
        public string Avatar { get; set; } //头像，资源存储地址
        [MaxLength(10)]
        public string Campus { get; set; } //所在校区
        [MaxLength(20)]
        public string Department { get; set; } //所属院系
        [MaxLength(20)]
        public string? Major { get; set; } //所属专业
        [MaxLength(5)]
        public string? Grade { get; set; } //年级，可以为空
        public DateTime UpdatedTime { get; set; } //个人资料的最后更新时间

    }
}
