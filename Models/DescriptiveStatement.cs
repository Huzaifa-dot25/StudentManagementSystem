using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.Models
{
    public class DescriptiveStatement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty; // "Academic Performance", "Behavioral Conduct", "Extracurricular"

        [Required]
        public string StatementText { get; set; } = string.Empty;
    }
}
