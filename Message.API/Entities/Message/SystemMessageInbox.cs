using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Message.API.Entities.Message
{
    [Table("SystemMessageInbox")]
    [Index(nameof(UUID))]
    public class SystemMessageInbox
    {
        public SystemMessageInbox(int id, int UUID, int messageId, int chatId, bool isDeleted, int sequence)
        {
            Id = id;
            this.UUID = UUID;
            MessageId = messageId;
            ChatId = chatId;
            IsDeleted = isDeleted;
            Sequence = sequence;
        }

        [Key]
        public int Id { get; set; } //主键
        public int UUID { get; set; } //消息所有者
        public int MessageId { get; set; } //消息ID，一条SystemMessage的唯一标识
        public int ChatId { get; set; } //消息所属的ChatId
        public bool IsDeleted { get; set; } = false; //是否已被删除
        public int Sequence { get; set; } //该SystemMessage对于消息所有者来说的Sequence，用于消息对齐
    }
}
