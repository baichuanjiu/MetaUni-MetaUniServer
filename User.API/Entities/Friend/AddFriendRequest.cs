using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.Friend
{
    [Table("AddFriendRequest")]
    [Index(nameof(TargetId))]
    public class AddFriendRequest
    {
        public AddFriendRequest(int id, int UUID, int targetId, string? message, string? remark, int friendsGroupId, bool isPending)
        {
            Id = id;
            this.UUID = UUID;
            TargetId = targetId;
            Message = message;
            Remark = remark;
            FriendsGroupId = friendsGroupId;
            IsPending = isPending;
        }

        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //请求者的UUID
        public int TargetId { get; set; } //目标用户的UUID
        [MaxLength(30)]
        public string? Message { get; set; } //请求者填写的验证信息
        [MaxLength(15)]
        public string? Remark { get; set; } //请求者填写的备注名
        public int FriendsGroupId { get; set; } //请求者填写的好友分组Id
        public bool IsPending { get; set; } = false; //是否已被处理（已被同意或拒绝）
    }
}
