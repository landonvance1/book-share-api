using BookSharingApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookSharingApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Book> Books { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Community> Communities { get; set; }
        public DbSet<CommunityUser> CommunityUsers { get; set; }
        public DbSet<UserBook> UserBooks { get; set; }
        public DbSet<Share> Shares { get; set; }
        public DbSet<ShareUserState> ShareUserStates { get; set; }
        public DbSet<ChatThread> ChatThreads { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ShareChatThread> ShareChatThreads { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.FirstName).HasColumnName("first_name").IsRequired();
                entity.Property(e => e.LastName).HasColumnName("last_name").IsRequired();
            });

            modelBuilder.Entity<Book>(entity =>
            {
                entity.ToTable("book");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("book_id").ValueGeneratedOnAdd();
                entity.Property(e => e.Title).HasColumnName("title").IsRequired();
                entity.Property(e => e.Author).HasColumnName("author").IsRequired();
                entity.HasIndex(e => new { e.Title, e.Author }).IsUnique();
                entity.Ignore(e => e.ThumbnailUrl);
                entity.Ignore(e => e.ExternalThumbnailUrl);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("refresh_tokens");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.Token).HasColumnName("token").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
                entity.Ignore(e => e.IsExpired);
                entity.Ignore(e => e.IsRevoked);
                entity.Ignore(e => e.IsActive);
            });

            modelBuilder.Entity<Community>(entity =>
            {
                entity.ToTable("community");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("community_id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.Active).HasColumnName("active").IsRequired();
                entity.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
                entity.HasOne(e => e.Creator).WithMany().HasForeignKey(e => e.CreatedBy);
            });

            modelBuilder.Entity<CommunityUser>(entity =>
            {
                entity.ToTable("community_user");
                entity.HasKey(e => new { e.CommunityId, e.UserId });
                entity.Property(e => e.CommunityId).HasColumnName("community_id").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.IsModerator).HasColumnName("is_moderator").IsRequired();
                entity.HasOne(e => e.Community).WithMany(c => c.Members).HasForeignKey(e => e.CommunityId);
                entity.HasOne(e => e.User).WithMany(u => u.JoinedCommunities).HasForeignKey(e => e.UserId);
            });

            modelBuilder.Entity<UserBook>(entity =>
            {
                entity.ToTable("user_book");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("user_book_id").ValueGeneratedOnAdd();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.BookId).HasColumnName("book_id").IsRequired();
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.Book).WithMany().HasForeignKey(e => e.BookId);
            });

            modelBuilder.Entity<Share>(entity =>
            {
                entity.ToTable("share");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("share_id").ValueGeneratedOnAdd();
                entity.Property(e => e.UserBookId).HasColumnName("user_book_id").IsRequired();
                entity.Property(e => e.Borrower).HasColumnName("borrower").IsRequired();
                entity.Property(e => e.ReturnDate).HasColumnName("return_date");
                entity.Property(e => e.Status).HasColumnName("status").IsRequired();
                entity.HasOne(e => e.UserBook).WithMany().HasForeignKey(e => e.UserBookId);
                entity.HasOne(e => e.BorrowerUser).WithMany().HasForeignKey(e => e.Borrower);
            });

            modelBuilder.Entity<ShareUserState>(entity =>
            {
                entity.ToTable("share_user_state");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.ShareId).HasColumnName("share_id").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.IsArchived).HasColumnName("is_archived").IsRequired();
                entity.Property(e => e.ArchivedAt).HasColumnName("archived_at");
                entity.HasOne(e => e.Share).WithMany(s => s.ShareUserStates).HasForeignKey(e => e.ShareId);
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);

                // Ensure unique combination of ShareId and UserId
                entity.HasIndex(e => new { e.ShareId, e.UserId }).IsUnique().HasDatabaseName("IX_ShareUserState_ShareId_UserId_Unique");
            });

            modelBuilder.Entity<ChatThread>(entity =>
            {
                entity.ToTable("chat_thread");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("thread_id").ValueGeneratedOnAdd();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.LastActivity).HasColumnName("last_activity");
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("chat_message");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("message_id").ValueGeneratedOnAdd();
                entity.Property(e => e.ThreadId).HasColumnName("thread_id").IsRequired();
                entity.Property(e => e.SenderId).HasColumnName("sender_id").IsRequired();
                entity.Property(e => e.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
                entity.Property(e => e.SentAt).HasColumnName("sent_at").IsRequired();
                entity.Property(e => e.IsSystemMessage).HasColumnName("is_system_message").IsRequired();
                entity.HasOne(e => e.Thread).WithMany(t => t.Messages).HasForeignKey(e => e.ThreadId);
                entity.HasOne(e => e.Sender).WithMany().HasForeignKey(e => e.SenderId);

                // Indexes for efficient querying
                entity.HasIndex(e => new { e.ThreadId, e.SentAt }).HasDatabaseName("IX_ChatMessage_Thread_SentAt");
                entity.HasIndex(e => e.SenderId).HasDatabaseName("IX_ChatMessage_SenderId");
            });

            modelBuilder.Entity<ShareChatThread>(entity =>
            {
                entity.ToTable("share_chat_thread");
                entity.HasKey(e => e.ThreadId);
                entity.Property(e => e.ThreadId).HasColumnName("thread_id").IsRequired();
                entity.Property(e => e.ShareId).HasColumnName("share_id").IsRequired();
                entity.HasOne(e => e.Thread).WithOne(t => t.ShareChatThread).HasForeignKey<ShareChatThread>(e => e.ThreadId);
                entity.HasOne(e => e.Share).WithMany().HasForeignKey(e => e.ShareId);

                // Ensure one thread per share
                entity.HasIndex(e => e.ShareId).IsUnique().HasDatabaseName("IX_ShareChatThread_ShareId_Unique");
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("notification");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("notification_id").ValueGeneratedOnAdd();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.NotificationType).HasColumnName("notification_type").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Message).HasColumnName("message").HasMaxLength(500).IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.ReadAt).HasColumnName("read_at");
                entity.Property(e => e.ShareId).HasColumnName("share_id");
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();

                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.Share).WithMany().HasForeignKey(e => e.ShareId);
                entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

                // Index for efficient unread queries
                entity.HasIndex(e => new { e.UserId, e.ReadAt }).HasDatabaseName("IX_Notification_UserId_ReadAt");

                // Index for auto-mark-as-read queries
                entity.HasIndex(e => new { e.UserId, e.ShareId, e.NotificationType, e.ReadAt }).HasDatabaseName("IX_Notification_UserId_ShareId_Type_ReadAt");
            });
        }
    }
}