using Feed.API.DataCollection.Feed;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Feed.API.MongoDBServices.Feed
{
    public class FeedService
    {
        private readonly IMongoCollection<Models.Feed.Feed> _feedCollection;
        private readonly IMongoCollection<BsonDocument> _bsonDocumentsCollection;

        public FeedService(IOptions<FeedCollectionSettings> feedCollectionSettings)
        {
            var mongoClient = new MongoClient(
                feedCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                feedCollectionSettings.Value.DatabaseName);

            _feedCollection = mongoDatabase.GetCollection<Models.Feed.Feed>(
                feedCollectionSettings.Value.FeedCollectionName);

            _bsonDocumentsCollection = mongoDatabase.GetCollection<BsonDocument>(
                feedCollectionSettings.Value.FeedCollectionName);
        }

        public async Task<List<Models.Feed.Feed>> GetFeedsByIdListAsync(List<string> idList)
        {
            var feeds = await _feedCollection
                .Find(feed => idList.Contains(feed.Id!))
                .ToListAsync();
            feeds.Sort((a, b) => idList.IndexOf(a.Id!).CompareTo(idList.IndexOf(b.Id!)));
            return feeds;
        }
    }
}
