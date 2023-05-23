using Microsoft.Extensions.Options;
using MiniApp.API.DataCollection.Review;
using MiniApp.API.Models.Review;
using MongoDB.Driver;

namespace MiniApp.API.MongoDBServices.Review
{
    public class MiniAppReviewService
    {
        private readonly IMongoCollection<MiniAppReview> _miniAppReviewDocumentsCollection;

        public MiniAppReviewService(IOptions<MiniAppReviewCollectionSettings> miniAppReviewCollectionSettings)
        {
            var mongoClient = new MongoClient(
                miniAppReviewCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                miniAppReviewCollectionSettings.Value.DatabaseName);

            _miniAppReviewDocumentsCollection = mongoDatabase.GetCollection<MiniAppReview>(
                miniAppReviewCollectionSettings.Value.MiniAppReviewCollectionName);
        }

        public MiniAppReview GetLatestById(string id) {
            return _miniAppReviewDocumentsCollection.Find(review => review.MiniAppId == id).SortByDescending(review => review.CreatedTime).FirstOrDefault();
        }
    }
}
