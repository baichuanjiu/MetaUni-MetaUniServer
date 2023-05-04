using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Message.API.Entities.Chat
{
    [Table("CommonChatStatus")]
    [Index(nameof(UUID))]
    [Index(nameof(ChatId))]
    [Index(nameof(TargetUserChatId))]
    public class CommonChatStatus
    {
        public CommonChatStatus(int id, int UUID, int chatId, int targetUserChatId, int? lastMessageSendByMe, bool? isRead, DateTime? readTime, DateTime updatedTime)
        {
            Id = id;
            this.UUID = UUID;
            ChatId = chatId;
            TargetUserChatId = targetUserChatId;
            LastMessageSendByMe = lastMessageSendByMe;
            IsRead = isRead;
            ReadTime = readTime;
            UpdatedTime = updatedTime;
        }

        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //标识这条Status记录属于谁
        public int ChatId { get; set; } //外键，与Chat表关联
        public int TargetUserChatId { get; set; } //记录一下对方的ChatId，方便查找
        public int? LastMessageSendByMe { get; set; } //最后一条由我（Chat持有者）发送的消息的MessageId
        public bool? IsRead { get; set; } //对方（Chat中的Target）是否已读我（Chat持有者）发送的最后一条消息
        public DateTime? ReadTime { get; set; } //已读时间
        public DateTime UpdatedTime { get; set; } //状态最后一次更新的时间
    }
}
