using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace MiniApp.API.Models.Review
{
    public class MiniAppReview
    {
        public MiniAppReview(string? id, string miniAppId, int UUID, int stars, string title, DateTime createdTime, string content)
        {
            Id = id;
            MiniAppId = miniAppId;
            this.UUID = UUID;
            Stars = stars;
            Title = title;
            CreatedTime = createdTime;
            Content = content;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id

        public string MiniAppId { get; set; } //标识这条评价数据属于哪个MiniApp

        public int UUID { get; set; } //标识这条评价数据由哪个用户发布

        public int Stars { get; set; } //打分，1-5星

        public string Title { get; set; } //标题

        public DateTime CreatedTime { get; set; } //评价发布时间

        public string Content { get; set; } //正文内容
    }
}
