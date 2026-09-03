using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FraudRuleEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerKeysetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fraud_assessments_customer_evaluated_at",
                table: "fraud_assessments");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_customer_evaluated_at_id",
                table: "fraud_assessments",
                columns: new[] { "customer_id", "evaluated_at_utc", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fraud_assessments_customer_evaluated_at_id",
                table: "fraud_assessments");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_customer_evaluated_at",
                table: "fraud_assessments",
                columns: new[] { "customer_id", "evaluated_at_utc" },
                descending: new[] { false, true });
        }
    }
}
