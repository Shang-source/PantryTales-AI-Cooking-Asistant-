-- Seed script for ingredient type tags
-- Run this against your PostgreSQL database to add ingredient categories
--
-- Usage: psql -d your_database -f seed_ingredient_types.sql
-- Or via Docker: docker exec -i pantry-tales-db psql -U postgres -d pantry_tales < seed_ingredient_types.sql

-- First, create the tag type if it doesn't exist
INSERT INTO tag_types (name, display_name, description, created_at, updated_at)
SELECT 'ingredient_type', 'Ingredient Type', 'Category classification for ingredients (e.g., Vegetables, Fruits, Meat)', NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM tag_types WHERE name = 'ingredient_type');

-- Update if it does exist
UPDATE tag_types SET
    display_name = 'Ingredient Type',
    description = 'Category classification for ingredients (e.g., Vegetables, Fruits, Meat)',
    updated_at = NOW()
WHERE name = 'ingredient_type';

-- Now insert the ingredient type tags using a function for upsert
-- These match the CATEGORIES in mobile/constants/dropdownValue.ts

DO $$
DECLARE
    v_tag RECORD;
BEGIN
    FOR v_tag IN
        SELECT * FROM (VALUES
            ('vegetables', 'Vegetables', 'leaf', '#22c55e'),
            ('fruits', 'Fruits', 'apple', '#f97316'),
            ('meat', 'Meat', 'drumstick', '#ef4444'),
            ('seafood', 'Seafood', 'fish', '#3b82f6'),
            ('dairy', 'Dairy', 'milk', '#fbbf24'),
            ('grains', 'Grains', 'wheat', '#a3a3a3'),
            ('spices', 'Spices', 'flame', '#dc2626'),
            ('beverages', 'Beverages', 'cup-soda', '#06b6d4'),
            ('snacks', 'Snacks', 'cookie', '#f472b6'),
            ('other', 'Other', 'package', '#737373')
        ) AS t(name, display_name, icon, color)
    LOOP
        -- Try to insert, if exists then update
        IF EXISTS (SELECT 1 FROM tags WHERE name = v_tag.name AND type = 'ingredient_type') THEN
            UPDATE tags SET
                display_name = v_tag.display_name,
                icon = v_tag.icon,
                color = v_tag.color,
                updated_at = NOW()
            WHERE name = v_tag.name AND type = 'ingredient_type';
        ELSE
            INSERT INTO tags (name, display_name, type, icon, color, is_active, created_at, updated_at)
            VALUES (v_tag.name, v_tag.display_name, 'ingredient_type', v_tag.icon, v_tag.color, true, NOW(), NOW());
        END IF;
    END LOOP;
END $$;

-- Verify the insertion
SELECT t.id, t.name, t.display_name, t.type, t.icon, t.color
FROM tags t
WHERE t.type = 'ingredient_type'
ORDER BY t.display_name;
