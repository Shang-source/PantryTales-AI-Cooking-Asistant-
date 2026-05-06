#!/usr/bin/env python3
"""
Import Food.com-style recipes from scripts/data/RAW_recipes.csv into the Postgres schema
defined by backend/Models (recipes, ingredients, recipe_ingredients, tags, recipe_tags).

Design notes (aligned with the guidance you shared):
- Treat `ingredients.canonical_name` as the canonical store for ingredient strings.
- Do NOT use `ingredient_aliases` as a staging table for all ingredient strings.
- RAW_recipes.csv does not include ingredient quantities (amount/unit); `recipe_ingredients` uses placeholder values.
"""

from __future__ import annotations

import argparse
import ast
import csv
import heapq
import json
import os
import re
import sys
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Iterable, Optional


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent


@dataclass(slots=True)
class ImportStats:
    read_rows: int = 0
    selected_recipes: int = 0
    imported_recipes: int = 0
    skipped_rows: int = 0
    skip_reasons: dict[str, int] = field(default_factory=dict)
    created_ingredients: int = 0
    created_tags: int = 0
    linked_recipe_ingredients: int = 0
    linked_recipe_tags: int = 0

    def skip(self, reason: str) -> None:
        self.skipped_rows += 1
        self.skip_reasons[reason] = self.skip_reasons.get(reason, 0) + 1


@dataclass(frozen=True, slots=True)
class ParsedRecipe:
    source_id: int
    title: str
    description: Optional[str]
    total_time_minutes: Optional[int]
    difficulty: str
    calories: Optional[Decimal]
    fat: Optional[Decimal]
    sugar: Optional[Decimal]
    sodium: Optional[Decimal]
    protein: Optional[Decimal]
    saturated_fat: Optional[Decimal]
    carbohydrates: Optional[Decimal]
    steps: list[str]
    ingredient_names: list[str]  # normalized ingredient strings
    tag_names: list[str]  # normalized tag slugs from CSV


_WHITESPACE_RE = re.compile(r"\s+")
_PARENS_RE = re.compile(r"\([^)]*\)")
_NON_WORD_RE = re.compile(r"[^a-z0-9\s]+")

_DIFFICULTY_TAG_VALUE: dict[str, str] = {
    "easy": "Easy",
    "medium": "Medium",
    "hard": "Hard",
}
_DIFFICULTY_TAG_SLUGS = set(_DIFFICULTY_TAG_VALUE.keys())


def _is_blank(value: object) -> bool:
    return value is None or (isinstance(value, str) and value.strip() == "")


def _safe_literal_list(raw: str) -> Optional[list]:
    if _is_blank(raw):
        return []
    try:
        parsed = ast.literal_eval(raw)
    except (SyntaxError, ValueError):
        return None
    return parsed if isinstance(parsed, list) else None


def normalize_ingredient_name(raw: str) -> str:
    value = raw.strip().lower()
    value = _PARENS_RE.sub(" ", value)
    value = value.replace("_", " ").replace("-", " ")
    value = _NON_WORD_RE.sub(" ", value)
    value = _WHITESPACE_RE.sub(" ", value).strip()
    return value


def normalize_tag_slug(raw: str) -> str:
    value = raw.strip().lower()
    value = _WHITESPACE_RE.sub("-", value)
    return value


def difficulty_from_tag_slugs(tag_slugs: Iterable[str]) -> str:
    slugs = set(tag_slugs)
    if "hard" in slugs:
        return _DIFFICULTY_TAG_VALUE["hard"]
    if "medium" in slugs:
        return _DIFFICULTY_TAG_VALUE["medium"]
    if "easy" in slugs:
        return _DIFFICULTY_TAG_VALUE["easy"]
    return "None"


def tag_display_name(slug: str) -> str:
    words = [w for w in re.split(r"[-_]+", slug) if w]
    return " ".join(w.capitalize() if not w.isupper() else w for w in words) or slug


def ingredient_tag_slug(ingredient_name: str) -> str:
    return normalize_tag_slug(ingredient_name.replace(" ", "-"))


def truncate(value: str, max_len: int) -> str:
    value = value.strip()
    return value if len(value) <= max_len else value[: max_len - 1].rstrip() + "…"


def parse_decimal(value: object) -> Optional[Decimal]:
    if value is None:
        return None
    if isinstance(value, Decimal):
        return value
    if isinstance(value, (int, float)):
        return Decimal(str(value))
    if isinstance(value, str):
        s = value.strip()
        if not s:
            return None
        try:
            return Decimal(s)
        except InvalidOperation:
            return None
    return None


def parse_recipe_row(row: dict[str, str], stats: ImportStats) -> Optional[ParsedRecipe]:
    stats.read_rows += 1

    name = row.get("name", "")
    if _is_blank(name):
        stats.skip("missing_name")
        return None

    source_id_raw = row.get("id", "")
    try:
        source_id = int(source_id_raw)
    except (TypeError, ValueError):
        stats.skip("invalid_id")
        return None

    steps_raw = row.get("steps", "")
    steps_list = _safe_literal_list(steps_raw)
    if steps_list is None:
        stats.skip("invalid_steps")
        return None
    steps = [s.strip() for s in steps_list if isinstance(s, str) and s.strip()]
    if not steps:
        stats.skip("empty_steps")
        return None

    ingredients_raw = row.get("ingredients", "")
    ingredients_list = _safe_literal_list(ingredients_raw)
    if ingredients_list is None:
        stats.skip("invalid_ingredients")
        return None
    ingredient_names = []
    for item in ingredients_list:
        if not isinstance(item, str):
            continue
        normalized = normalize_ingredient_name(item)
        if normalized:
            ingredient_names.append(normalized)
    ingredient_names = sorted(set(ingredient_names))
    if not ingredient_names:
        stats.skip("empty_ingredients")
        return None

    tags_raw = row.get("tags", "")
    tags_list = _safe_literal_list(tags_raw)
    if tags_list is None:
        stats.skip("invalid_tags")
        return None
    tag_names = []
    for item in tags_list:
        if not isinstance(item, str):
            continue
        slug = normalize_tag_slug(item)
        if slug:
            tag_names.append(slug)
    tag_names = sorted(set(tag_names))
    difficulty = difficulty_from_tag_slugs(tag_names)

    minutes_raw = row.get("minutes", "")
    total_time_minutes: Optional[int]
    try:
        minutes = int(minutes_raw)
        total_time_minutes = minutes if minutes > 0 else None
    except (TypeError, ValueError):
        total_time_minutes = None

    nutrition_raw = row.get("nutrition", "")
    nutrition_list = _safe_literal_list(nutrition_raw)
    calories = fat = sugar = sodium = protein = saturated_fat = carbohydrates = None
    if nutrition_list is not None and len(nutrition_list) == 7:
        calories = parse_decimal(nutrition_list[0])
        fat = parse_decimal(nutrition_list[1])
        sugar = parse_decimal(nutrition_list[2])
        sodium = parse_decimal(nutrition_list[3])
        protein = parse_decimal(nutrition_list[4])
        saturated_fat = parse_decimal(nutrition_list[5])
        carbohydrates = parse_decimal(nutrition_list[6])

    description_raw = row.get("description", "")
    description = None if _is_blank(description_raw) else description_raw.strip()

    return ParsedRecipe(
        source_id=source_id,
        title=truncate(str(name), 256),
        description=description,
        total_time_minutes=total_time_minutes,
        difficulty=difficulty,
        calories=calories,
        fat=fat,
        sugar=sugar,
        sodium=sodium,
        protein=protein,
        saturated_fat=saturated_fat,
        carbohydrates=carbohydrates,
        steps=steps,
        ingredient_names=ingredient_names,
        tag_names=tag_names,
    )


def _clamp01(value: float) -> float:
    return 0.0 if value < 0.0 else 1.0 if value > 1.0 else value


def recipe_quality_score(recipe: ParsedRecipe) -> float:
    """
    "High quality" selection uses a maximin rule:
    - compute multiple dimension scores in [0,1]
    - overall quality = min(dimensions)
    - keep the top-N recipes by this quality (best worst-dimension)
    """

    desc_len = len(recipe.description or "")
    desc_score = _clamp01((desc_len - 20) / 100)  # 0 at <20, 1 at >=120

    steps_count = len(recipe.steps)
    if steps_count < 3:
        steps_score = 0.0
    elif steps_count <= 8:
        steps_score = _clamp01((steps_count - 3) / 5)
    elif steps_count <= 18:
        steps_score = 1.0
    else:
        steps_score = _clamp01(1.0 - (steps_count - 18) / 30)

    ing_count = len(recipe.ingredient_names)
    if ing_count < 3:
        ing_score = 0.0
    elif ing_count <= 8:
        ing_score = _clamp01((ing_count - 3) / 5)
    elif ing_count <= 20:
        ing_score = 1.0
    else:
        ing_score = _clamp01(1.0 - (ing_count - 20) / 40)

    if recipe.total_time_minutes is None:
        time_score = 0.4
    else:
        m = recipe.total_time_minutes
        if m <= 0:
            time_score = 0.0
        elif 5 <= m <= 240:
            time_score = 1.0
        elif m < 5:
            time_score = _clamp01(m / 5)
        else:
            time_score = _clamp01(1.0 - (m - 240) / 600)

    tags_count = len(recipe.tag_names)
    if tags_count == 0:
        tag_score = 0.5
    elif tags_count <= 5:
        tag_score = _clamp01(0.5 + tags_count / 10)
    else:
        tag_score = 1.0

    return min(desc_score, steps_score, ing_score, time_score, tag_score)


def iter_parsed_recipes(csv_path: Path, limit: Optional[int], stats: ImportStats) -> Iterable[ParsedRecipe]:
    with csv_path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            if limit is not None and stats.read_rows >= limit:
                break
            parsed = parse_recipe_row(row, stats)
            if parsed is not None:
                yield parsed


def select_top_recipes(
    *,
    csv_path: Path,
    limit: Optional[int],
    top_n: int,
    stats: ImportStats,
) -> list[ParsedRecipe]:
    heap: list[tuple[float, int, ParsedRecipe]] = []
    for parsed in iter_parsed_recipes(csv_path, limit, stats):
        quality = recipe_quality_score(parsed)
        item = (quality, parsed.source_id, parsed)
        if len(heap) < top_n:
            heapq.heappush(heap, item)
            continue
        if item > heap[0]:
            heapq.heapreplace(heap, item)

    selected = [p for (_, _, p) in sorted(heap, reverse=True)]
    stats.selected_recipes = len(selected)
    return selected


def parse_ado_net_connection_string(value: str) -> str:
    def normalize_sslmode(raw: str) -> str:
        cleaned = "".join(ch for ch in raw.strip().lower() if ch.isalpha())
        return {
            "disable": "disable",
            "allow": "allow",
            "prefer": "prefer",
            "require": "require",
            "verifyca": "verify-ca",
            "verifyfull": "verify-full",
        }.get(cleaned, raw.strip().lower())

    def normalize_channel_binding(raw: str) -> str:
        cleaned = "".join(ch for ch in raw.strip().lower() if ch.isalpha())
        return {
            "disable": "disable",
            "prefer": "prefer",
            "require": "require",
        }.get(cleaned, raw.strip().lower())

    parts: dict[str, str] = {}
    for piece in value.split(";"):
        piece = piece.strip()
        if not piece:
            continue
        if "=" not in piece:
            continue
        k, v = piece.split("=", 1)
        parts[k.strip().lower()] = v.strip()

    host = parts.get("host") or parts.get("server")
    port = parts.get("port")
    dbname = parts.get("database") or parts.get("initial catalog")
    user = parts.get("username") or parts.get("user id") or parts.get("userid")
    password = parts.get("password")
    sslmode = parts.get("ssl mode") or parts.get("sslmode")
    channel_binding = parts.get("channel binding") or parts.get("channelbinding")
    sslrootcert = (
        parts.get("ssl root certificate")
        or parts.get("sslrootcert")
        or parts.get("root certificate")
        or parts.get("rootcertificate")
    )

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
    normalized_sslmode: Optional[str] = None
    if sslmode:
        normalized_sslmode = normalize_sslmode(sslmode)
        dsn_parts.append(f"sslmode={normalized_sslmode}")
    if channel_binding:
        dsn_parts.append(f"channel_binding={normalize_channel_binding(channel_binding)}")
    if sslrootcert:
        dsn_parts.append(f"sslrootcert={sslrootcert.strip()}")
    elif normalized_sslmode in ("verify-full", "verify-ca"):
        certifi_bundle: Optional[str] = None
        try:
            import certifi  # type: ignore

            certifi_bundle = certifi.where()
        except Exception:
            certifi_bundle = None

        if certifi_bundle and Path(certifi_bundle).exists():
            dsn_parts.append(f"sslrootcert={certifi_bundle}")
        else:
            dsn_parts.append("sslrootcert=system")
    return " ".join(dsn_parts)


def resolve_connection_string(cli_value: Optional[str]) -> Optional[str]:
    value = cli_value or os.environ.get("POSTGRES_DSN") or os.environ.get("POSTGRES_CONNECTION_STRING")
    if not value:
        return None
    if ";" in value and ("host=" in value.lower() or "server=" in value.lower() or "database=" in value.lower()):
        return parse_ado_net_connection_string(value)
    return value


def require_psycopg():
    try:
        import psycopg  # type: ignore
    except ModuleNotFoundError as e:
        raise SystemExit(
            "Missing dependency: psycopg.\n"
            "Install with: `python -m pip install \"psycopg[binary]\"`\n"
            "Then rerun this script."
        ) from e
    return psycopg


def chunked(iterable: Iterable[ParsedRecipe], size: int) -> Iterable[list[ParsedRecipe]]:
    batch: list[ParsedRecipe] = []
    for item in iterable:
        batch.append(item)
        if len(batch) >= size:
            yield batch
            batch = []
    if batch:
        yield batch


def import_to_db(
    *,
    dsn: str,
    household_id: Optional[uuid.UUID],
    csv_path: Path,
    limit: Optional[int],
    batch_size: int,
    default_ingredient_unit: str,
    ingredient_default_unit: Optional[str],
    ingredient_default_days_to_expire: Optional[int],
    recipe_tag_type_name: str,
    top_n: int,
    mapping_out: Optional[Path],
) -> ImportStats:
    psycopg = require_psycopg()
    stats = ImportStats()

    ingredient_cache: dict[str, uuid.UUID] = {}
    tag_cache: dict[str, int] = {}

    mapping_file = None
    mapping_writer = None
    if mapping_out is not None:
        mapping_out.parent.mkdir(parents=True, exist_ok=True)
        mapping_file = mapping_out.open("w", encoding="utf-8", newline="")
        mapping_writer = csv.writer(mapping_file)
        mapping_writer.writerow(["source_id", "recipe_id", "title"])

    global_ingredient_vocab: Optional[set[str]] = None
    if top_n > 0:
        selected_recipes = select_top_recipes(csv_path=csv_path, limit=limit, top_n=top_n, stats=stats)
        global_ingredient_vocab = {ingredient_tag_slug(i) for r in selected_recipes for i in r.ingredient_names}
        global_ingredient_vocab.discard("")
        recipes_iterable: Iterable[ParsedRecipe] = selected_recipes
    else:
        recipes_iterable = iter_parsed_recipes(csv_path=csv_path, limit=limit, stats=stats)

    def resolve_target_household_id(cur) -> uuid.UUID:
        if household_id is not None:
            cur.execute("SELECT 1 FROM households WHERE id = %s", (str(household_id),))
            if cur.fetchone() is None:
                raise SystemExit(f"Household not found: {household_id}")
            return household_id

        # If not specified, import into a dedicated synthetic "System" household.
        system_clerk_user_id = "system_import"
        system_email = "system-import@pantrytales.local"
        system_nickname = "System Import"
        system_household_name = "System"

        cur.execute(
            """
            SELECT id
            FROM users
            WHERE clerk_user_id = %s OR email = %s
            LIMIT 1
            """,
            (system_clerk_user_id, system_email),
        )
        user_row = cur.fetchone()
        if user_row is not None:
            system_user_id = uuid.UUID(str(user_row[0]))
        else:
            system_user_id = uuid.uuid4()
            now = datetime.now(timezone.utc)
            cur.execute(
                """
                INSERT INTO users (id, clerk_user_id, nickname, email, created_at, updated_at)
                VALUES (%s, %s, %s, %s, %s, %s)
                """,
                (
                    str(system_user_id),
                    system_clerk_user_id,
                    system_nickname,
                    system_email,
                    now,
                    now,
                ),
            )

        cur.execute(
            """
            SELECT id
            FROM households
            WHERE owner_id = %s AND name = %s
            ORDER BY created_at ASC NULLS FIRST
            LIMIT 1
            """,
            (str(system_user_id), system_household_name),
        )
        existing_household = cur.fetchone()
        if existing_household is not None:
            return uuid.UUID(str(existing_household[0]))

        household_uuid = uuid.uuid4()
        cur.execute(
            """
            INSERT INTO households (id, name, owner_id)
            VALUES (%s, %s, %s)
            """,
            (str(household_uuid), system_household_name, str(system_user_id)),
        )

        cur.execute(
            """
            INSERT INTO household_members (household_id, user_id, role, display_name, email)
            VALUES (%s, %s, %s, %s, %s)
            ON CONFLICT DO NOTHING
            """,
            (str(household_uuid), str(system_user_id), "owner", system_nickname, system_email),
        )

        return household_uuid

    with psycopg.connect(dsn) as conn:
        conn.execute("SET statement_timeout TO '0'")

        target_household_id: uuid.UUID
        with conn.cursor() as cur:
            target_household_id = resolve_target_household_id(cur)

            cur.execute(
                """
                INSERT INTO tag_types (name, display_name, description)
                VALUES (%s, %s, %s)
                ON CONFLICT (name) DO NOTHING
                """,
                (recipe_tag_type_name, recipe_tag_type_name.capitalize(), "Imported recipe tags"),
            )

        for batch in chunked(recipes_iterable, batch_size):
            now = datetime.now(timezone.utc)

            batch_ingredient_names = sorted({i for r in batch for i in r.ingredient_names})

            # Notebook-style de-dupe: if a token exists in ingredient vocabulary, remove the recipe-tag dimension.
            if global_ingredient_vocab is not None:
                batch_recipe_tag_names = sorted(
                    {
                        t
                        for r in batch
                        for t in r.tag_names
                        if t not in global_ingredient_vocab and t not in _DIFFICULTY_TAG_SLUGS
                    }
                )
            else:
                batch_recipe_tag_names = sorted(
                    {
                        t
                        for r in batch
                        for t in r.tag_names
                        if t not in {ingredient_tag_slug(i) for i in r.ingredient_names} and t not in _DIFFICULTY_TAG_SLUGS
                    }
                )

            with conn.cursor() as cur:
                missing_ing = [n for n in batch_ingredient_names if n not in ingredient_cache]
                if missing_ing:
                    cur.execute(
                        "SELECT id, canonical_name FROM ingredients WHERE canonical_name = ANY(%s)",
                        (missing_ing,),
                    )
                    for row in cur.fetchall():
                        ingredient_cache[str(row[1])] = uuid.UUID(str(row[0]))

                to_create_ing = [n for n in missing_ing if n not in ingredient_cache]
                if to_create_ing:
                    rows = [
                        (
                            str(uuid.uuid4()),
                            n,
                            ingredient_default_unit,
                            ingredient_default_days_to_expire,
                        )
                        for n in to_create_ing
                    ]
                    cur.executemany(
                        """
                        INSERT INTO ingredients (id, canonical_name, default_unit, default_days_to_expire)
                        VALUES (%s, %s, %s, %s)
                        """,
                        rows,
                    )
                    for (new_id, canonical_name, _, _) in rows:
                        ingredient_cache[canonical_name] = uuid.UUID(new_id)
                    stats.created_ingredients += len(rows)

                missing = [n for n in batch_recipe_tag_names if n not in tag_cache]
                if missing:
                    cur.execute(
                        "SELECT id, name FROM tags WHERE type = %s AND name = ANY(%s)",
                        (recipe_tag_type_name, missing),
                    )
                    for row in cur.fetchall():
                        tag_cache[str(row[1])] = int(row[0])

                to_create = [n for n in missing if n not in tag_cache]
                for n in to_create:
                    cur.execute(
                        """
                        INSERT INTO tags (name, display_name, type, is_active, updated_at)
                        VALUES (%s, %s, %s, %s, %s)
                        ON CONFLICT (name, type) DO UPDATE
                        SET display_name = EXCLUDED.display_name,
                            is_active = EXCLUDED.is_active,
                            updated_at = EXCLUDED.updated_at
                        RETURNING id, name
                        """,
                        (n, tag_display_name(n), recipe_tag_type_name, True, now),
                    )
                    tag_id, name = cur.fetchone()
                    tag_cache[str(name)] = int(tag_id)
                    stats.created_tags += 1

                recipes_rows = []
                recipe_ingredient_rows = []
                recipe_tag_rows = []

                for r in batch:
                    recipe_id = uuid.uuid4()
                    recipes_rows.append(
                        (
                            str(recipe_id),
                            str(target_household_id),
                            "System",
                            r.title,
                            r.description,
                            r.total_time_minutes,
                            r.calories,
                            r.fat,
                            r.sugar,
                            r.sodium,
                            r.protein,
                            r.saturated_fat,
                            r.carbohydrates,
                            r.difficulty,
                            json.dumps(r.steps, ensure_ascii=False),
                            "Public",
                            0,
                            0,
                            0,
                            "Pending",
                            now,
                        )
                    )

                    if mapping_writer is not None:
                        mapping_writer.writerow([r.source_id, str(recipe_id), r.title])

                    for ing_name in r.ingredient_names:
                        ing_id = ingredient_cache.get(ing_name)
                        if ing_id is None:
                            raise RuntimeError(f"Missing ingredient id for '{ing_name}' (should not happen)")
                        recipe_ingredient_rows.append(
                            (
                                str(uuid.uuid4()),
                                str(recipe_id),
                                str(ing_id),
                                1,
                                default_ingredient_unit,
                                False,
                                now,
                            )
                        )

                    if global_ingredient_vocab is not None:
                        recipe_tag_names = [
                            t
                            for t in r.tag_names
                            if t not in global_ingredient_vocab and t not in _DIFFICULTY_TAG_SLUGS
                        ]
                    else:
                        ingredient_tag_names = {ingredient_tag_slug(i) for i in r.ingredient_names}
                        ingredient_tag_names.discard("")
                        recipe_tag_names = [
                            t for t in r.tag_names if t not in ingredient_tag_names and t not in _DIFFICULTY_TAG_SLUGS
                        ]
                    for rec_tag in recipe_tag_names:
                        tag_id = tag_cache.get(rec_tag)
                        if tag_id is not None:
                            recipe_tag_rows.append((str(recipe_id), int(tag_id)))

                cur.executemany(
                    """
                    INSERT INTO recipes (
                        id,
                        household_id,
                        type,
                        title,
                        description,
                        total_time_minutes,
                        calories,
                        fat,
                        sugar,
                        sodium,
                        protein,
                        saturated_fat,
                        carbohydrates,
                        difficulty,
                        steps,
                        visibility,
                        likes_count,
                        comments_count,
                        saved_count,
                        embedding_status,
                        updated_at
                    )
                    VALUES (
                        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s::jsonb, %s, %s, %s, %s, %s, %s
                    )
                    """,
                    recipes_rows,
                )

                cur.executemany(
                    """
                    INSERT INTO recipe_ingredients (
                        id, recipe_id, ingredient_id, amount, unit, is_optional, created_at
                    )
                    VALUES (%s, %s, %s, %s, %s, %s, %s)
                    """,
                    recipe_ingredient_rows,
                )

                if recipe_tag_rows:
                    cur.executemany(
                        """
                        INSERT INTO recipe_tags (recipe_id, tag_id)
                        VALUES (%s, %s)
                        ON CONFLICT DO NOTHING
                        """,
                        recipe_tag_rows,
                    )

            conn.commit()

            stats.imported_recipes += len(batch)
            stats.linked_recipe_ingredients += len(recipe_ingredient_rows)
            stats.linked_recipe_tags += len(recipe_tag_rows)

    if mapping_file is not None:
        mapping_file.close()

    return stats


def dry_run(csv_path: Path, limit: Optional[int]) -> ImportStats:
    stats = ImportStats()
    with csv_path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            if limit is not None and stats.read_rows >= limit:
                break
            _ = parse_recipe_row(row, stats)
    return stats


def resolve_existing_path(path: Path) -> tuple[Path, list[Path]]:
    """
    Resolve a possibly-relative CLI path in a way that's stable regardless of CWD.

    Tries, in order:
    - as provided (relative to current working dir)
    - relative to this script's directory
    - relative to repo root (parent of scripts/)
    - if it starts with "scripts/", strip that and retry from repo root / script dir
    """

    if path.is_absolute():
        return path, [path]

    tried: list[Path] = [path]
    if path.exists():
        return path, tried

    candidates: list[Path] = [SCRIPT_DIR / path, REPO_ROOT / path]

    if path.parts and path.parts[0] == "scripts":
        stripped = Path(*path.parts[1:])
        candidates.extend([REPO_ROOT / stripped, SCRIPT_DIR / stripped])

    for candidate in candidates:
        tried.append(candidate)
        if candidate.exists():
            return candidate, tried

    return path, tried


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Import RAW_recipes.csv into PantryTales Postgres DB.")
    p.add_argument(
        "--csv",
        dest="csv_path",
        type=Path,
        default=SCRIPT_DIR / "data" / "RAW_recipes.csv",
        help="Path to RAW_recipes.csv",
    )
    p.add_argument("--limit", type=int, default=None, help="Only process N rows from the CSV (for testing).")
    p.add_argument("--batch-size", type=int, default=200, help="Commit every N recipes.")
    p.add_argument(
        "--top-n",
        type=int,
        default=2000,
        help="Keep only top N recipes by quality (maximin over dimensions).",
    )

    p.add_argument(
        "--dry-run",
        action="store_true",
        help="Parse + clean only (no DB writes).",
    )

    p.add_argument("--connection", default=None, help="Postgres DSN (or ADO.NET ConnectionStrings:Postgres format).")
    p.add_argument(
        "--household-id",
        default=None,
        help="Optional target household UUID for imported recipes (auto-select/create if omitted).",
    )
    p.add_argument(
        "--default-ingredient-unit",
        default="unit",
        help="Placeholder unit for imported recipe_ingredients.",
    )
    p.add_argument(
        "--ingredient-default-days-to-expire",
        type=int,
        default=None,
        help="Optional default_days_to_expire for newly created ingredients (NULL if omitted).",
    )
    p.add_argument(
        "--ingredient-default-unit",
        default=None,
        help="Optional default_unit for newly created ingredients (NULL if omitted).",
    )
    p.add_argument(
        "--tag-type",
        "--recipe-tag-type",
        dest="recipe_tag_type",
        default="recipe",
        help="TagType/Tag.Type name used for imported recipe tags.",
    )
    p.add_argument(
        "--mapping-out",
        type=Path,
        default=SCRIPT_DIR / "data" / "imported_recipes_map.csv",
        help="Write a mapping file (source_id -> recipe_id) here.",
    )
    return p


def print_stats(stats: ImportStats) -> None:
    print(f"read_rows={stats.read_rows}")
    print(f"selected_recipes={stats.selected_recipes}")
    print(f"imported_recipes={stats.imported_recipes}")
    print(f"skipped_rows={stats.skipped_rows}")
    if stats.skip_reasons:
        for k in sorted(stats.skip_reasons):
            print(f"  skipped[{k}]={stats.skip_reasons[k]}")
    print(f"created_ingredients={stats.created_ingredients}")
    print(f"created_tags={stats.created_tags}")
    print(f"linked_recipe_ingredients={stats.linked_recipe_ingredients}")
    print(f"linked_recipe_tags={stats.linked_recipe_tags}")


def main() -> int:
    args = build_parser().parse_args()

    csv_path, tried = resolve_existing_path(args.csv_path)
    if not csv_path.exists():
        tried_str = "\n".join(f"  - {p}" for p in tried)
        print(f"CSV not found: {args.csv_path}\nTried:\n{tried_str}", file=sys.stderr)
        return 2

    if args.dry_run:
        stats = ImportStats()
        if int(args.top_n) > 0:
            _ = select_top_recipes(csv_path=csv_path, limit=args.limit, top_n=int(args.top_n), stats=stats)
        else:
            for _ in iter_parsed_recipes(csv_path, args.limit, stats):
                pass
        print_stats(stats)
        return 0

    dsn = resolve_connection_string(args.connection)
    if not dsn:
        print(
            "Missing connection string. Provide --connection or set POSTGRES_DSN / POSTGRES_CONNECTION_STRING.",
            file=sys.stderr,
        )
        return 2

    household_id = uuid.UUID(args.household_id) if args.household_id else None

    stats = import_to_db(
        dsn=dsn,
        household_id=household_id,
        csv_path=csv_path,
        limit=args.limit,
        batch_size=args.batch_size,
        default_ingredient_unit=str(args.default_ingredient_unit).strip() or "unit",
        ingredient_default_unit=(args.ingredient_default_unit.strip() or None) if args.ingredient_default_unit else None,
        ingredient_default_days_to_expire=args.ingredient_default_days_to_expire,
        recipe_tag_type_name=str(args.recipe_tag_type).strip().lower() or "recipe",
        top_n=int(args.top_n),
        mapping_out=args.mapping_out,
    )
    print_stats(stats)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
