using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace MiniApp.API.Models.MiniApp
{
    public class ClientApp
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id

        public string Type { get; set; } = "ClientApp"; //标识MiniApp类型

        public string Name { get; set; } //MiniApp名称

        public string Avatar { get; set; } //MiniApp头像

        public string Description { get; set; } //MiniApp描述

        public string BackgroundImage { get; set; } //MiniApp在应用仓库里的背景图

        public string RoutingURL { get; set; } //MiniApp页面在客户端内的路由地址

        public string URL { get; set; } //MiniApp的服务端地址

        public double TrendingValue { get; set; } //MiniApp的热度值，用于决定显示顺序的排名

        public string MinimumSupportVersion { get; set; } //MiniApp最低支持的版本号
    }
}
