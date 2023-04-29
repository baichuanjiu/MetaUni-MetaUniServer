using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Message.API.Entities.Chat
{
    [Table("Chat")]
    [Index(nameof(UUID))]
    public class Chat
    {
        public Chat(int id, int UUID, int targetId, bool isWithOtherUser, bool isWithGroup, bool isWithSystem, bool isStickyOnTop, bool isDeleted, int numberOfUnreadMessages, int? lastMessageId, DateTime updatedTime)
        {
            Id = id;
            this.UUID = UUID;
            TargetId = targetId;
            IsWithOtherUser = isWithOtherUser;
            IsWithGroup = isWithGroup;
            IsWithSystem = isWithSystem;
            IsStickyOnTop = isStickyOnTop;
            IsDeleted = isDeleted;
            NumberOfUnreadMessages = numberOfUnreadMessages;
            LastMessageId = lastMessageId;
            UpdatedTime = updatedTime;
        }

        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //标识这条Chat记录属于谁，主体的UUID
        public int TargetId { get; set; } //标识这条Chat记录是主体与谁进行的，客体的UUID，客体可以是OtherUser、Group、System
        public bool IsWithOtherUser { get; set; } = false; //客体是OtherUser
        public bool IsWithGroup { get; set; } = false; //客体是Group
        public bool IsWithSystem { get; set; } = false; //客体是System
        public bool IsStickyOnTop { get; set; } = false; //主体是否将与客体的对话置顶
        public bool IsDeleted { get; set; } = false; //主体是否已将与客体的对话删除（暂时不显示在消息列表中，直到对话中下一条消息产生）
        public int NumberOfUnreadMessages { get; set; } //该会话中主体的未读消息数
        public int? LastMessageId { get; set; } //该会话中最新一条消息的消息Id
        public DateTime UpdatedTime { get; set; } //该会话状态（新建（包含从删除状态中恢复）、置顶、删除、消息更新）最后一次更新的时间
    }
}
