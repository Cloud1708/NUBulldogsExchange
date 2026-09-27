-- NUBulldogsExchange: Color + size sellable variants (Web)
-- Run in Supabase SQL Editor AFTER 001_product_variants.sql.
-- Non-destructive: ADD COLUMN IF NOT EXISTS, no data deletes.
-- Shared schema is used by Web and Mobile. This script does not require
-- any Mobile source changes. Existing size-only rows stay valid.
--
-- Current constraint from 001_product_variants.sql:
--   UNIQUE (product_id, size)
-- That blocks Navy/M and Black/M. Replace it with a combo unique that
-- treats NULL/blank color as '' so size-only rows stay unique per size.

ALTER TABLE public.product_variants
    ADD COLUMN IF NOT EXISTS color_name text;

ALTER TABLE public.product_variants
    ADD COLUMN IF NOT EXISTS color_hex text;

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

ALTER TABLE public.order_items
    ADD COLUMN IF NOT EXISTS color_name text NULL;

-- Optional: persist p_items[].selected_color onto order_items.color_name
-- inside place_checkout_order so admin color renames do not rewrite history.
