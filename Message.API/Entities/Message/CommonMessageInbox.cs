using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Message.API.Entities.Message
{
    [Table("CommonMessageInbox")]
    [Index(nameof(UUID))]
    public class CommonMessageInbox
    {
        public CommonMessageInbox(int id, int UUID, int messageId, int chatId, bool isDeleted, int sequence)
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
        public int MessageId { get; set; } //消息ID，一条CommonMessage的唯一标识
        public int ChatId { get; set; } //消息所属的ChatId
        public bool IsDeleted { get; set; } = false; //是否已被删除
        public int Sequence { get; set; } //该CommonMessage对于消息所有者来说的Sequence，用于消息对齐
    }
}
