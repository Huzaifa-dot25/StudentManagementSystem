using System.Collections.Generic;

namespace StudentManagementSystem.Models
{
    public class StudentDetailReportViewModel
    {
        public Student Student { get; set; } = null!;
        public List<FeeChallan> FeeHistory { get; set; } = new();
        public List<StudentResult> Results { get; set; } = new();
        
        public decimal TotalFeePaid => FeeHistory.Where(f => f.Status == "Paid").Sum(f => f.Amount + f.Arrears);
        public decimal TotalFeePending => FeeHistory.Where(f => f.Status == "Unpaid").Sum(f => f.Amount + f.Arrears);
        
        public decimal OverallPercentage => Results.Any(r => r.TotalMarks > 0) 
            ? Math.Round(Results.Where(r => r.Status == "Present").Sum(r => r.ObtainedMarks) / Results.Where(r => r.Status == "Present").Sum(r => r.TotalMarks) * 100, 1) 
            : 0;
            
        public string OverallGrade => OverallPercentage >= 90 ? "A+" : OverallPercentage >= 80 ? "A" : OverallPercentage >= 70 ? "B"
                                    : OverallPercentage >= 60 ? "C"  : OverallPercentage >= 50 ? "D" : "F";
    }
}
