using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptiveStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DescriptiveStatements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StatementText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescriptiveStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentID = table.Column<int>(type: "int", nullable: false),
                    Session = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Class = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Term = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AcademicFeedback = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BehavioralFeedback = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtracurricularFeedback = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFeedbacks_Students_StudentID",
                        column: x => x.StudentID,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeedbacks_StudentID",
                table: "StudentFeedbacks",
                column: "StudentID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DescriptiveStatements");

            migrationBuilder.DropTable(
                name: "StudentFeedbacks");
        }
    }
}
