## Scripts

This folder contains utilities for working with PantryTales data and the sample Food.com recipe dataset.

## Setup

- Python: `>= 3.12`
- Install dependencies (one of):
  - `python3 -m pip install -r scripts/requirements.txt`
  - If you use `uv`: `uv pip install -r scripts/requirements.txt`

For DB imports, also install: `python3 -m pip install "psycopg[binary]"`

## Recipe Import

Imports `scripts/data/RAW_recipes.csv` into the Postgres schema defined in `backend/Models`:
- `recipes` (including nutrition columns if present)
- `ingredients`
- `recipe_ingredients`
- `tags` / `recipe_tags` (recipe tags only)

Prereqs:
1) Your DB schema is up-to-date (migrations applied).

```bash
python3 scripts/import_recipes.py \
  --connection "<Postgres DSN or ADO.NET ConnectionStrings:Postgres>"
```

You can also run it from inside `scripts/` (the script resolves `--csv` relative to the script/repo, not just your current directory).

Notes:
- `--household-id` is optional. If omitted, the script imports into a synthetic "System" household (created on-demand).

Defaults:
- Imports top 2000 recipes by "maximin quality" (best worst-dimension across description/steps/ingredients/time/tags). Use `--top-n 0` to import all.
- Creates tags of type `recipe` (from CSV tags).
- Promotes `easy` / `medium` / `hard` CSV tags into `recipes.difficulty` (and does not import those as recipe tags).
- De-dupes overlap (notebook-style): if a token exists in ingredient vocabulary, the recipe tag is skipped.
- Imports nutrition columns into `recipes` when present in DB (`calories`, `fat`, `sugar`, `sodium`, `protein`, `saturated_fat`, `carbohydrates`).
- Newly created `ingredients.default_unit/default_days_to_expire` are `NULL` unless you pass `--ingredient-default-unit` / `--ingredient-default-days-to-expire`.
- `recipe_ingredients.amount/unit` are placeholders (`amount=1`, `unit=--default-ingredient-unit`, default `"unit"`), because the CSV does not contain quantities.

To preview cleaning/skip stats without touching the DB:

```bash
python3 scripts/import_recipes.py --dry-run --limit 1000 --top-n 2000
```

## Recipe Search (Streamlit)

The demo UI reads precomputed parquet files under `scripts/data/`:

```bash
cd scripts
streamlit run recipe_search.py
```

## Name Normalization (Inventory)

When ingesting real-world grocery item names (often containing brand/packaging/promo words), use a 2-layer approach:
- Keep the raw string for display/audit.
- Derive a normalized string for matching (units/packaging/promo removed), and optionally strip a learned brand prefix list.

Helper module:
- `scripts/name_normalization.py` (`normalize_product_name`)

Learn brand-like prefixes from your own names dataset (no manual labeling, heuristic/approximate):

```bash
# CSV with a `name` column
python3 scripts/learn_brand_prefixes.py --csv path/to/items.csv --column name --out scripts/data/brand_prefixes.json
```

Then load `brand_prefixes.json` and pass it to `normalize_product_name(..., brand_prefixes=...)` in your resolver/job.
