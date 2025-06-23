using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConduitLLM.Configuration.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAsyncTaskStateToInteger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add temporary column
            migrationBuilder.AddColumn<int>(
                name: "StateInt",
                table: "AsyncTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Convert string values to integers
            migrationBuilder.Sql(@"
                UPDATE AsyncTasks 
                SET StateInt = CASE State
                    WHEN 'Pending' THEN 0
                    WHEN 'Processing' THEN 1
                    WHEN 'Completed' THEN 2
                    WHEN 'Failed' THEN 3
                    WHEN 'Cancelled' THEN 4
                    WHEN 'TimedOut' THEN 5
                    ELSE 0
                END
            ");

            // Drop old column
            migrationBuilder.DropColumn(
                name: "State",
                table: "AsyncTasks");

            // Rename new column
            migrationBuilder.RenameColumn(
                name: "StateInt",
                table: "AsyncTasks",
                newName: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add temporary column
            migrationBuilder.AddColumn<string>(
                name: "StateString",
                table: "AsyncTasks",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            // Convert integer values back to strings
            migrationBuilder.Sql(@"
                UPDATE AsyncTasks 
                SET StateString = CASE State
                    WHEN 0 THEN 'Pending'
                    WHEN 1 THEN 'Processing'
                    WHEN 2 THEN 'Completed'
                    WHEN 3 THEN 'Failed'
                    WHEN 4 THEN 'Cancelled'
                    WHEN 5 THEN 'TimedOut'
                    ELSE 'Pending'
                END
            ");

            // Drop old column
            migrationBuilder.DropColumn(
                name: "State",
                table: "AsyncTasks");

            // Rename new column
            migrationBuilder.RenameColumn(
                name: "StateString",
                table: "AsyncTasks",
                newName: "State");
        }
    }
}
