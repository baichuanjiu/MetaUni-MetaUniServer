using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Message.API.Entities.Message
{
    [Table("CommonMessage")]
    public class CommonMessage
    {
        public CommonMessage(int id, int senderId, int receiverId, DateTime createdTime, bool isCustom, bool isRecalled, bool isReply, bool isMediaMessage, bool isVoiceMessage, string? customType, string? minimumSupportVersion, string? textOnError, string? customMessageContent, int? messageReplied, string? messageText, string? messageMedias, string? messageVoice)
        {
            Id = id;
            SenderId = senderId;
            ReceiverId = receiverId;
            CreatedTime = createdTime;
            IsCustom = isCustom;
            IsRecalled = isRecalled;
            IsReply = isReply;
            IsMediaMessage = isMediaMessage;
            IsVoiceMessage = isVoiceMessage;
            CustomType = customType;
            MinimumSupportVersion = minimumSupportVersion;
            TextOnError = textOnError;
            CustomMessageContent = customMessageContent;
            MessageReplied = messageReplied;
            MessageText = messageText;
            MessageMedias = messageMedias;
            MessageVoice = messageVoice;
        }

        [Key]
        public int Id { get; set; } //消息ID，一条消息记录的唯一标识
        public int SenderId { get; set; } //消息发送者的UUID
        public int ReceiverId { get; set; } //消息接收者的UUID
        public DateTime CreatedTime { get; set; } //消息产生时间
        public bool IsCustom { get; set; } = false; //是否是特殊消息
        public bool IsRecalled { get; set; } = false; //是否已被撤回
        public bool IsReply { get; set; } = false; //是否是某条消息的回复
        public bool IsMediaMessage { get; set; } = false; //是否是带有图像或视频的消息
        public bool IsVoiceMessage { get; set; } = false; //是否是语音消息
        public string? CustomType { get; set; } //特殊消息类型
        public string? MinimumSupportVersion { get; set; } //特殊消息最低支持的应用版本号
        public string? TextOnError { get; set; } //特殊消息不支持时，显示的文字
        public string? CustomMessageContent { get; set; } //特殊消息内容，为JSON形式存储的字符串
        public int? MessageReplied { get; set; } //回复的某条消息的消息ID
        public string? MessageText { get; set; } //消息的文本内容
        public string? MessageMedias { get; set; } //消息附带的图像或视频，以JSON形式存储的对象数组，数组内容为图像或视频资源存储地址及其它需要的属性
        public string? MessageVoice { get; set; } //以JSON形式存储的对象，对象内容为语音消息资源存储地址以及其它需要的属性
    }
}
