-- Quick fix: run this NOW in Supabase SQL Editor if Customer My Orders shows:
--   permission denied for table order_items
--
-- RLS alone is not enough. Without GRANT, Postgres returns 42501.

GRANT SELECT ON TABLE public.order_items TO authenticated;
GRANT ALL ON TABLE public.order_items TO service_role;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'order_items_id_seq'
          AND c.relkind = 'S'
    ) THEN
        GRANT USAGE, SELECT ON SEQUENCE public.order_items_id_seq
            TO authenticated, service_role;
    END IF;
END $$;

ALTER TABLE public.order_items ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS order_items_select_own ON public.order_items;
CREATE POLICY order_items_select_own
    ON public.order_items
    FOR SELECT
    TO authenticated
    USING (
        EXISTS (
            SELECT 1
            FROM public.orders o
            WHERE o.id = order_items.order_id
              AND o.auth_user_id = auth.uid()
        )
        OR EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id = auth.uid()
              AND lower(u.role) IN ('admin', 'staff')
        )
    );
