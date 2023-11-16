using Microsoft.Extensions.Options;
using MiniApp.API.DataCollection.Introduction;
using MiniApp.API.Models.Introduction;
using MongoDB.Driver;

namespace MiniApp.API.MongoDBServices.Introduction
{
    public class MiniAppIntroductionService
    {
        private readonly IMongoCollection<MiniAppIntroduction> _miniAppIntroductionDocumentsCollection;

        public MiniAppIntroductionService(IOptions<MiniAppIntroductionCollectionSettings> miniAppIntroductionCollectionSettings)
        {
            var mongoClient = new MongoClient(
                miniAppIntroductionCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                miniAppIntroductionCollectionSettings.Value.DatabaseName);

            _miniAppIntroductionDocumentsCollection = mongoDatabase.GetCollection<MiniAppIntroduction>(
                miniAppIntroductionCollectionSettings.Value.MiniAppIntroductionCollectionName);
        }

        public MiniAppIntroduction GetIntroductionById(string id)
        {
            return _miniAppIntroductionDocumentsCollection.Find(introduction => introduction.MiniAppId == id).FirstOrDefault();
        }
    }
}
