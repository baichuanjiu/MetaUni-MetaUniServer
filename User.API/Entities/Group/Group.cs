using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.API.Entities.Group
{
    [Table("Group")]
    public class Group
    {
        [Key]
        public int Id { get; set; } //群号，群组的唯一标识
        [MaxLength(20)]
        public string GroupName { get; set; } //群组名称
        public string GroupAvatar { get; set; } //群头像，资源存储地址
        public string? GroupTags { get; set; } //群标签，以JSON形式存储的字符串数组，可以为空 //这个后续应该是要重构的
        [MaxLength(200)]
        public string? GroupDescription { get; set; } //群简介，可以为空
        public int GroupCreator { get; set; } //群创建者，群创建者的UUID
        public DateTime GroupCreatedTime { get; set; } //群创建时间
        public int GroupLeader { get; set; } //群主，群主的UUID
        public string GroupAdministrators { get; set; } //群管理员，以JSON形式存储的Int数组 //这个后续应该是要重构的
        public int Sequence { get; set; } //该群组内最新一条消息的Sequence，用于消息对齐

    }
}
