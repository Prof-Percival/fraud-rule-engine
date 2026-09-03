using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FraudRuleEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transaction_events",
                columns: table => new
                {
                    event_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    merchant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    merchant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    ingested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "fraud_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    risk_score = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rule_set_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evaluated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fraud_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_fraud_assessments_transaction_events_event_id",
                        column: x => x.event_id,
                        principalTable: "transaction_events",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rule_outcomes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_triggered = table.Column<bool>(type: "boolean", nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rule_outcomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_rule_outcomes_fraud_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "fraud_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_customer_evaluated_at",
                table: "fraud_assessments",
                columns: new[] { "customer_id", "evaluated_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_decision_evaluated_at_id",
                table: "fraud_assessments",
                columns: new[] { "decision", "evaluated_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_evaluated_at_id",
                table: "fraud_assessments",
                columns: new[] { "evaluated_at_utc", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_fraud_assessments_event_id",
                table: "fraud_assessments",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rule_outcomes_assessment_id",
                table: "rule_outcomes",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_rule_outcomes_rule_id_triggered",
                table: "rule_outcomes",
                column: "rule_id",
                filter: "is_triggered = true");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_events_customer_occurred_at",
                table: "transaction_events",
                columns: new[] { "customer_id", "occurred_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_events_transaction_id",
                table: "transaction_events",
                column: "transaction_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rule_outcomes");

            migrationBuilder.DropTable(
                name: "fraud_assessments");

            migrationBuilder.DropTable(
                name: "transaction_events");
        }
    }
}
