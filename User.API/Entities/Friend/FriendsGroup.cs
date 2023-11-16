using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.Friend
{
    [Table("FriendsGroup")]
    [Index(nameof(UUID))]
    public class FriendsGroup
    {
        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //逻辑外键，与UserAccount表关联，标识该好友分组属于谁，主体的UUID
        public int OrderNumber { get; set; } //标识主体对该好友分组的排序
        [MaxLength(10)]
        public string FriendsGroupName { get; set; } //主体对该好友分组的命名
        public bool IsDeleted { get; set; } = false; //这一分组是否已被用户选择删除
        public DateTime UpdatedTime { get; set; } //最后更新时间，用于实现增量更新

    }
}
