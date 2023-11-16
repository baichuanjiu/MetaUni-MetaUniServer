using Message.API.Entities.Chat;
using Message.API.Entities.Message;
using Message.API.Entities.SystemPromotion;
using Microsoft.EntityFrameworkCore;

namespace Message.API.DataContext.Message
{
    public class MessageContext : DbContext
    {
        public MessageContext(DbContextOptions<MessageContext> options) : base(options)
        {
        }

        public DbSet<Chat> Chats { get; set; }
        public DbSet<CommonChatStatus> CommonChatStatuses { get; set; }
        public DbSet<CommonMessage> CommonMessages { get; set; }
        public DbSet<CommonMessageInbox> CommonMessageInboxes { get; set; }
        public DbSet<SystemPromotion> SystemPromotions { get; set; }
        public DbSet<SystemMessage> SystemMessages { get; set; }
        public DbSet<SystemMessageInbox> SystemMessageInboxes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Chat>();
            modelBuilder.Entity<CommonChatStatus>();
            modelBuilder.Entity<CommonMessage>();
            modelBuilder.Entity<CommonMessageInbox>();

            modelBuilder.HasSequence<int>("CurrentUUID").StartsAt(10000);
            modelBuilder.Entity<SystemPromotion>().Property(u => u.UUID).HasDefaultValueSql("NEXT VALUE FOR CurrentUUID");
            modelBuilder.Entity<SystemMessage>();
            modelBuilder.Entity<SystemMessageInbox>();
        }
    }
}
