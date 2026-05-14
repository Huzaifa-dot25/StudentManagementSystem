using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models;

namespace StudentManagementSystem.Controllers
{
    public class SeedController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SeedController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            int totalStudentsAdded = 0;
            string[] classes = { "Class 1", "Class 2", "Class 3", "Class 4", "Class 5", "Class 6", "Class 7", "Class 8", "Class 9", "Class 10" };
            string[] sections = { "A", "B" };
            string[] firstNames = { 
                "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda", "David", "Elizabeth", 
                "William", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen",
                "Christopher", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony", "Margaret", "Mark", "Sandra",
                "Donald", "Ashley", "Steven", "Kimberly", "Paul", "Emily", "Andrew", "Donna", "Joshua", "Michelle",
                "Kenneth", "Dorothy", "Kevin", "Carol", "Brian", "Amanda", "George", "Melissa", "Timothy", "Deborah",
                "Ronald", "Stephanie", "Edward", "Rebecca", "Jason", "Sharon", "Jeffrey", "Laura", "Gary", "Cynthia",
                "Jacob", "Kathleen", "Nicholas", "Amy", "Gary", "Angela", "Eric", "Shirley", "Stephen", "Anna",
                "Jonathan", "Brenda", "Larry", "Pamela", "Justin", "Emma", "Scott", "Nicole", "Brandon", "Helen",
                "Benjamin", "Samantha", "Samuel", "Katherine", "Gregory", "Christine", "Alexander", "Debra", "Patrick", "Rachel"
            };
            string[] lastNames = { 
                "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", 
                "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
                "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
                "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
                "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts",
                "Gomez", "Phillips", "Evans", "Turner", "Diaz", "Parker", "Cruz", "Edwards", "Collins", "Reyes",
                "Stewart", "Morris", "Morales", "Murphy", "Cook", "Rogers", "Gutierrez", "Ortiz", "Morgan", "Cooper",
                "Peterson", "Bailey", "Reed", "Kelly", "Howard", "Ramos", "Kim", "Cox", "Ward", "Richardson"
            };
            
            Random rnd = new Random();
            HashSet<string> generatedNames = new HashSet<string>();

            foreach (var className in classes)
            {
                // Ensure we have 10 students for this class
                var currentStudents = await _context.Admissions.Where(a => a.Class == className).CountAsync();
                int studentsToAdd = 10 - currentStudents;

                for (int i = 1; i <= studentsToAdd; i++)
                {
                    string studentName;
                    string firstName = "";
                    string lastName = "";
                    do {
                        firstName = firstNames[rnd.Next(firstNames.Length)];
                        lastName = lastNames[rnd.Next(lastNames.Length)];
                        studentName = $"{firstName} {lastName}";
                    } while (generatedNames.Contains(studentName));
                    
                    generatedNames.Add(studentName);
                    
                    // Generate a specific ID (RollNo) like SMS-2026-C1-001
                    string classShort = className.Replace("Class ", "C");
                    // Check highest existing roll number to ensure uniqueness
                    string rollNo = $"SMS-2026-{classShort}-{(currentStudents + i):D3}";

                    var student = new Student
                    {
                        Name = studentName,
                        RegistrationDate = DateTime.Today.AddDays(-rnd.Next(100)),
                        DateOfBirth = DateTime.Today.AddYears(-rnd.Next(6, 18)),
                        Gender = rnd.Next(2) == 0 ? "Male" : "Female",
                        Status = "Active",
                        ApplicationStatus = "Applied",
                        MobileNo = $"03{rnd.Next(100, 999)}{rnd.Next(1000000, 9999999)}",
                        Address = $"{rnd.Next(1, 999)} Street, {className} Area",
                        Parent = new Parent
                        {
                            FatherName = $"{lastName} Sr.",
                            FatherProfession = "Businessman",
                            FatherMobile = $"03{rnd.Next(100, 999)}{rnd.Next(1000000, 9999999)}",
                            FatherNIC = $"{rnd.Next(10000, 99999)}-{rnd.Next(1000000, 9999999)}-{rnd.Next(1, 9)}",
                            MotherName = $"Mrs. {lastName}"
                        },
                        Admission = new Admission
                        {
                            Session = "2024-2025",
                            Class = className,
                            Section = sections[rnd.Next(sections.Length)],
                            RollNo = rollNo,
                            ActiveInClass = "Yes",
                            DbStatus = "Active"
                        }
                    };

                    _context.Students.Add(student);
                    totalStudentsAdded++;
                }
            }

            if (totalStudentsAdded > 0)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Successfully added {totalStudentsAdded} students across all classes.";
            }
            else
            {
                TempData["Info"] = "Students already exist for these classes.";
            }

            return RedirectToAction("Index", "Students");
        }

        /// <summary>
        /// Adds sample fee challans for students who have none (for reports such as Secondary/StudentDetailReport).
        /// Safe to run multiple times; only fills students with zero challans.
        /// </summary>
        public async Task<IActionResult> DummyChallans()
        {
            var studentIdsWithChallans = await _context.FeeChallans
                .Select(f => f.StudentID)
                .Distinct()
                .ToListAsync();

            var students = await _context.Students
                .Include(s => s.Admission)
                .Where(s => s.Admission != null && !studentIdsWithChallans.Contains(s.StudentID))
                .ToListAsync();

            int added = 0;
            foreach (var student in students)
            {
                var className = student.Admission!.Class ?? "Unknown";

                _context.FeeChallans.Add(new FeeChallan
                {
                    ChallanID = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    StudentID = student.StudentID,
                    ClassName = className,
                    Month = DateTime.Today.AddMonths(-1).ToString("MMM-yy"),
                    Amount = 5000,
                    Arrears = 0,
                    DueDate = DateTime.Today.AddMonths(-1).AddDays(15),
                    Status = "Paid"
                });
                added++;

                _context.FeeChallans.Add(new FeeChallan
                {
                    ChallanID = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    StudentID = student.StudentID,
                    ClassName = className,
                    Month = DateTime.Today.ToString("MMM-yy"),
                    Amount = 5000,
                    Arrears = 0,
                    DueDate = DateTime.Today.AddDays(14),
                    Status = "Unpaid"
                });
                added++;
            }

            if (added > 0)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Added {added} dummy challans for {students.Count} students (one paid, one current unpaid each).";
            }
            else
            {
                TempData["Info"] = "Every student already has at least one challan; nothing to add.";
            }

            return RedirectToAction("Index", "Students");
        }
    }
}
