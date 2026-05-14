namespace StudentManagementSystem.Models
{
    /// <summary>
    /// Manage Result screen: filters plus one <see cref="StudentResult"/> row per student (Student loaded for display only).
    /// </summary>
    public class ManageResultsPageViewModel
    {
        public string Session { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Section { get; set; } = "";
        public string Term { get; set; } = "";
        public string ExamType { get; set; } = "";
        public string Subject { get; set; } = "";
        public List<StudentResult> Results { get; set; } = new();
    }
}
