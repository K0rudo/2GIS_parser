using Microsoft.EntityFrameworkCore;

namespace _2GIS_parser.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ParseJob> ParseJobs { get; set; }
        public DbSet<ParseJobOrganization> ParseJobOrganizations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParseJobOrganization>()
                .HasKey(x => new { x.ParseJobId, x.OrganizationId });
        }
    }
}