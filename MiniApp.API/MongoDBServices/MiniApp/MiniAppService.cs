using Microsoft.Extensions.Options;
using MiniApp.API.DataCollection.MiniApp;
using MiniApp.API.Models.MiniApp;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MiniApp.API.MongoDBServices.MiniApp
{
    public class MiniAppService
    {
        private readonly IMongoCollection<ClientApp> _clientAppsCollection;
        private readonly IMongoCollection<WebApp> _webAppsCollection;
        private readonly IMongoCollection<BsonDocument> _bsonDocumentsCollection;

        public MiniAppService(IOptions<MiniAppCollectionSettings> miniAppCollectionSettings)
        {
            var mongoClient = new MongoClient(
                miniAppCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                miniAppCollectionSettings.Value.DatabaseName);

            _clientAppsCollection = mongoDatabase.GetCollection<ClientApp>(
                miniAppCollectionSettings.Value.MiniAppCollectionName);

            _webAppsCollection = mongoDatabase.GetCollection<WebApp>(
                miniAppCollectionSettings.Value.MiniAppCollectionName);

            _bsonDocumentsCollection = mongoDatabase.GetCollection<BsonDocument>(
                miniAppCollectionSettings.Value.MiniAppCollectionName);
        }

        //根据Rank（排名）获取MiniApps信息（每次固定最多获取20个）
        public string GetMiniAppsByRank(int rank) {
            return _bsonDocumentsCollection
                .Find(new BsonDocument())
                .Project(app => new { Id = app["_id"].ToString(), Type = app["Type"], Name = app["Name"], Avatar = app["Avatar"], Description = app["Description"], BackgroundImage = app["BackgroundImage"], TrendingValue = app["TrendingValue"] })
                .SortByDescending(app => app["TrendingValue"])
                .Skip(rank)
                .Limit(20)
                .ToList()
                .ToJson();
        }

        public ClientApp GetClientAppById(string id)
        {
            return _clientAppsCollection.Find(app => app.Id == id).FirstOrDefault();
        }

        public WebApp GetWebAppById(string id)
        {
            return _webAppsCollection.Find(app => app.Id == id).FirstOrDefault();
        }
    }
}
