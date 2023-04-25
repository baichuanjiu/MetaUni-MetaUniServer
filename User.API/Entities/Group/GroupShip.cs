using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.Group
{
    [Table("GroupShip")]
    [Index(nameof(GroupId))]
    [Index(nameof(UUID))]
    public class GroupShip
    {
        [Key]
        public int Id { get; set; } //主键
        public int GroupId { get; set; } //逻辑外键，与Group表关联，群号，群组的唯一标识
        public int UUID { get; set; } //逻辑外键，与UserAccount表关联，标识这条GroupShip属于谁，主体的UUID
        public DateTime GroupJoinTime { get; set; } //主体的入群时间
        [MaxLength(13)]
        public string Role { get; set; } //标识主体在该群组内的角色，Leader、Administrator、Member
        public bool DoNotDisturbMode { get; set; } = false;  //主体是否对该群组开启免打扰模式
        [MaxLength(10)]
        public string? NicknameInGroup { get; set; } //主体在该群组内的群昵称
        public int SequenceWhenJoined { get; set; } //主体在加入该群组时，群组内的最新一条消息的Sequence，用于消息对齐
        public int Sequence { get; set; } //主体在该群组内获取到的最新一条消息的Sequence，用于消息对齐

    }
}
