using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Models;

namespace StudentManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<AdditionalInfo> AdditionalInfo { get; set; }
        public DbSet<Admission> Admissions { get; set; }
        public DbSet<Transport> Transports { get; set; }
        public DbSet<InternationalDetail> InternationalDetails { get; set; }
        public DbSet<MedicalDetail> MedicalDetails { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<FeeChallan> FeeChallans { get; set; }
        public DbSet<FeeSchedule> FeeSchedules { get; set; }
        public DbSet<FeeScheduleItem> FeeScheduleItems { get; set; }
        public DbSet<StudentResult> StudentResults { get; set; }
        public DbSet<TeacherAssignment> TeacherAssignments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);



            modelBuilder.Entity<Student>()
                .HasOne(s => s.Parent)
                .WithOne(p => p.Student)
                .HasForeignKey<Parent>(p => p.StudentID);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.AdditionalInfo)
                .WithOne(a => a.Student)
                .HasForeignKey<AdditionalInfo>(a => a.StudentID);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.Admission)
                .WithOne(a => a.Student)
                .HasForeignKey<Admission>(a => a.StudentID);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.Transport)
                .WithOne(t => t.Student)
                .HasForeignKey<Transport>(t => t.StudentID);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.InternationalDetail)
                .WithOne(i => i.Student)
                .HasForeignKey<InternationalDetail>(i => i.StudentID);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.MedicalDetail)
                .WithOne(m => m.Student)
                .HasForeignKey<MedicalDetail>(m => m.StudentID);

            // Configure one-to-many relationship
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Student)
                .WithMany(s => s.Documents)
                .HasForeignKey(d => d.StudentID);

            modelBuilder.Entity<FeeScheduleItem>()
                .HasOne(i => i.FeeSchedule)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.FeeScheduleId);
        }
    }
}
