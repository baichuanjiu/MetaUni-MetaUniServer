using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace MiniApp.API.Models.MiniApp
{
    public class WebApp
    {
        public WebApp(string? id, string name, string avatar, string description, string backgroundImage, string URL, double trendingValue)
        {
            Id = id;
            Name = name;
            Avatar = avatar;
            Description = description;
            BackgroundImage = backgroundImage;
            this.URL = URL;
            TrendingValue = trendingValue;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id

        public string Type { get; set; } = "WebApp"; //标识MiniApp类型

        public string Name { get; set; } //MiniApp名称

        public string Avatar { get; set; } //MiniApp头像

        public string Description { get; set; } //MiniApp描述

        public string BackgroundImage { get; set; } //MiniApp在应用仓库里的背景图

        public string URL { get; set; } //MiniApp的Web地址

        public double TrendingValue { get; set; } //MiniApp的热度值，用于决定显示顺序的排名
    }
}
