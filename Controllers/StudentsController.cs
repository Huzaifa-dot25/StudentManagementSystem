using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.Data;
using StudentManagementSystem.Models;

namespace StudentManagementSystem.Controllers
{
    [Authorize(Policy = "CanViewStudents")]
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public StudentsController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        private async Task PopulateClassDropdowns()
        {
            ViewBag.Classes = await _context.Admissions
                .Where(a => !string.IsNullOrEmpty(a.Class))
                .Select(a => a.Class)
                .Distinct()
                .ToListAsync();

            if (ViewBag.Classes.Count == 0)
            {
                ViewBag.Classes = new List<string> { "Class 1", "Class 2", "Class 3", "Class 4", "Class 5", "Class 6", "Class 7", "Class 8", "Class 9", "Class 10" };
            }

            ViewBag.Sections = await _context.Admissions
                .Where(a => !string.IsNullOrEmpty(a.Section))
                .Select(a => a.Section)
                .Distinct()
                .ToListAsync();

            if (ViewBag.Sections.Count == 0)
            {
                ViewBag.Sections = new List<string> { "A", "B", "C", "D" };
            }
        }

        // GET: Students
        public async Task<IActionResult> Index(string? searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var students = _context.Students
                .Include(s => s.Admission)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                students = students.Where(s => s.Name.Contains(searchString) 
                                            || s.StudentID.ToString() == searchString
                                            || (s.Admission != null && s.Admission.RollNo != null && s.Admission.RollNo.Contains(searchString)));
            }

            students = students.OrderBy(s => s.Name);

            return View(await students.ToListAsync());
        }

        // GET: Students/PrintList
        public async Task<IActionResult> PrintList(string? searchString)
        {
            var students = _context.Students
                .Include(s => s.Admission)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                students = students.Where(s => s.Name.Contains(searchString) 
                                            || s.StudentID.ToString() == searchString
                                            || (s.Admission != null && s.Admission.RollNo != null && s.Admission.RollNo.Contains(searchString)));
            }

            return View(await students.OrderBy(s => s.Name).ToListAsync());
        }

        // GET: Students/PrintProfile/5
        public async Task<IActionResult> PrintProfile(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                .Include(s => s.Parent)
                .Include(s => s.AdditionalInfo)
                .Include(s => s.Admission)
                .Include(s => s.Transport)
                .Include(s => s.InternationalDetail)
                .Include(s => s.MedicalDetail)
                .FirstOrDefaultAsync(m => m.StudentID == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // GET: Students/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                .Include(s => s.Parent)
                .Include(s => s.AdditionalInfo)
                .Include(s => s.Admission)
                .Include(s => s.Transport)
                .Include(s => s.InternationalDetail)
                .Include(s => s.MedicalDetail)
                .Include(s => s.Documents)
                .FirstOrDefaultAsync(m => m.StudentID == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // GET: Students/Create
        public async Task<IActionResult> Create()
        {
            await PopulateClassDropdowns();
            var student = new Student
            {
                Parent = new Parent(),
                AdditionalInfo = new AdditionalInfo(),
                Admission = new Admission(),
                Transport = new Transport(),
                InternationalDetail = new InternationalDetail(),
                MedicalDetail = new MedicalDetail()
            };
            return View(student);
        }

        // POST: Students/Create
        [Authorize(Policy = "CanManageStudents")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Student student, IFormFile? photoFile, List<IFormFile> documentFiles)
        {
            if (ModelState.IsValid)
            {
                // Handle Photo Upload
                if (photoFile != null)
                {
                    student.PhotoPath = await SaveFile(photoFile, "photos");
                }

                _context.Add(student);
                await _context.SaveChangesAsync();

                // Handle Multiple Documents Upload
                if (documentFiles != null && documentFiles.Count > 0)
                {
                    foreach (var file in documentFiles)
                    {
                        var path = await SaveFile(file, "documents");
                        _context.Documents.Add(new Document
                        {
                            StudentID = student.StudentID,
                            DocumentName = file.FileName,
                            FilePath = path
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            await PopulateClassDropdowns();
            return View(student);
        }

        // GET: Students/Edit/5
        [Authorize(Policy = "CanManageStudents")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            await PopulateClassDropdowns();
            var student = await _context.Students
                .Include(s => s.Parent)
                .Include(s => s.AdditionalInfo)
                .Include(s => s.Admission)
                .Include(s => s.Transport)
                .Include(s => s.InternationalDetail)
                .Include(s => s.MedicalDetail)
                .Include(s => s.Documents)
                .FirstOrDefaultAsync(m => m.StudentID == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // POST: Students/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Student student, IFormFile? photoFile, List<IFormFile> documentFiles)
        {
            if (id != student.StudentID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Load existing student with all related entities
                    var existingStudent = await _context.Students
                        .Include(s => s.Parent)
                        .Include(s => s.AdditionalInfo)
                        .Include(s => s.Admission)
                        .Include(s => s.Transport)
                        .Include(s => s.InternationalDetail)
                        .Include(s => s.MedicalDetail)
                        .FirstOrDefaultAsync(s => s.StudentID == id);

                    if (existingStudent == null) return NotFound();

                    // Update Student core properties
                    _context.Entry(existingStudent).CurrentValues.SetValues(student);

                    // Update Navigation Properties
                    if (student.Parent != null && existingStudent.Parent != null)
                    {
                        student.Parent.StudentID = existingStudent.StudentID;
                        _context.Entry(existingStudent.Parent).CurrentValues.SetValues(student.Parent);
                    }
                    
                    if (student.AdditionalInfo != null && existingStudent.AdditionalInfo != null)
                    {
                        student.AdditionalInfo.StudentID = existingStudent.StudentID;
                        _context.Entry(existingStudent.AdditionalInfo).CurrentValues.SetValues(student.AdditionalInfo);
                    }

                    if (student.Admission != null && existingStudent.Admission != null)
                    {
                        student.Admission.StudentID = existingStudent.StudentID;
                        _context.Entry(existingStudent.Admission).CurrentValues.SetValues(student.Admission);
                    }

                    if (student.Transport != null && existingStudent.Transport != null)
                    {
                        student.Transport.StudentID = existingStudent.StudentID;
                        _context.Entry(existingStudent.Transport).CurrentValues.SetValues(student.Transport);
                    }

                    if (student.InternationalDetail != null && existingStudent.InternationalDetail != null)
                    {
                        student.InternationalDetail.StudentID = existingStudent.StudentID;
                        _context.Entry(existingStudent.InternationalDetail).CurrentValues.SetValues(student.InternationalDetail);
                    }

                    if (student.MedicalDetail != null && existingStudent.MedicalDetail != null)
                    {
                        student.MedicalDetail.StudentID = existingStudent.StudentID;
                        _context.Entry(existingStudent.MedicalDetail).CurrentValues.SetValues(student.MedicalDetail);
                    }

                    // Handle Photo Update
                    if (photoFile != null)
                    {
                        existingStudent.PhotoPath = await SaveFile(photoFile, "photos");
                    }
                    else
                    {
                        // Keep existing photo if no new one provided
                        _context.Entry(existingStudent).Property(x => x.PhotoPath).IsModified = false;
                    }

                    await _context.SaveChangesAsync();

                    // Handle Additional Documents
                    if (documentFiles != null && documentFiles.Count > 0)
                    {
                        foreach (var file in documentFiles)
                        {
                            var path = await SaveFile(file, "documents");
                            _context.Documents.Add(new Document
                            {
                                StudentID = existingStudent.StudentID,
                                DocumentName = file.FileName,
                                FilePath = path
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.StudentID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await PopulateClassDropdowns();
            return View(student);
        }

        // GET: Students/Delete/5
        [Authorize(Policy = "CanManageStudents")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                .FirstOrDefaultAsync(m => m.StudentID == id);
            if (student == null) return NotFound();

            return View(student);
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                _context.Students.Remove(student);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.StudentID == id);
        }

        private async Task<string> SaveFile(IFormFile file, string subFolder)
        {
            string wwwRootPath = _hostEnvironment.WebRootPath;
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string path = Path.Combine(wwwRootPath, "uploads", subFolder, fileName);
            
            using (var fileStream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return "/uploads/" + subFolder + "/" + fileName;
        }
    }
}
