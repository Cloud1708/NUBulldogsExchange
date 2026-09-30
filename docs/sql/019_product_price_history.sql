-- NUBulldogsExchange: Product price history + ensure original_price column.
-- Run in Supabase SQL Editor AFTER existing product migrations.
-- Does NOT change order item prices. Does NOT create a promotion/coupon system.

-- Compare-at / crossed-out list price used by customer Web (Product.OriginalPrice).
ALTER TABLE public.products
    ADD COLUMN IF NOT EXISTS original_price numeric(12, 2);

CREATE TABLE IF NOT EXISTS public.product_price_history (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id bigint NOT NULL REFERENCES public.products(id) ON DELETE CASCADE,
    variant_id bigint NULL,
    previous_price numeric(12, 2) NOT NULL,
    new_price numeric(12, 2) NOT NULL,
    promo_price numeric(12, 2) NULL,
    reason text NOT NULL,
    notes text NOT NULL DEFAULT '',
    updated_by text NOT NULL DEFAULT 'Admin',
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_product_price_history_product_created
    ON public.product_price_history (product_id, created_at DESC);

ALTER TABLE public.product_price_history ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS product_price_history_admin_all ON public.product_price_history;
CREATE POLICY product_price_history_admin_all
    ON public.product_price_history
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

GRANT SELECT, INSERT ON TABLE public.product_price_history TO authenticated;
GRANT ALL ON TABLE public.product_price_history TO service_role;

-- Customers/anon never read price history from this table.
REVOKE ALL ON TABLE public.product_price_history FROM anon;
