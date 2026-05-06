from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from typing import Iterable, Optional, Sequence


@dataclass(frozen=True, slots=True)
class NormalizationResult:
    raw: str
    normalized: str
    brand_prefix: Optional[str]
    removed_tokens: tuple[str, ...]


_WS_RE = re.compile(r"\s+")
_BRACKET_RE = re.compile(r"[\(\[\{（【].*?[\)\]\}）】]")
_PUNCT_RE = re.compile(r"[^\w\s]+", flags=re.UNICODE)
_NUM_RE = re.compile(r"^\d+(\.\d+)?$")
_NUM_UNIT_RE = re.compile(r"^\d+(\.\d+)?(g|kg|mg|ml|l|oz|lb|pcs|pc|pack|packs)$")
_MULTIPLIER_RE = re.compile(r"^(\d+)\s*[x×]\s*(\d+(\.\d+)?)(g|kg|mg|ml|l|oz|lb)$")


DEFAULT_UNIT_TOKENS = {
    # metric / imperial
    "g",
    "kg",
    "mg",
    "ml",
    "l",
    "oz",
    "lb",
    # packaging units
    "pack",
    "packs",
    "pcs",
    "pc",
    "bottle",
    "bottles",
    "can",
    "cans",
    "jar",
    "jars",
    "bag",
    "bags",
    "box",
    "boxes",
    "tin",
    "tins",
    # zh-ish
    "克",
    "千克",
    "公斤",
    "毫升",
    "升",
    "袋",
    "包",
    "瓶",
    "罐",
    "盒",
}

DEFAULT_PACKAGING_TOKENS = {
    # generic packaging / descriptors
    "family",
    "size",
    "value",
    "bundle",
    "multipack",
    "assorted",
    "variety",
    "mini",
    "large",
    "small",
    # temperature / storage
    "frozen",
    "chilled",
    "fresh",
    # zh-ish
    "大包装",
    "家庭装",
    "特大",
    "小包装",
}

DEFAULT_PROMO_TOKENS = {
    # promo / marketing
    "new",
    "sale",
    "promo",
    "free",
    "limited",
    "edition",
    "best",
    "premium",
    "classic",
    # diet claims
    "organic",
    "gluten",
    "glutenfree",
    "sugarfree",
    "lowfat",
    "nonsugar",
    # zh-ish
    "新品",
    "促销",
    "特价",
    "买一送一",
    "有机",
    "无糖",
}


def _tokenize(raw: str) -> list[str]:
    text = unicodedata.normalize("NFKC", raw).lower()
    text = _BRACKET_RE.sub(" ", text)
    text = text.replace("_", " ").replace("-", " ")
    text = _PUNCT_RE.sub(" ", text)
    text = _WS_RE.sub(" ", text).strip()
    return [t for t in text.split(" ") if t]


def normalize_product_name(
    raw: str,
    *,
    brand_prefixes: Optional[Sequence[str]] = None,
    unit_tokens: Iterable[str] = DEFAULT_UNIT_TOKENS,
    packaging_tokens: Iterable[str] = DEFAULT_PACKAGING_TOKENS,
    promo_tokens: Iterable[str] = DEFAULT_PROMO_TOKENS,
    drop_leading_brand: bool = True,
) -> NormalizationResult:
    """
    Normalizes a potentially branded retail-ish product name into an ingredient-like name for matching.

    Important: this is *heuristic* and should be used for matching; always keep the original raw string too.
    """

    tokens = _tokenize(raw)
    removed: list[str] = []

    unit_set = {t.lower() for t in unit_tokens}
    packaging_set = {t.lower() for t in packaging_tokens}
    promo_set = {t.lower() for t in promo_tokens}
    brand_set = {t.lower() for t in (brand_prefixes or [])}

    # Remove obvious numeric/size tokens first
    cleaned: list[str] = []
    for idx, t in enumerate(tokens):
        prev_tok = tokens[idx - 1] if idx > 0 else None
        next_tok = tokens[idx + 1] if idx + 1 < len(tokens) else None

        if t in {"x", "×"} and (prev_tok and _NUM_RE.match(prev_tok) or next_tok and (_NUM_RE.match(next_tok) or _NUM_UNIT_RE.match(next_tok))):
            removed.append(t)
            continue

        if _NUM_RE.match(t) or _NUM_UNIT_RE.match(t) or _MULTIPLIER_RE.match(t):
            removed.append(t)
            continue
        if t in unit_set or t in packaging_set or t in promo_set:
            removed.append(t)
            continue
        cleaned.append(t)

    brand_prefix: Optional[str] = None
    if drop_leading_brand and cleaned:
        # Support 1-3 token brand prefixes (e.g. "woolworths", "countdown", "tesco finest")
        for n in (3, 2, 1):
            if len(cleaned) >= n:
                candidate = " ".join(cleaned[:n])
                if candidate in brand_set:
                    brand_prefix = candidate
                    removed.extend(cleaned[:n])
                    cleaned = cleaned[n:]
                    break

    # Dedupe while keeping order
    seen: set[str] = set()
    normalized_tokens: list[str] = []
    for t in cleaned:
        if t not in seen:
            seen.add(t)
            normalized_tokens.append(t)

    normalized = " ".join(normalized_tokens).strip()
    return NormalizationResult(raw=raw, normalized=normalized, brand_prefix=brand_prefix, removed_tokens=tuple(removed))
