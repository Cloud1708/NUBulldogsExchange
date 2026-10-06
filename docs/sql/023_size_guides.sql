-- Reusable Size Guide templates + product assignment.
-- Run in the Supabase SQL Editor. Do not duplicate this schema.
-- Editing a size_guides row updates every product that references it.

CREATE TABLE IF NOT EXISTS public.size_guides (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name text NOT NULL,
    sizing_standard text NOT NULL,
    measurement_unit text NOT NULL,
    notes text,
    columns jsonb NOT NULL DEFAULT '[]'::jsonb,
    rows jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_size_guides_name
    ON public.size_guides (lower(name));

ALTER TABLE public.products
    ADD COLUMN IF NOT EXISTS size_guide_id uuid REFERENCES public.size_guides(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_products_size_guide_id
    ON public.products (size_guide_id);

ALTER TABLE public.size_guides ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS size_guides_public_read ON public.size_guides;
CREATE POLICY size_guides_public_read
    ON public.size_guides
    FOR SELECT
    TO anon, authenticated
    USING (true);

DROP POLICY IF EXISTS size_guides_admin_write ON public.size_guides;
CREATE POLICY size_guides_admin_write
    ON public.size_guides
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

GRANT SELECT ON TABLE public.size_guides TO anon, authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.size_guides TO authenticated;
GRANT ALL ON TABLE public.size_guides TO service_role;
