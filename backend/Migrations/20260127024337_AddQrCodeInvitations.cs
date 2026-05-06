using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddQrCodeInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS / IF EXISTS for idempotency
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_invites_unique;");

            migrationBuilder.Sql(@"ALTER TABLE household_invitations ALTER COLUMN email DROP NOT NULL;");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'household_invitations' AND column_name = 'invitation_type') THEN
                        ALTER TABLE household_invitations ADD COLUMN invitation_type character varying(16) NOT NULL DEFAULT 'email';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'household_invitations' AND column_name = 'token') THEN
                        ALTER TABLE household_invitations ADD COLUMN token character varying(32);
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS idx_invites_token ON household_invitations (token) WHERE token IS NOT NULL;");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS idx_invites_unique ON household_invitations (household_id, email) WHERE status = 'pending' AND invitation_type = 'email' AND email IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_invites_token",
                table: "household_invitations");

            migrationBuilder.DropIndex(
                name: "idx_invites_unique",
                table: "household_invitations");

            migrationBuilder.DropColumn(
                name: "invitation_type",
                table: "household_invitations");

            migrationBuilder.DropColumn(
                name: "token",
                table: "household_invitations");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "household_invitations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_invites_unique",
                table: "household_invitations",
                columns: new[] { "household_id", "email" },
                unique: true,
                filter: "status = 'pending'");
        }
    }
}
