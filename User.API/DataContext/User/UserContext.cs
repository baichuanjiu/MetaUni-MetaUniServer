using Microsoft.EntityFrameworkCore;
using User.API.Entities.Friend;
using User.API.Entities.User;

namespace User.API.DataContext.User
{
    public class UserContext : DbContext
    {
        public UserContext(DbContextOptions<UserContext> options) : base(options)
        {
        }

        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<UserSyncTable> UserSyncTables { get; set; }
        public DbSet<FriendsGroup> FriendsGroups { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<AddFriendRequest> AddFriendRequests { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<int>("CurrentUUID").StartsAt(10000000);

            modelBuilder.Entity<UserAccount>().Property(u => u.UUID).HasDefaultValueSql("NEXT VALUE FOR CurrentUUID");
            modelBuilder.Entity<UserProfile>();
            modelBuilder.Entity<UserRole>();
            modelBuilder.Entity<UserSyncTable>();
            modelBuilder.Entity<FriendsGroup>();
            modelBuilder.Entity<Friendship>();
            modelBuilder.Entity<AddFriendRequest>();
        }

    }
}
