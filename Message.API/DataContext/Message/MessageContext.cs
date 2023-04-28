using Message.API.Entities.Chat;
using Message.API.Entities.Message;
using Microsoft.EntityFrameworkCore;

namespace Message.API.DataContext.Message
{
    public class MessageContext : DbContext
    {
        public MessageContext(DbContextOptions<MessageContext> options) : base(options)
        {
        }

        public DbSet<Chat> Chats { get; set; }
        public DbSet<CommonMessage> CommonMessages { get; set; }
        public DbSet<CommonMessageInbox> CommonMessageInboxes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Chat>();
            modelBuilder.Entity<CommonMessage>();
            modelBuilder.Entity<CommonMessageInbox>();
        }
    }
}
