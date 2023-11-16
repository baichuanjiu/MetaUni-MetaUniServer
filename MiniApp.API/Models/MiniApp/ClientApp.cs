using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace MiniApp.API.Models.MiniApp
{
    public class ClientApp
    {
        public ClientApp(string? id, string name, string avatar, string description, string backgroundImage, string routingURL, string? URL, string minimumSupportVersion)
        {
            Id = id;
            Name = name;
            Avatar = avatar;
            Description = description;
            BackgroundImage = backgroundImage;
            RoutingURL = routingURL;
            this.URL = URL;
            MinimumSupportVersion = minimumSupportVersion;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id

        public string Type { get; set; } = "ClientApp"; //标识MiniApp类型

        public string Name { get; set; } //MiniApp名称

        public string Avatar { get; set; } //MiniApp头像

        public string Description { get; set; } //MiniApp描述

        public string BackgroundImage { get; set; } //MiniApp在应用仓库里的背景图

        public string RoutingURL { get; set; } //MiniApp页面在客户端内的路由地址

        public string? URL { get; set; } //MiniApp的服务端地址

        public string MinimumSupportVersion { get; set; } //MiniApp最低支持的版本号
    }
}
