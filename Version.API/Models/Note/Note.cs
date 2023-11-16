using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Version.API.Models.Note
{
    public class Note
    {
        public Note(string? id, string title, string description, string version, DateTime createdTime)
        {
            Id = id;
            Title = title;
            Description = description;
            Version = version;
            CreatedTime = createdTime;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id
        public string Title { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public DateTime CreatedTime { get; set; }
    }
}
