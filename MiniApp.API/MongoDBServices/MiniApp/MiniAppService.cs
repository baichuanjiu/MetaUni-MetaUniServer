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
        private readonly IMongoCollection<dynamic> _dynamicCollection;

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

            _dynamicCollection = mongoDatabase.GetCollection<dynamic>(
                miniAppCollectionSettings.Value.MiniAppCollectionName);
        }

        public IEnumerable<object> GetMiniApps() {
            return _dynamicCollection.Find(new BsonDocument()).ToEnumerable();
        }
    }
}
