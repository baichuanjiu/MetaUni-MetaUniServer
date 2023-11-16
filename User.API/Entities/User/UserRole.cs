using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.User
{
    [Table("UserRole")]
    [Index(nameof(UUID))]
    public class UserRole
    {
        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //逻辑外键，与UserAccount表关联
        public string Role { get; set; } //角色
    }
}
