using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.Friend
{
    [Table("Friendship")]
    [Index(nameof(UUID))]
    [Index(nameof(FriendId))]
    public class Friendship
    {
        public Friendship(int id, int UUID, int friendsGroupId, int friendId, DateTime shipCreatedTime, string? remark, bool isFocus, bool isDeleted, DateTime updatedTime)
        {
            Id = id;
            this.UUID = UUID;
            FriendsGroupId = friendsGroupId;
            FriendId = friendId;
            ShipCreatedTime = shipCreatedTime;
            Remark = remark;
            IsFocus = isFocus;
            IsDeleted = isDeleted;
            UpdatedTime = updatedTime;
        }

        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //逻辑外键，与UserAccount表关联，标识这条FriendShip属于谁，主体的UUID
        public int FriendsGroupId { get; set; } //逻辑外键，与FriendsGroup表关联，标识这条Friendship被主体放在哪个好友分组内
        public int FriendId { get; set; } //标识谁是主体的Friend，客体的UUID
        public DateTime ShipCreatedTime { get; set; } //成为好友的时间
        [MaxLength(15)]
        public string? Remark { get; set; } //主体对客体的备注名，可以为空
        public bool IsFocus { get; set; } = false; //主体是否将客体设置为特别关心
        public bool IsDeleted { get; set; } = false; //这条好友关系是否已被用户删除
        public DateTime UpdatedTime { get; set; } //最后更新时间（包含从删除状态中恢复，即删除后又重新成为好友），用于实现增量更新
    }
}
