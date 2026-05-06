-- Quick fix: Insert just the ingredient_type tag type
-- Run this to add the missing tag type entry

INSERT INTO tag_types (name, display_name, description, created_at, updated_at)
VALUES (
    'ingredient_type',
    'Ingredient Type',
    'Category classification for ingredients (e.g., Vegetables, Fruits, Meat)',
    NOW(),
    NOW()
);

-- Verify
SELECT * FROM tag_types WHERE name = 'ingredient_type';
