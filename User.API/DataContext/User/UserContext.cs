using Microsoft.EntityFrameworkCore;
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
        public DbSet<UserSyncTable> UserSynchronizationTables { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<int>("CurrentUUID").StartsAt(10000000);

            modelBuilder.Entity<UserAccount>().HasOne<UserProfile>().WithOne().HasForeignKey<UserProfile>(e => e.UUID).IsRequired();
            modelBuilder.Entity<UserAccount>().HasOne<UserSyncTable>().WithOne().HasForeignKey<UserSyncTable>(e => e.UUID).IsRequired();
            modelBuilder.Entity<UserAccount>().Property(u => u.UUID).HasDefaultValueSql("NEXT VALUE FOR CurrentUUID");
            modelBuilder.Entity<UserProfile>();
            modelBuilder.Entity<UserSyncTable>();
        }

    }
}
