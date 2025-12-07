using JobPosts.Models;
using JobPosts.Seeds;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobPosts.Data
{
    public class JobPostsDbContext : IdentityDbContext<ApplicationUser>
    {
        public JobPostsDbContext(DbContextOptions<JobPostsDbContext> options)
        : base(options) { }

        public DbSet<Adzuna> AdzunaJobs { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<JobPost> JobPosts { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<ContractType> ContractTypes { get; set; }
        public DbSet<ContractTime> ContractTimes { get; set; }
        public DbSet<WorkplaceModel> WorkplaceModels { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<JobPostSkill> JobPostSkills { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<JobPostLanguage> JobPostLanguages { get; set; }
        public DbSet<UserFavoriteJob> UserFavoriteJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Seed();

            modelBuilder.Entity<Adzuna>().ToTable("Adzuna");

            modelBuilder.Entity<UserFavoriteJob>(entity =>
            {
                entity.ToTable("UserFavoriteJobs");

                entity.HasKey(ufj => new { ufj.UserId, ufj.JobPostId });

                entity.HasOne(ufj => ufj.JobPost)
                      .WithMany(j => j.UsersWhoFavorited)
                      .HasForeignKey(ufj => ufj.JobPostId)
                      .HasConstraintName("FK_UserFavoriteJobs_JobPosts")
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ufj => ufj.User)
                      .WithMany(u => u.FavoriteJobs)
                      .HasForeignKey(ufj => ufj.UserId)
                      .HasConstraintName("FK_UserFavoriteJobs_Users")
                      .OnDelete(DeleteBehavior.NoAction); // Changed from Restrict to NoAction

                entity.HasIndex(e => new { e.UserId, e.JobPostId }).IsUnique().HasDatabaseName("IX_UserFavoriteJobs_UserId_JobPostId");
            });

            modelBuilder.Entity<JobPostSkill>()
                .HasKey(jps => new { jps.JobPostId, jps.SkillId });

            modelBuilder.Entity<JobPostSkill>()
                .HasOne(jps => jps.JobPost)
                .WithMany(jp => jp.JobPostSkills)
                .HasForeignKey(jps => jps.JobPostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobPostSkill>()
                .HasOne(jps => jps.Skill)
                .WithMany(s => s.JobPostSkills)
                .HasForeignKey(jps => jps.SkillId)
                .OnDelete(DeleteBehavior.NoAction); // Changed from Restrict to NoAction

            modelBuilder.Entity<JobPostLanguage>()
                .HasKey(jpl => new { jpl.JobPostId, jpl.LanguageId });

            modelBuilder.Entity<JobPostLanguage>()
                .HasOne(jpl => jpl.JobPost)
                .WithMany(jp => jp.JobPostLanguages)
                .HasForeignKey(jpl => jpl.JobPostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobPostLanguage>()
                .HasOne(jpl => jpl.Language)
                .WithMany(l => l.JobPostLanguages)
                .HasForeignKey(jpl => jpl.LanguageId)
                .OnDelete(DeleteBehavior.NoAction); // Changed from Restrict to NoAction

            modelBuilder.Entity<JobPost>()
                .HasOne(jp => jp.Country)
                .WithMany(c => c.JobPosts)
                .HasForeignKey(jp => jp.CountryId)
                .OnDelete(DeleteBehavior.NoAction); // Changed from Restrict to NoAction

            modelBuilder.Entity<JobPost>()
                .HasOne(jp => jp.Company)
                .WithMany(c => c.JobPosts)
                .HasForeignKey(jp => jp.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<JobPost>()
                .HasOne(jp => jp.Location)
                .WithMany(l => l.JobPosts)
                .HasForeignKey(jp => jp.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<JobPost>()
                .HasOne(jp => jp.ContractType)
                .WithMany(ct => ct.JobPosts)
                .HasForeignKey(jp => jp.ContractTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<JobPost>()
                .HasOne(jp => jp.ContractTime)
                .WithMany(ct => ct.JobPosts)
                .HasForeignKey(jp => jp.ContractTimeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<JobPost>()
                .HasOne(jp => jp.WorkplaceModel)
                .WithMany(wm => wm.JobPosts)
                .HasForeignKey(jp => jp.WorkplaceModelId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Location>()
                .HasOne(l => l.Country)
                .WithMany(c => c.Locations)
                .HasForeignKey(l => l.CountryId)
                .OnDelete(DeleteBehavior.NoAction); // Changed from Restrict to NoAction

            modelBuilder.Entity<Company>()
                .HasOne(c => c.Country)
                .WithMany(cn => cn.Companies)
                .HasForeignKey(c => c.CountryId)
                .OnDelete(DeleteBehavior.NoAction); // Changed from Restrict to NoAction

            modelBuilder.Entity<Location>()
                .HasIndex(l => new { l.CountryId, l.LocationName })
                .IsUnique()
                .HasDatabaseName("IX_Location_CountryId_LocationName_Unique");

            modelBuilder.Entity<Company>()
                .HasIndex(c => new { c.CountryId, c.CompanyName })
                .IsUnique()
                .HasDatabaseName("IX_Company_CountryId_CompanyName_Unique");

            modelBuilder.Entity<JobPost>()
                .Property(j => j.Title)
                .HasMaxLength(255)
                .IsRequired();

            modelBuilder.Entity<JobPost>(entity =>
            {
                entity.Property(j => j.TitleNormalized)
                    .HasMaxLength(255)
                    .IsRequired(false);

                entity.Property(e => e.IsDetailsUrl)
                    .IsRequired()
                    .HasDefaultValue(false);

                // Configure URL property with proper MaxLength
                entity.Property(e => e.Url)
                    .HasMaxLength(800)
                    .IsRequired(false);

                // Configure Salary property for Careerjet compatibility
                entity.Property(e => e.Salary)
                    .HasMaxLength(50)
                    .IsRequired(false);

                // Configure DataSource property
                entity.Property(e => e.DataSource)
                    .HasMaxLength(20)
                    .IsRequired(false);

                // IMPORTANT: Add unique constraint on URL for external sources to prevent duplicates
                entity.HasIndex(e => e.Url)
                    .IsUnique()
                    .HasFilter("[DataSource] IS NOT NULL AND [Url] IS NOT NULL")
                    .HasDatabaseName("IX_JobPost_Url_External_Unique");

                // Add index on DataSource for filtering
                entity.HasIndex(e => e.DataSource)
                    .HasDatabaseName("IX_JobPost_DataSource");

                // Add composite index for common queries
                entity.HasIndex(e => new { e.DataSource, e.CountryId })
                    .HasDatabaseName("IX_JobPost_DataSource_CountryId");

                // Composite index for deduplication
                entity.HasIndex(e => new { e.CompositeHash, e.DataSource, e.CountryId })
                      .HasDatabaseName("IX_JobPosts_CompositeHash_DataSource_CountryId");

                // Performance indexes
                entity.HasIndex(e => new { e.DataSource, e.CountryId, e.Created })
                      .HasDatabaseName("IX_JobPosts_DataSource_CountryId_Created");

                // Additional unique constraint if needed
                entity.HasIndex(e => new { e.CompositeHash, e.DataSource })
                      .IsUnique()
                      .HasFilter("[CompositeHash] IS NOT NULL AND [DataSource] IS NOT NULL")
                      .HasDatabaseName("UQ_JobPosts_CompositeHash_DataSource");
            });
        }
    }
}
