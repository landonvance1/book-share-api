using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSharingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBookSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "user_book",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "user_book",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Add index on is_deleted for efficient filtering
            migrationBuilder.CreateIndex(
                name: "IX_user_book_is_deleted",
                table: "user_book",
                column: "is_deleted");

            // Update search_accessible_books function to filter out soft-deleted books
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS search_accessible_books(TEXT, TEXT);");

            migrationBuilder.Sql(@"
                CREATE FUNCTION search_accessible_books(
                    p_user_id TEXT,
                    p_search TEXT DEFAULT NULL
                )
                RETURNS TABLE(
                    book_id INTEGER,
                    title TEXT,
                    author TEXT,
                    user_book_id INTEGER,
                    owner_user_id TEXT,
                    owner_first_name TEXT,
                    status INTEGER,
                    community_id INTEGER,
                    community_name TEXT
                ) AS $$
                BEGIN
                    RETURN QUERY
                    SELECT
                        b.book_id,
                        b.title,
                        b.author,
                        ub.user_book_id,
                        ub.user_id as owner_user_id,
                        u.first_name as owner_first_name,
                        ub.status,
                        c.community_id,
                        c.name as community_name
                    FROM book b
                    INNER JOIN user_book ub ON ub.book_id = b.book_id
                        AND ub.status != 2
                        AND ub.user_id != p_user_id
                        AND ub.is_deleted = FALSE
                    INNER JOIN ""AspNetUsers"" u ON u.""Id"" = ub.user_id
                    INNER JOIN community_user cu ON cu.user_id = ub.user_id
                    INNER JOIN community c ON c.community_id = cu.community_id AND c.active = true
                    WHERE c.community_id IN (
                        SELECT cu2.community_id
                        FROM community_user cu2
                        INNER JOIN community c2 ON c2.community_id = cu2.community_id
                        WHERE cu2.user_id = p_user_id AND c2.active = true
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM share s
                        WHERE s.user_book_id = ub.user_book_id
                        AND s.status IN (2, 3, 4)
                    )
                    AND (
                        p_search IS NULL
                        OR p_search = ''
                        OR b.title ILIKE '%' || p_search || '%'
                        OR b.author ILIKE '%' || p_search || '%'
                    )
                    ORDER BY b.title;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore original search function without is_deleted filter
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS search_accessible_books(TEXT, TEXT);");

            migrationBuilder.Sql(@"
                CREATE FUNCTION search_accessible_books(
                    p_user_id TEXT,
                    p_search TEXT DEFAULT NULL
                )
                RETURNS TABLE(
                    book_id INTEGER,
                    title TEXT,
                    author TEXT,
                    user_book_id INTEGER,
                    owner_user_id TEXT,
                    status INTEGER,
                    community_id INTEGER,
                    community_name TEXT
                ) AS $$
                BEGIN
                    RETURN QUERY
                    SELECT
                        b.book_id,
                        b.title,
                        b.author,
                        ub.user_book_id,
                        ub.user_id as owner_user_id,
                        ub.status,
                        c.community_id,
                        c.name as community_name
                    FROM book b
                    INNER JOIN user_book ub ON ub.book_id = b.book_id AND ub.status != 2 AND ub.user_id != p_user_id
                    INNER JOIN community_user cu ON cu.user_id = ub.user_id
                    INNER JOIN community c ON c.community_id = cu.community_id AND c.active = true
                    WHERE c.community_id IN (
                        SELECT cu2.community_id
                        FROM community_user cu2
                        INNER JOIN community c2 ON c2.community_id = cu2.community_id
                        WHERE cu2.user_id = p_user_id AND c2.active = true
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM share s
                        WHERE s.user_book_id = ub.user_book_id
                        AND s.status IN (2, 3, 4)
                    )
                    AND (
                        p_search IS NULL
                        OR p_search = ''
                        OR b.title ILIKE '%' || p_search || '%'
                        OR b.author ILIKE '%' || p_search || '%'
                    )
                    ORDER BY b.title;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.DropIndex(
                name: "IX_user_book_is_deleted",
                table: "user_book");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "user_book");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "user_book");
        }
    }
}
