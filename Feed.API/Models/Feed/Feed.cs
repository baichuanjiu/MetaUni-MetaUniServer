using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using Feed.API.ReusableClass;

namespace Feed.API.Models.Feed
{
    public class Feed
    {
        public Feed(string? id, MediaMetadata? cover, string previewContent, BriefMiniAppInfo briefMiniAppInfo, string title, string description, string openPageUrl)
        {
            Id = id;
            Cover = cover;
            PreviewContent = previewContent;
            BriefMiniAppInfo = briefMiniAppInfo;
            Title = title;
            Description = description;
            OpenPageUrl = openPageUrl;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id
        public MediaMetadata? Cover { get; set; }
        public string PreviewContent { get; set; }
        public BriefMiniAppInfo BriefMiniAppInfo { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string OpenPageUrl { get; set; }
    }
}
