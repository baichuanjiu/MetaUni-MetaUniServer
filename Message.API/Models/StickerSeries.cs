using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Message.API.Models
{
    public class Sticker 
    {
        public Sticker()
        {
        }

        public Sticker(string id, string url, string tittle, string text)
        {
            Id = id;
            Url = url;
            Tittle = tittle;
            Text = text;
        }

        public string Id { get; set; }
        public string Url { get; set; }
        public string Tittle { get; set; }
        public string Text { get; set; }
    }

    public class StickerSeries
    {
        public StickerSeries(string? id, string preview, string tittle, List<Sticker> stickers)
        {
            Id = id;
            Preview = preview;
            Tittle = tittle;
            Stickers = stickers;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } //MongoDB中存储的_Id

        public string Preview { get; set; }

        public string Tittle { get; set; }

        public List<Sticker> Stickers { get; set; }
    }
}
