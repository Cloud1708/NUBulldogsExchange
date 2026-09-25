-- Allow Admin/Staff to INSERT/UPDATE/DELETE public.products.
-- Run in Supabase SQL Editor if Add Product does not persist.
-- Does not drop existing customer SELECT policies.

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.products TO authenticated;
GRANT ALL ON TABLE public.products TO service_role;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'products_id_seq'
          AND c.relkind = 'S'
    ) THEN
        GRANT USAGE, SELECT ON SEQUENCE public.products_id_seq
            TO authenticated, service_role;
    END IF;
END $$;

ALTER TABLE public.products ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS products_admin_all ON public.products;
CREATE POLICY products_admin_all
    ON public.products
    FOR ALL
    TO authenticated
    USING (
        EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id = auth.uid()
              AND lower(u.role) IN ('admin', 'staff')
        )
    )
    WITH CHECK (
        EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id = auth.uid()
              AND lower(u.role) IN ('admin', 'staff')
        )
    );
