-- NUBulldogsExchange: Confirmed order status + customer status notifications
-- Run in Supabase SQL Editor AFTER inspecting current constraints.
-- Does NOT create a new orders table or a new notifications table.
-- Reuses public.orders and public.notifications.
--
-- INSPECT FIRST:
--   SELECT conname, pg_get_constraintdef(oid)
--   FROM pg_constraint
--   WHERE conrelid = 'public.orders'::regclass
--     AND contype = 'c';
--
--   SELECT data_type, udt_name
--   FROM information_schema.columns
--   WHERE table_schema = 'public'
--     AND table_name = 'orders'
--     AND column_name IN ('status', 'payment_status', 'fulfillment', 'payment_method');
--
-- This script only drops/recreates a status CHECK when Confirmed is missing.
-- If no status CHECK exists, none is added.

-- =========================================================
-- 1) Allow Confirmed (and keep legacy aliases) on public.orders.status
-- =========================================================
DO $$
DECLARE
    rec record;
    added boolean := false;
BEGIN
    FOR rec IN
        SELECT con.conname, pg_get_constraintdef(con.oid) AS def
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
        WHERE nsp.nspname = 'public'
          AND rel.relname = 'orders'
          AND con.contype = 'c'
    LOOP
        IF rec.def ILIKE '%status%'
           AND rec.def NOT ILIKE '%payment_status%'
           AND (
                rec.def NOT ILIKE '%Confirmed%'
                OR rec.def NOT ILIKE '%Out for Delivery%'
           ) THEN
            EXECUTE format('ALTER TABLE public.orders DROP CONSTRAINT %I', rec.conname);
            EXECUTE $sql$
                ALTER TABLE public.orders
                ADD CONSTRAINT orders_status_check
                CHECK (status IN (
                    'Pending',
                    'Confirmed',
                    'Processing',
                    'Preparing',
                    'Ready for Pickup',
                    'Out for Delivery',
                    'Shipped',
                    'Delivered',
                    'Completed',
                    'Cancelled'
                ))
            $sql$;
            added := true;
            EXIT;
        END IF;
    END LOOP;

    IF NOT added THEN
        RAISE NOTICE 'No orders.status CHECK required an update (Confirmed already allowed, or no status CHECK exists).';
    END IF;
END $$;

-- If orders.status is an enum type instead of text+CHECK, run after confirming the type name:
-- ALTER TYPE order_status ADD VALUE IF NOT EXISTS 'Confirmed';

-- =========================================================
-- 2) Optional related_* columns on EXISTING public.notifications
-- =========================================================
ALTER TABLE public.notifications
    ADD COLUMN IF NOT EXISTS related_id text;

ALTER TABLE public.notifications
    ADD COLUMN IF NOT EXISTS related_href text;

-- =========================================================
-- 3) Let Admin/Staff insert customer notifications (existing table)
-- =========================================================
ALTER TABLE public.notifications ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS notifications_admin_insert ON public.notifications;
CREATE POLICY notifications_admin_insert
    ON public.notifications
    FOR INSERT
    TO authenticated
    WITH CHECK (
        EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id = auth.uid()
              AND lower(u.role) IN ('admin', 'staff')
        )
    );

-- =========================================================
-- 4) SECURITY DEFINER helper (fallback if direct insert is blocked)
-- =========================================================
CREATE OR REPLACE FUNCTION public.add_customer_notification(
    p_auth_user_id uuid,
    p_email text,
    p_id text,
    p_title text,
    p_message text,
    p_time_ago text DEFAULT NULL,
    p_icon text DEFAULT 'package',
    p_tone text DEFAULT 'blue',
    p_related_id text DEFAULT NULL,
    p_related_href text DEFAULT '/orders'
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    IF auth.uid() IS NULL THEN
        RAISE EXCEPTION 'Not authenticated';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.users_with_roles u
        WHERE u.id = auth.uid()
          AND lower(u.role) IN ('admin', 'staff')
    ) THEN
        RAISE EXCEPTION 'Only Admin or Staff can notify a customer.';
    END IF;

    IF p_auth_user_id IS NULL THEN
        RAISE EXCEPTION 'Customer user id is required.';
    END IF;

    INSERT INTO public.notifications (
        id,
        auth_user_id,
        email,
        title,
        message,
        time_ago,
        icon,
        tone,
        is_read,
        created_at,
        related_id,
        related_href
    )
    VALUES (
        COALESCE(NULLIF(btrim(p_id), ''), gen_random_uuid()::text),
        p_auth_user_id,
        NULLIF(btrim(COALESCE(p_email, '')), ''),
        p_title,
        p_message,
        COALESCE(NULLIF(btrim(COALESCE(p_time_ago, '')), ''), 'Just now'),
        COALESCE(NULLIF(btrim(COALESCE(p_icon, '')), ''), 'package'),
        COALESCE(NULLIF(btrim(COALESCE(p_tone, '')), ''), 'blue'),
        false,
        now(),
        NULLIF(btrim(COALESCE(p_related_id, '')), ''),
        COALESCE(NULLIF(btrim(COALESCE(p_related_href, '')), ''), '/orders')
    );
END;
$$;

REVOKE ALL ON FUNCTION public.add_customer_notification(uuid, text, text, text, text, text, text, text, text, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.add_customer_notification(uuid, text, text, text, text, text, text, text, text, text) TO authenticated;
GRANT EXECUTE ON FUNCTION public.add_customer_notification(uuid, text, text, text, text, text, text, text, text, text) TO service_role;
