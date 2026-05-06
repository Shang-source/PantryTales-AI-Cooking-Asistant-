#!/usr/bin/env python3
"""
Clean recipe steps by removing entries that are empty or contain only numbers.

This script connects to the Postgres database and updates recipes where steps
contain invalid entries (empty strings, whitespace-only, or just numbers like "1", "2").

Usage:
    python clean_recipe_steps.py --connection "<Postgres DSN>"
    python clean_recipe_steps.py --dry-run  # Preview changes without applying

Environment variables:
    POSTGRES_DSN or POSTGRES_CONNECTION_STRING - Database connection string
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass, field


@dataclass(slots=True)
class CleanStats:
    total_recipes: int = 0
    recipes_with_steps: int = 0
    recipes_cleaned: int = 0
    steps_removed: int = 0
    empty_steps_removed: int = 0
    number_only_steps_removed: int = 0
    recipes_by_removed_count: dict[int, int] = field(default_factory=dict)


# Regex to match strings that are only numbers (with optional whitespace)
NUMBER_ONLY_RE = re.compile(r"^\s*\d+\.?\s*$")


def is_invalid_step(step_text: str) -> tuple[bool, str]:
    """
    Check if a step is invalid (empty or number-only).

    Returns:
        Tuple of (is_invalid, reason)
    """
    if not step_text or not step_text.strip():
        return True, "empty"

    stripped = step_text.strip()
    if NUMBER_ONLY_RE.match(stripped):
        return True, "number_only"

    return False, ""


def clean_steps(steps_json: str) -> tuple[str, int, int, int]:
    """
    Clean steps JSON by removing invalid entries.

    Returns:
        Tuple of (cleaned_json, total_removed, empty_removed, number_only_removed)
    """
    if not steps_json or steps_json.strip() in ("", "[]", "null"):
        return steps_json, 0, 0, 0

    try:
        steps = json.loads(steps_json)
    except json.JSONDecodeError:
        return steps_json, 0, 0, 0

    if not isinstance(steps, list):
        return steps_json, 0, 0, 0

    cleaned_steps = []
    empty_removed = 0
    number_only_removed = 0

    for step in steps:
        # Handle string steps
        if isinstance(step, str):
            is_invalid, reason = is_invalid_step(step)
            if is_invalid:
                if reason == "empty":
                    empty_removed += 1
                else:
                    number_only_removed += 1
                continue
            cleaned_steps.append(step)

        # Handle object steps
        elif isinstance(step, dict):
            instruction = step.get("instruction") or step.get("text") or ""
            is_invalid, reason = is_invalid_step(instruction)
            if is_invalid:
                if reason == "empty":
                    empty_removed += 1
                else:
                    number_only_removed += 1
                continue
            cleaned_steps.append(step)

        # Keep other types as-is (shouldn't happen but be safe)
        else:
            cleaned_steps.append(step)

    total_removed = empty_removed + number_only_removed
    if total_removed == 0:
        return steps_json, 0, 0, 0

    # Re-number the steps if they have order fields
    for i, step in enumerate(cleaned_steps, start=1):
        if isinstance(step, dict) and "order" in step:
            step["order"] = i

    cleaned_json = json.dumps(cleaned_steps, ensure_ascii=False)
    return cleaned_json, total_removed, empty_removed, number_only_removed


def parse_ado_net_connection_string(value: str) -> str:
    """Parse ADO.NET connection string to libpq format."""
    pairs: dict[str, str] = {}
    for part in value.split(";"):
        part = part.strip()
        if not part or "=" not in part:
            continue
        key, val = part.split("=", 1)
        pairs[key.strip().lower()] = val.strip()

    host = pairs.get("host") or pairs.get("server") or "localhost"
    port = pairs.get("port") or "5432"
    database = pairs.get("database") or pairs.get("initial catalog") or "postgres"
    username = pairs.get("username") or pairs.get("user id") or "postgres"
    password = pairs.get("password") or ""

    dsn = f"host={host} port={port} dbname={database} user={username}"
    if password:
        dsn += f" password={password}"
    return dsn


def resolve_connection_string(cli_value: str | None) -> str | None:
    """Resolve database connection string from CLI or environment."""
    value = (
        cli_value
        or os.environ.get("POSTGRES_DSN")
        or os.environ.get("POSTGRES_CONNECTION_STRING")
    )
    if not value:
        return None
    if ";" in value and "=" in value and "://" not in value:
        return parse_ado_net_connection_string(value)
    return value


def require_psycopg():
    """Ensure psycopg is installed."""
    try:
        import psycopg

        return psycopg
    except ImportError:
        sys.exit(
            "Missing dependency: psycopg.\n"
            'Install with: `pip install "psycopg[binary]"`\n'
        )


def run_cleanup(dsn: str, dry_run: bool = False, verbose: bool = False) -> CleanStats:
    """
    Run the cleanup process on all recipes.

    Args:
        dsn: Database connection string
        dry_run: If True, preview changes without applying them
        verbose: If True, print details for each cleaned recipe

    Returns:
        CleanStats with summary of changes
    """
    psycopg = require_psycopg()
    stats = CleanStats()

    print(f"{'[DRY RUN] ' if dry_run else ''}Connecting to database...")

    with psycopg.connect(dsn) as conn:
        with conn.cursor() as cur:
            # Get all recipes with steps
            # steps is JSONB, so compare with JSON values, not strings
            cur.execute("""
                SELECT id, title, steps::text
                FROM recipes
                WHERE steps IS NOT NULL
                  AND steps != '[]'::jsonb
                  AND jsonb_array_length(steps) > 0
                ORDER BY created_at DESC
            """)

            recipes = cur.fetchall()
            stats.total_recipes = len(recipes)

            print(f"Found {stats.total_recipes} recipes with steps")

            updates = []

            for recipe_id, title, steps_json in recipes:
                stats.recipes_with_steps += 1

                cleaned_json, total_removed, empty_removed, number_only_removed = (
                    clean_steps(steps_json)
                )

                if total_removed > 0:
                    stats.recipes_cleaned += 1
                    stats.steps_removed += total_removed
                    stats.empty_steps_removed += empty_removed
                    stats.number_only_steps_removed += number_only_removed

                    # Track distribution
                    stats.recipes_by_removed_count[total_removed] = (
                        stats.recipes_by_removed_count.get(total_removed, 0) + 1
                    )

                    updates.append((cleaned_json, recipe_id))

                    if verbose:
                        print(
                            f"  Recipe '{title}' ({recipe_id}): removed {total_removed} steps "
                            f"({empty_removed} empty, {number_only_removed} number-only)"
                        )

            if updates and not dry_run:
                print(f"\nApplying {len(updates)} updates...")
                cur.executemany(
                    "UPDATE recipes SET steps = %s::jsonb WHERE id = %s", updates
                )
                conn.commit()
                print("Done!")
            elif updates and dry_run:
                print(f"\n[DRY RUN] Would update {len(updates)} recipes")
            else:
                print("\nNo recipes need cleaning")

    return stats


def print_stats(stats: CleanStats, dry_run: bool = False) -> None:
    """Print summary statistics."""
    prefix = "[DRY RUN] " if dry_run else ""

    print(f"\n{prefix}Summary:")
    print(f"  Total recipes scanned: {stats.total_recipes}")
    print(f"  Recipes with steps: {stats.recipes_with_steps}")
    print(f"  Recipes {'to clean' if dry_run else 'cleaned'}: {stats.recipes_cleaned}")
    print(f"  Steps {'to remove' if dry_run else 'removed'}: {stats.steps_removed}")
    print(f"    - Empty steps: {stats.empty_steps_removed}")
    print(f"    - Number-only steps: {stats.number_only_steps_removed}")

    if stats.recipes_by_removed_count:
        print(f"\n  Distribution of removed steps per recipe:")
        for count in sorted(stats.recipes_by_removed_count.keys()):
            recipes = stats.recipes_by_removed_count[count]
            print(f"    {count} step(s) removed: {recipes} recipe(s)")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Clean recipe steps by removing empty or number-only entries."
    )
    parser.add_argument(
        "--connection",
        default=None,
        help="Postgres DSN (or set POSTGRES_DSN / POSTGRES_CONNECTION_STRING env var)",
    )
    parser.add_argument(
        "--dry-run", action="store_true", help="Preview changes without applying them"
    )
    parser.add_argument(
        "--verbose",
        "-v",
        action="store_true",
        help="Print details for each cleaned recipe",
    )

    args = parser.parse_args()

    dsn = resolve_connection_string(args.connection)
    if not dsn:
        sys.exit(
            "Error: No database connection string provided.\n"
            "Provide --connection or set POSTGRES_DSN / POSTGRES_CONNECTION_STRING."
        )

    stats = run_cleanup(dsn, dry_run=args.dry_run, verbose=args.verbose)
    print_stats(stats, dry_run=args.dry_run)


if __name__ == "__main__":
    main()
