-- Fix: Admin Edit Product fails with
--   new row violates row-level security policy for table "product_variants"
-- Cause: product_variants is missing (or has a broken) admin write policy.
-- Run this in the Supabase SQL Editor, then retry Save Changes.

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.product_variants
    TO anon, authenticated;
GRANT ALL ON TABLE public.product_variants TO service_role;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'product_variants_id_seq'
          AND c.relkind = 'S'
    ) THEN
        GRANT USAGE, SELECT ON SEQUENCE public.product_variants_id_seq
            TO anon, authenticated, service_role;
    END IF;
END $$;

ALTER TABLE public.product_variants ENABLE ROW LEVEL SECURITY;

-- Public/customer read of active variants (storefront).
DROP POLICY IF EXISTS product_variants_select_active ON public.product_variants;
CREATE POLICY product_variants_select_active
    ON public.product_variants
    FOR SELECT
    TO authenticated, anon
    USING (status = 'Active');

-- Admin/staff full access (same pattern as products_admin_all / size_guides).
DROP POLICY IF EXISTS product_variants_admin_all ON public.product_variants;
CREATE POLICY product_variants_admin_all
    ON public.product_variants
    FOR ALL
    TO authenticated
    USING (
        EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id::text = auth.uid()::text
              AND lower(u.role) IN ('admin', 'staff')
        )
    )
    WITH CHECK (
        EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id::text = auth.uid()::text
              AND lower(u.role) IN ('admin', 'staff')
        )
    );
