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
        public DbSet<CompanyOffice> CompanyOffices { get; set; }

        public DbSet<NetworkNode> NetworkNodes { get; set; }
        public DbSet<NetworkNodeHardware> NetworkNodeHardware { get; set; }
        public DbSet<NetworkLink> NetworkLinks { get; set; }
        public DbSet<NetworkLinkWaypoint> NetworkLinkWaypoints { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParseJobOrganization>()
                .HasKey(x => new { x.ParseJobId, x.OrganizationId });
            modelBuilder.Entity<NetworkLink>()
                .HasOne(l => l.FromNode)
                .WithMany(n => n.LinksFrom)
                .HasForeignKey(l => l.FromNodeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<NetworkLink>()
                .HasOne(l => l.ToNode)
                .WithMany(n => n.LinksTo)
                .HasForeignKey(l => l.ToNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}