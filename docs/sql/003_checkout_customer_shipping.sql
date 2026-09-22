-- NUBulldogsExchange: Checkout customer contact + shipping snapshot + saved addresses
-- Run in Supabase SQL Editor AFTER relying on delivery address / phone snapshot.
-- Non-destructive (IF NOT EXISTS / ADD COLUMN IF NOT EXISTS).

-- =========================================================
-- 1) Saved addresses (optional for Delivery checkout)
-- =========================================================
CREATE TABLE IF NOT EXISTS public.user_addresses (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL
        REFERENCES public.users(id)
        ON DELETE CASCADE,
    label text NOT NULL DEFAULT 'Home',
    recipient_name text NOT NULL,
    phone_number text NOT NULL,
    address_line text NOT NULL,
    barangay text NOT NULL,
    city text NOT NULL,
    province text NOT NULL,
    postal_code text,
    is_default boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_user_addresses_user_id
    ON public.user_addresses (user_id);

ALTER TABLE public.user_addresses ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS user_addresses_select_own ON public.user_addresses;
CREATE POLICY user_addresses_select_own
    ON public.user_addresses FOR SELECT TO authenticated
    USING (auth.uid() = user_id);

DROP POLICY IF EXISTS user_addresses_insert_own ON public.user_addresses;
CREATE POLICY user_addresses_insert_own
    ON public.user_addresses FOR INSERT TO authenticated
    WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS user_addresses_update_own ON public.user_addresses;
CREATE POLICY user_addresses_update_own
    ON public.user_addresses FOR UPDATE TO authenticated
    USING (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

DROP POLICY IF EXISTS user_addresses_delete_own ON public.user_addresses;
CREATE POLICY user_addresses_delete_own
    ON public.user_addresses FOR DELETE TO authenticated
    USING (auth.uid() = user_id);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.user_addresses TO authenticated;
GRANT ALL ON TABLE public.user_addresses TO service_role;

-- =========================================================
-- 2) Order contact + shipping snapshot columns
--    (nullable — Campus Pickup leaves shipping_* NULL)
-- =========================================================
ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS customer_phone text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS payment_method text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS order_notes text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_fee numeric(10,2) NOT NULL DEFAULT 0;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_recipient_name text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_phone text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_address_line text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_barangay text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_city text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_province text;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS shipping_postal_code text;

-- =========================================================
-- 3) place_checkout_order RPC notes
-- =========================================================
-- Update place_checkout_order so p_order may include:
--   customer_phone, payment_method, order_notes, shipping_fee,
--   shipping_recipient_name, shipping_phone, shipping_address_line,
--   shipping_barangay, shipping_city, shipping_province, shipping_postal_code
-- Persist them onto public.orders at insert time.
-- For Delivery: add shipping_fee into total when appropriate.
-- For Campus Pickup: shipping_* may be null; shipping_fee = 0.
--
-- Until the RPC is patched, the app also attempts a best-effort PATCH
-- of these snapshot fields after order creation.
