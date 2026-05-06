#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from typing import Iterable

from name_normalization import DEFAULT_PACKAGING_TOKENS, DEFAULT_PROMO_TOKENS, DEFAULT_UNIT_TOKENS, _tokenize


def iter_names_from_csv(path: Path, column: str) -> Iterable[str]:
    with path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            value = (row.get(column) or "").strip()
            if value:
                yield value


def iter_names_from_lines(path: Path) -> Iterable[str]:
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            value = line.strip()
            if value:
                yield value


def shannon_entropy(counts: Counter[str]) -> float:
    total = sum(counts.values())
    if total <= 0:
        return 0.0
    h = 0.0
    for c in counts.values():
        p = c / total
        h -= p * math.log(p + 1e-12)
    return h


def learn_brand_prefixes(
    names: Iterable[str],
    *,
    min_starts: int,
    max_prefix_tokens: int,
    min_entropy: float,
    top_k: int,
) -> list[str]:
    """
    Heuristic:
    - take first 1..N tokens of each name (after basic tokenization)
    - count how often each prefix occurs at the beginning
    - measure diversity of what follows that prefix (entropy of next token)
    - keep prefixes that occur often AND have high next-token entropy (brand-like)
    """

    unit = {t.lower() for t in DEFAULT_UNIT_TOKENS}
    packaging = {t.lower() for t in DEFAULT_PACKAGING_TOKENS}
    promo = {t.lower() for t in DEFAULT_PROMO_TOKENS}
    blocked = unit | packaging | promo

    starts_count: Counter[str] = Counter()
    next_token_counts: dict[str, Counter[str]] = defaultdict(Counter)

    for raw in names:
        tokens = [t for t in _tokenize(raw) if t and t not in blocked]
        if len(tokens) < 2:
            continue

        for n in range(1, max_prefix_tokens + 1):
            if len(tokens) <= n:
                continue
            prefix = " ".join(tokens[:n])
            nxt = tokens[n]
            starts_count[prefix] += 1
            next_token_counts[prefix][nxt] += 1

    scored: list[tuple[float, int, str]] = []
    for prefix, count in starts_count.items():
        if count < min_starts:
            continue
        ent = shannon_entropy(next_token_counts[prefix])
        if ent < min_entropy:
            continue
        # score favors: high entropy, high count, shorter prefixes
        score = ent * math.log(count + 1)
        scored.append((score, count, prefix))

    scored.sort(reverse=True)
    return [p for (_, _, p) in scored[:top_k]]


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Learn brand-like prefixes from a list of product names.")
    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument("--csv", type=Path, help="CSV file containing a column of item names.")
    src.add_argument("--lines", type=Path, help="Text file with one item name per line.")
    p.add_argument("--column", default="name", help="CSV column name to read (default: name).")
    p.add_argument("--min-starts", type=int, default=200, help="Min times a prefix must appear at start.")
    p.add_argument("--max-prefix-tokens", type=int, default=2, help="Max tokens for a brand prefix.")
    p.add_argument("--min-entropy", type=float, default=2.0, help="Min entropy of following token distribution.")
    p.add_argument("--top-k", type=int, default=200, help="How many prefixes to output.")
    p.add_argument(
        "--out",
        type=Path,
        default=Path("scripts/data/brand_prefixes.json"),
        help="Output JSON file path.",
    )
    return p


def main() -> int:
    args = build_parser().parse_args()
    if args.csv:
        names = iter_names_from_csv(args.csv, args.column)
    else:
        names = iter_names_from_lines(args.lines)

    prefixes = learn_brand_prefixes(
        names,
        min_starts=int(args.min_starts),
        max_prefix_tokens=int(args.max_prefix_tokens),
        min_entropy=float(args.min_entropy),
        top_k=int(args.top_k),
    )

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps({"brand_prefixes": prefixes}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {len(prefixes)} brand_prefixes to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

