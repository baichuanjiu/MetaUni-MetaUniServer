using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.User
{
    //用户同步表，用于记录用户对于某些需要同步的数据的最后更新时间，以及最后收到的普通消息与系统消息的Sequence（群组消息的Sequence由GroupShip进行维护）
    [Table("UserSyncTable")]
    [Index(nameof(UUID))]
    public class UserSyncTable
    {
        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //逻辑外键，与UserAccount表关联
        public int SequenceForCommonMessages { get; set; } //用户最新一条CommonMessage的Sequence
        public int SequenceForSystemMessages { get; set; } //用户最新一条SystemMessage的Sequence
        public DateTime UpdatedTimeForFriendsGroups { get; set; } //用户的好友分组的最后一次更新时间
        public DateTime UpdatedTimeForFriendships { get; set; } //用户的好友关系的最后一次更新时间
        public DateTime UpdatedTimeForChats { get; set; } //用户的会话列表的最后一次更新（新建（包含从删除状态中恢复）、置顶、删除、消息更新）时间
        public DateTime LastSyncTimeForCommonChatStatuses { get; set; } //用户最后一次对CommonChatStatu进行同步的时间
        public DateTime LastSyncTimeForFriendsBriefInformation { get; set; } //用户最后一次对好友的信息（网名、头像）进行同步的时间
        public DateTime LastSyncTimeForSystemPromotionInformation { get; set; } //用户最后一次对系统推送的信息（名称、头像）进行同步的时间
    }
}
