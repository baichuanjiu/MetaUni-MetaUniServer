using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace MiniApp.API.Models.Introduction
{
    public class MiniAppIntroduction
    {
        public MiniAppIntroduction(string? id, string miniAppId, List<int> stars, string developer, List<string> preview, string guide, string readme)
        {
            Id = id;
            MiniAppId = miniAppId;
            Stars = stars;
            Developer = developer;
            Preview = preview;
            Guide = guide;
            Readme = readme;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id

        public string MiniAppId { get; set; } //标识这条介绍数据属于哪个MiniApp

        public List<int> Stars { get; set; } //数组内共计五个元素，每个元素表示给1、2、3、4、5星的人数

        public string Developer { get; set; } //MiniApp开发者

        public List<string> Preview { get; set; } //预览图

        public string Guide { get; set; } //导览文字

        public string Readme { get; set; } //开发者说
    }
}
