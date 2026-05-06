# Recipe Recommendation Best Practices (PantryTales)

> Goal: deliver a “useful” recipe recommender that is engineering-feasible, iterative, and measurable—rather than trying to ship a perfect model in one shot.

This doc provides a general set of best practices along a “simple → complex” path, and tailors them to your current stack (Postgres + pgvector, `recipe_likes`, `user_preferences`, `tags/ingredients`, etc.) with actionable implementation suggestions.

---

## 1) A Layered Recommendation Architecture (Best Practice)

**A recommender is typically split into two layers:**

1. **Candidate retrieval**: quickly fetch 100–2000 candidates from a large recipe catalog.
2. **Ranking / re-ranking**: score candidates more carefully and output the final Top-K (e.g., 20).

Benefits: speed, explainability, modularity, and incremental upgrade paths.

---

## 2) Strong Recommendation: Hybrid (Rules + Content + Collaborative), Not a Single Method

**Don’t rely only on embeddings or only on tags**. Best practice is hybrid:

- **Rules layer (hard constraints)**: allergies/restrictions, time budget, must-include ingredients, blacklists (filter first, then recommend).
- **Content-based similarity**: semantic similarity across recipes/ingredients/tags/steps (embeddings or TF‑IDF).
- **Collaborative signals**: user↔item interactions (like/save/click/cook) to capture crowd preference.
- **Re-ranking policies**: diversity, freshness, exploration.

---

## 3) Data Modeling (Schema): Minimum Necessary + Iterative

### 3.1 Essential interaction events (fuel for the recommender)

With only `recipe_likes`, it’s hard to build a strong recommender. Consider gradually adding more event types (you don’t need to implement everything on day one):

- `recipe_interactions` (recommended new table)
  - `user_id`, `recipe_id`
  - `event_type`: `impression/click/open/save/like/unlike/dwell/cook/share`
  - `created_at`
  - optional: `session_id`, `source` (home/search/details placement)

**Why**: likes are sparse (cold-start + slow learning). Impressions/clicks are earlier and denser signals.

### 3.2 Item features and embeddings

You already have:
- `recipes.embedding` (vector)
- `ingredients.embedding` (vector)
- `users.embedding` (field added)

**Best practice**:
- recipe embedding: derived from combined text of `title + description + steps + ingredients/tags` (ideally via offline batch).
- ingredient embedding: derived from canonical name + aliases (if any) + optional external knowledge.
- user embedding: weighted aggregation of historically liked/saved/clicked recipes (or train a two-tower model later).

### 3.3 Preferences and constraints

You already have `user_preferences(user_id, tag_id, relation)`—a great starting point.

It helps to clearly separate:
- **Hard constraints**: allergy/restriction (must filter out)
- **Soft preferences**: like/dislike/goals (contribute to scoring)

---

## 4) Cold Start Is Priority #1

### 4.1 New users

Best practice:
- onboarding to pick a few preference tags (`user_preferences`)
- allow users to enter existing ingredients (inventory) as hard constraints
- recommend a blended list: “popular/high-quality” + “diverse”

### 4.2 New recipes

Best practice:
- as soon as content exists (title/steps/ingredients/tags), generate an embedding and join content-based retrieval immediately
- once impressions/clicks exist, bring it into collaborative components

---

## 5) Candidate Retrieval: Three Parallel Channels (Recommended)

In retrieval, you usually run multiple channels in parallel, then union + dedupe.

### 5.1 Content-based retrieval (pgvector)

**Query vector** sources:
- user vector (`users.embedding`)
- or a session/search query vector (e.g., from selected ingredients/tags)

With pgvector:
- `ORDER BY embedding <-> :query LIMIT 500`

### 5.2 Tags/ingredients retrieval (structured)

Use `recipe_tags` and `recipe_ingredients` for filtering/weighting:
- must-include ingredients (top 2–5 most important from inventory)
- match count/weight for user preference tags

### 5.3 Popularity/freshness retrieval (Pop/Fresh)

Use the last 7/30 days of events (like/save/click) to compute trending:
- `score = w1*likes + w2*saves + w3*clicks - decay(age)`

---

## 6) Ranking/Re-ranking: Start Simple and Explainable

For a university project, the safest path is:

### 6.1 Ranking (interpretable linear scoring)

Compute a few components for each candidate recipe:
- `sim_user_recipe` (vector similarity)
- `tag_match_score` (preference tag match)
- `ingredient_match_score` (ingredient coverage from inventory)
- `popularity_score` (trend score)
- `novelty_penalty` (penalize repeats / near-duplicates)

Combine:
```
final = a*sim + b*tag + c*ingredient + d*pop - e*repetition
```

### 6.2 Re-ranking: diversity

Best practice: run MMR/greedy de-similarization on Top‑N (e.g., 100):
- avoid “10 variants of chicken breast”
- keep cuisine / cooking time / tags diverse

---

## 7) Two Common Ways to Build “User Embeddings”

### 7.1 Rule-based aggregation (easiest to ship)

`user_embedding = normalize( sum( w(event)*recipe_embedding ) )`

Example weights (tunable):
- like/save：`1.0`
- click/open：`0.3`
- impression：`0.05`

Update strategy:
- daily batch + incremental updates after like/save

### 7.2 Two-tower / implicit feedback (stronger, higher cost)

If you have enough events (especially click/impression), you can train:
- user tower: user history sequence/preferences
- item tower: recipe content

Objectives:
- BPR / sampled softmax

For a university project, 7.1 alone is often enough to feel “very usable”.

---

## 8) Evaluation and Iteration (Required; Otherwise It’s Guesswork)

### 8.1 Offline

- Recall@K / NDCG@K (based on held-out like/save)
- Coverage (fraction of distinct recipes that get recommended)
- Diversity (similarity distribution within the recommendation list)

### 8.2 Online (or quasi-online)

If you don’t have A/B infrastructure, at least log:
- the funnel: impression → click → save/like
- placement metrics: CTR, save rate, like rate
- overly repetitive / poor-feedback content (negative signals)

---

## 9) Minimum Viable Roadmap for Your Current Project (Suggested)

1. **Instrumentation**: add `recipe_interactions` (impression/click/save/like).
2. **User vectors**: start with rule-based aggregation (weighted like/save/click) and write to `users.embedding`.
3. **Multi-channel retrieval**: vector retrieval + tag/ingredient retrieval + pop/fresh retrieval, then merge + dedupe.
4. **Explainable ranking**: linear scoring + basic diversity re-ranking.
5. **Continuous evaluation**: at least offline Recall@K and online funnel logging.

---

## 10) How to Treat “Brands/Packaging/Promo Words” (Recommender Perspective)

In recipe recommendation, brand is usually not a core signal (unless you’re building shopping/repeat-purchase/brand-preference features).

Best practice:
- keep `ingredients` as the canonical vocabulary (no brands)
- keep raw item names on the inventory/checklist side for display/audit
- use an async resolver to map items → ingredients (brand cleaning is just one feature/step in the resolver)

---

## 11) Iterative Name Normalization Dictionary (DB Dictionary + Versioning)

When you can’t enumerate all “brand/packaging/promo/size” noise tokens upfront, it’s best to turn normalization into a data pipeline that is **configurable, auditable, and re-runnable**:

### 11.1 Dictionary table

- `name_normalization_tokens`
  - `token`: token to remove/transform (store lowercase)
  - `category`: `Brand/Unit/Packaging/Promo/Noise`
  - `is_active`: whether enabled (supports gradual rollout/rollback)
  - `language` / `source`: optional (trace origin: manual/learned/seed)

### 11.2 Version table

- `name_normalization_dictionary_versions`
  - single-row table (`id = 1`)
  - `dictionary_version`: bump by +1 after enabling/disabling/adding/modifying tokens
  - `algorithm_version`: bump by +1 when the normalization logic changes

### 11.3 Track which version was used

For example on `inventory_items` / `ingredient_aliases`:
- `normalized_name`
- `name_normalization_dictionary_version`
- `name_normalization_algorithm_version`
- `name_normalization_removed_tokens` (optional, jsonb, useful for debugging)

### 11.4 Incremental re-processing (avoid full-table reruns)

Filter conditions for the normalization job:
- `name_normalization_dictionary_version IS NULL OR name_normalization_dictionary_version < current_dictionary_version`
- or `name_normalization_algorithm_version IS NULL OR name_normalization_algorithm_version < current_algorithm_version`

This way, even a small initial dictionary won’t block writes; as the dictionary grows, the system automatically “catches up” on historical data.

## Appendix: Common Pitfalls

- Embeddings only: low explainability, poor cold-start, hard to handle constraints
- Tags only: low ceiling, weak semantic generalization
- Likes only: signals are too sparse, learning is slow
- No impressions: you can’t compute CTR
- No diversity: users feel recommendations are repetitive and boring
