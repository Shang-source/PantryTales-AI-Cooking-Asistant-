#!/usr/bin/env python3
"""
Import cooking knowledge articles from CSV into the knowledgebase_articles table.

CSV format: type, question, response
Mapping:
  - type -> Tag (matched by name or display_name, created if not exists with tag_type='knowledge_base')
  - question -> title
  - response -> content

Usage:
  python import_knowledgebase.py --csv import_cooking_knowledge.csv
  python import_knowledgebase.py --csv import_cooking_knowledge.csv --dry-run
"""

from __future__ import annotations

import argparse
import csv
import os
import sys
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent


@dataclass(slots=True)
class ImportStats:
    read_rows: int = 0
    imported_articles: int = 0
    skipped_rows: int = 0
    skip_reasons: dict[str, int] = field(default_factory=dict)
    created_tags: int = 0
    reused_tags: int = 0

    def skip(self, reason: str) -> None:
        self.skipped_rows += 1
        self.skip_reasons[reason] = self.skip_reasons.get(reason, 0) + 1


def normalize_tag_name(raw: str) -> str:
    """Normalize tag name: lowercase, replace spaces with hyphens."""
    return raw.strip().lower().replace(" ", "-").replace("_", "-")


def tag_display_name(raw: str) -> str:
    """Create display name from raw type value."""
    return raw.strip().title()


def parse_ado_net_connection_string(value: str) -> str:
    """Parse ADO.NET connection string to libpq format."""
    parts: dict[str, str] = {}
    for piece in value.split(";"):
        piece = piece.strip()
        if not piece or "=" not in piece:
            continue
        k, v = piece.split("=", 1)
        parts[k.strip().lower()] = v.strip()

    host = parts.get("host") or parts.get("server")
    port = parts.get("port")
    dbname = parts.get("database") or parts.get("initial catalog")
    user = parts.get("username") or parts.get("user id") or parts.get("userid")
    password = parts.get("password")
    sslmode = parts.get("ssl mode") or parts.get("sslmode")

    dsn_parts: list[str] = []
    if host:
        dsn_parts.append(f"host={host}")
    if port:
        dsn_parts.append(f"port={port}")
    if dbname:
        dsn_parts.append(f"dbname={dbname}")
    if user:
        dsn_parts.append(f"user={user}")
    if password:
        dsn_parts.append(f"password={password}")
    if sslmode:
        dsn_parts.append(f"sslmode={sslmode.lower()}")
    return " ".join(dsn_parts)


def resolve_connection_string(cli_value: Optional[str]) -> Optional[str]:
    """Resolve database connection string from CLI or environment."""
    value = cli_value or os.environ.get("POSTGRES_DSN") or os.environ.get("POSTGRES_CONNECTION_STRING")
    if not value:
        return None
    if ";" in value and ("host=" in value.lower() or "server=" in value.lower() or "database=" in value.lower()):
        return parse_ado_net_connection_string(value)
    return value


def require_psycopg():
    """Ensure psycopg is installed."""
    try:
        import psycopg
    except ModuleNotFoundError as e:
        raise SystemExit(
            "Missing dependency: psycopg.\n"
            "Install with: `pip install \"psycopg[binary]\"`\n"
            "Then rerun this script."
        ) from e
    return psycopg


def import_to_db(
    *,
    dsn: str,
    csv_path: Path,
    tag_type_name: str,
    batch_size: int,
    dry_run: bool,
) -> ImportStats:
    """Import CSV data into the database."""
    stats = ImportStats()

    # Read and parse CSV
    articles: list[dict] = []
    with csv_path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            stats.read_rows += 1
            
            type_val = row.get("type", "").strip()
            question = row.get("question", "").strip()
            response = row.get("response", "").strip()
            
            if not type_val:
                stats.skip("missing_type")
                continue
            if not question:
                stats.skip("missing_question")
                continue
            if not response:
                stats.skip("missing_response")
                continue
            
            articles.append({
                "type": type_val,
                "title": question[:256],  # Truncate to max length
                "content": response,
            })

    print(f"Read {stats.read_rows} rows, {len(articles)} valid articles")

    if dry_run:
        # Just show what would be imported
        type_counts: dict[str, int] = {}
        for a in articles:
            type_counts[a["type"]] = type_counts.get(a["type"], 0) + 1
        print("\n[DRY RUN] Would import the following:")
        for t, count in sorted(type_counts.items()):
            print(f"  - {t}: {count} articles")
        return stats

    # Cache for tag lookups
    tag_cache: dict[str, int] = {}  # normalized_name -> tag_id

    psycopg = require_psycopg()

    with psycopg.connect(dsn) as conn:
        conn.execute("SET statement_timeout TO '0'")

        with conn.cursor() as cur:
            # Ensure tag_type exists
            cur.execute(
                """
                INSERT INTO tag_types (name, display_name, description)
                VALUES (%s, %s, %s)
                ON CONFLICT (name) DO NOTHING
                """,
                (tag_type_name, tag_type_name.replace("_", " ").title(), "Knowledge base article categories"),
            )

            # Get existing tags of this type
            cur.execute(
                "SELECT id, name FROM tags WHERE type = %s",
                (tag_type_name,),
            )
            for row in cur.fetchall():
                tag_cache[str(row[1]).lower()] = int(row[0])

        now = datetime.now(timezone.utc)

        # Process in batches
        for i in range(0, len(articles), batch_size):
            batch = articles[i:i + batch_size]

            with conn.cursor() as cur:
                article_rows = []

                for article in batch:
                    type_val = article["type"]
                    normalized_type = normalize_tag_name(type_val)

                    # Get or create tag
                    tag_id = tag_cache.get(normalized_type)
                    if tag_id is None:
                        cur.execute(
                            """
                            INSERT INTO tags (name, display_name, type, is_active, updated_at)
                            VALUES (%s, %s, %s, %s, %s)
                            ON CONFLICT (name, type) DO UPDATE
                            SET display_name = EXCLUDED.display_name,
                                is_active = EXCLUDED.is_active,
                                updated_at = EXCLUDED.updated_at
                            RETURNING id
                            """,
                            (normalized_type, tag_display_name(type_val), tag_type_name, True, now),
                        )
                        tag_id = int(cur.fetchone()[0])
                        tag_cache[normalized_type] = tag_id
                        stats.created_tags += 1
                    else:
                        stats.reused_tags += 1

                    article_rows.append((
                        str(uuid.uuid4()),
                        tag_id,
                        article["title"],
                        None,  # subtitle
                        None,  # icon_name
                        article["content"],
                        True,  # is_published
                        now,
                        now,
                    ))

                # Insert articles
                cur.executemany(
                    """
                    INSERT INTO knowledgebase_articles (id, tag_id, title, subtitle, icon_name, content, is_published, created_at, updated_at)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                    """,
                    article_rows,
                )

            conn.commit()
            stats.imported_articles += len(batch)
            print(f"Imported {stats.imported_articles}/{len(articles)} articles...")

    return stats


def main():
    parser = argparse.ArgumentParser(description="Import cooking knowledge articles from CSV")
    parser.add_argument(
        "--csv",
        type=Path,
        default=SCRIPT_DIR / "import_cooking_knowledge.csv",
        help="Path to the CSV file (default: import_cooking_knowledge.csv in scripts folder)",
    )
    parser.add_argument(
        "--dsn",
        type=str,
        default=None,
        help="PostgreSQL connection string (or set POSTGRES_DSN env var)",
    )
    parser.add_argument(
        "--tag-type",
        type=str,
        default="knowledge_base",
        help="Tag type name for created tags (default: knowledge_base)",
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=100,
        help="Number of articles to insert per batch (default: 100)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would be imported without making changes",
    )

    args = parser.parse_args()

    if not args.csv.exists():
        print(f"Error: CSV file not found: {args.csv}", file=sys.stderr)
        sys.exit(1)

    dsn = resolve_connection_string(args.dsn)
    if not dsn and not args.dry_run:
        print(
            "Error: No database connection string provided.\n"
            "Set POSTGRES_DSN or POSTGRES_CONNECTION_STRING environment variable,\n"
            "or use --dsn option.",
            file=sys.stderr,
        )
        sys.exit(1)

    print(f"CSV file: {args.csv}")
    print(f"Tag type: {args.tag_type}")
    if args.dry_run:
        print("Mode: DRY RUN (no changes will be made)")
    print()

    stats = import_to_db(
        dsn=dsn or "",
        csv_path=args.csv,
        tag_type_name=args.tag_type,
        batch_size=args.batch_size,
        dry_run=args.dry_run,
    )

    print("\n=== Import Complete ===")
    print(f"Read rows: {stats.read_rows}")
    print(f"Imported articles: {stats.imported_articles}")
    print(f"Skipped rows: {stats.skipped_rows}")
    if stats.skip_reasons:
        print("Skip reasons:")
        for reason, count in stats.skip_reasons.items():
            print(f"  - {reason}: {count}")
    print(f"Created tags: {stats.created_tags}")
    print(f"Reused tags: {stats.reused_tags}")


if __name__ == "__main__":
    main()
