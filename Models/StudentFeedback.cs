using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.Models
{
    public class StudentFeedback
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentID { get; set; }
        [ForeignKey("StudentID")]
        public virtual Student? Student { get; set; }

        [Required]
        [MaxLength(50)]
        public string Session { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Class { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Section { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Term { get; set; } = string.Empty;

        public string AcademicFeedback { get; set; } = string.Empty;
        public string BehavioralFeedback { get; set; } = string.Empty;
        public string ExtracurricularFeedback { get; set; } = string.Empty;
    }
}
