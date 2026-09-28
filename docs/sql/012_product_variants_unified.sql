-- NUBulldogsExchange: Unified product variants (color and/or size)
-- Run in Supabase SQL Editor AFTER 001_product_variants.sql and 009_product_variant_colors.sql.
-- Reuses public.product_variants. Does not create a new table.
--
-- Original 001 schema: size text NOT NULL, UNIQUE (product_id, size)
-- 009 already added color_name/color_hex and replaced UNIQUE (product_id, size)
-- with ux_product_variants_product_size_color so Navy/M and Black/M can coexist.
--
-- This script:
-- 1) Makes size nullable so color-only rows can store NULL instead of ''.
-- 2) Drops leftover UNIQUE (product_id, size) if 009 was not applied.
-- Existing size-only rows (blank color + S/M/L/XL) are unchanged.

ALTER TABLE public.product_variants
    ALTER COLUMN size DROP NOT NULL;

DO $$
DECLARE
    constraint_name text;
BEGIN
    SELECT c.conname
      INTO constraint_name
      FROM pg_constraint c
      JOIN pg_class t ON t.oid = c.conrelid
      JOIN pg_namespace n ON n.oid = t.relnamespace
     WHERE n.nspname = 'public'
       AND t.relname = 'product_variants'
       AND c.contype = 'u'
       AND pg_get_constraintdef(c.oid) ILIKE '%(product_id, size)%'
       AND pg_get_constraintdef(c.oid) NOT ILIKE '%color%'
     LIMIT 1;

    IF constraint_name IS NOT NULL THEN
        EXECUTE format(
            'ALTER TABLE public.product_variants DROP CONSTRAINT %I',
            constraint_name
        );
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_product_variants_product_size_color
    ON public.product_variants (
        product_id,
        lower(btrim(coalesce(size, ''))),
        lower(btrim(coalesce(color_name, '')))
    );
