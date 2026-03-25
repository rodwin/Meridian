using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreation : Migration
{
    private static readonly string[] JobSchedulesIndexColumns = ["JobId", "IsEnabled"];
    private static readonly string[] JobStepsIndexColumns = ["JobId", "StepOrder"];
    private static readonly string[] OutboxIndexColumns = ["IsRelayed", "ProcessingStartedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "scheduling");

        migrationBuilder.EnsureSchema(
            name: "outbox");

        migrationBuilder.EnsureSchema(
            name: "app");

        migrationBuilder.CreateTable(
            name: "Jobs",
            schema: "scheduling",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Jobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            schema: "outbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MessageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                QueueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "default"),
                Type = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                TraceParent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsRelayed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                RelayedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "JobSchedules",
            schema: "scheduling",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobSchedules", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobSchedules_Jobs_JobId",
                    column: x => x.JobId,
                    principalSchema: "scheduling",
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "JobSteps",
            schema: "scheduling",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StepOrder = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StepType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OnFailure = table.Column<int>(type: "int", nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobSteps_Jobs_JobId",
                    column: x => x.JobId,
                    principalSchema: "scheduling",
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TodoItems",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Labels = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Priority = table.Column<int>(type: "int", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TodoItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_TodoItems_Users_UserId",
                    column: x => x.UserId,
                    principalSchema: "app",
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_JobSchedules_JobId_IsEnabled",
            schema: "scheduling",
            table: "JobSchedules",
            columns: JobSchedulesIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_JobSteps_JobId_StepOrder",
            schema: "scheduling",
            table: "JobSteps",
            columns: JobStepsIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_IsRelayed_ProcessingStartedAt",
            schema: "outbox",
            table: "OutboxMessages",
            columns: OutboxIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_TodoItems_UserId",
            schema: "app",
            table: "TodoItems",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            schema: "app",
            table: "Users",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "JobSchedules",
            schema: "scheduling");

        migrationBuilder.DropTable(
            name: "JobSteps",
            schema: "scheduling");

        migrationBuilder.DropTable(
            name: "OutboxMessages",
            schema: "outbox");

        migrationBuilder.DropTable(
            name: "TodoItems",
            schema: "app");

        migrationBuilder.DropTable(
            name: "Jobs",
            schema: "scheduling");

        migrationBuilder.DropTable(
            name: "Users",
            schema: "app");
    }
}
