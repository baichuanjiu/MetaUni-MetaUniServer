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

        public async Task<string> GetMiniAppsByIdListAsync(List<string> idList)
        {
            var miniApps = await _bsonDocumentsCollection
                .Find(bson => idList.Contains(bson["_id"].ToString()!))
                .Project(app => new { Id = app["_id"].ToString(), Type = app["Type"], Name = app["Name"], Avatar = app["Avatar"], Description = app["Description"], BackgroundImage = app["BackgroundImage"] })
                .ToListAsync();
            miniApps.Sort((a, b) => idList.IndexOf(a.Id!).CompareTo(idList.IndexOf(b.Id!)));
            return miniApps.ToJson();
        }

        public ClientApp GetClientAppById(string id)
        {
            return _clientAppsCollection.Find(app => app.Id == id).FirstOrDefault();
        }

        public WebApp GetWebAppById(string id)
        {
            return _webAppsCollection.Find(app => app.Id == id).FirstOrDefault();
        }

        public string GetInformationById(string id) {
            return _bsonDocumentsCollection.Find(app => app["_id"] == new ObjectId(id))
                .Project(app => new { Id = app["_id"].ToString(), Type = app["Type"], Name = app["Name"], Avatar = app["Avatar"], RoutingURL = app["RoutingURL"] ?? BsonNull.Value, URL = app["URL"] ?? BsonNull.Value, MinimumSupportVersion = app["MinimumSupportVersion"] ?? BsonNull.Value })
                .FirstOrDefault()
                .ToJson();
        }
    }
}
