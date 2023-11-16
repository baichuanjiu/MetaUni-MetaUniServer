using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Note.API.DataCollection.Note;

namespace Version.API.MongoDBServices.Note
{
    public class NoteService
    {
        private readonly IMongoCollection<Models.Note.Note> _noteCollection;
        private readonly IMongoCollection<BsonDocument> _bsonDocumentsCollection;

        public NoteService(IOptions<NoteCollectionSettings> noteCollectionSettings)
        {
            var mongoClient = new MongoClient(
                noteCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                noteCollectionSettings.Value.DatabaseName);

            _noteCollection = mongoDatabase.GetCollection<Models.Note.Note>(
                noteCollectionSettings.Value.NoteCollectionName);

            _bsonDocumentsCollection = mongoDatabase.GetCollection<BsonDocument>(
                noteCollectionSettings.Value.NoteCollectionName);
        }

        public Models.Note.Note GetLatestNote() 
        {
            return _noteCollection
                .Find(note => true)
                .SortByDescending(note => note.CreatedTime)
                .FirstOrDefault();
        }

        public List<Models.Note.Note> GetAllNotes()
        {
            return _noteCollection
                .Find(note => true)
                .SortByDescending(note => note.CreatedTime)
                .ToList();
        }
    }
}
