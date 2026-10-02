-- NUBulldogsExchange: persist the Delivery shipping snapshot captured at checkout
-- Run in Supabase SQL Editor AFTER 003_checkout_customer_shipping.sql.
-- Reuses public.orders. Does NOT grant customers general UPDATE.
-- Pattern matches confirm_checkout_payment / confirm_order_received (SECURITY DEFINER).
--
-- Why: place_checkout_order ignores the shipping_* keys in p_order, and the app's
-- follow-up PATCH is rejected by RLS for customers, so Delivery orders were saved
-- with NULL recipient / phone / address.
--
-- Rules:
--   own order, fulfillment = 'Delivery', status = 'Pending'
--   write-once: only fills shipping columns that are still empty
--   shipping_fee comes from AdminPortalSettings (orders.deliveryFee), never the client;
--   it is added to total only when total does not already include a fee.

CREATE OR REPLACE FUNCTION public.save_checkout_shipping_snapshot(
    p_order_id text,
    p_recipient_name text,
    p_phone text,
    p_address_line text,
    p_barangay text,
    p_city text,
    p_province text,
    p_postal_code text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_order public.orders;
    v_settings jsonb;
    v_fee numeric(10,2);
BEGIN
    IF auth.uid() IS NULL THEN
        RAISE EXCEPTION 'Not authenticated';
    END IF;

    IF p_order_id IS NULL OR btrim(p_order_id) = '' THEN
        RAISE EXCEPTION 'Order number is missing.';
    END IF;

    SELECT *
      INTO v_order
      FROM public.orders
     WHERE id = p_order_id
     LIMIT 1;

    IF NOT FOUND OR v_order.auth_user_id::text IS DISTINCT FROM auth.uid()::text THEN
        RAISE EXCEPTION 'Order not found or not owned by the current user.';
    END IF;

    IF v_order.fulfillment IS DISTINCT FROM 'Delivery' THEN
        RAISE EXCEPTION 'Shipping details apply to Delivery orders only.';
    END IF;

    IF v_order.status IS DISTINCT FROM 'Pending' THEN
        RAISE EXCEPTION 'Shipping details can only be saved while the order is Pending.';
    END IF;

    v_fee := 150;
    BEGIN
        SELECT s.value::text::jsonb
          INTO v_settings
          FROM public.settings s
         WHERE s.key = 'AdminPortalSettings'
         LIMIT 1;

        IF jsonb_typeof(v_settings) = 'string' THEN
            v_settings := (v_settings #>> '{}')::jsonb;
        END IF;

        IF (v_settings #>> '{orders,deliveryFee}')::numeric > 0 THEN
            v_fee := (v_settings #>> '{orders,deliveryFee}')::numeric;
        END IF;
    EXCEPTION
        WHEN OTHERS THEN
            v_fee := 150;
    END;

    UPDATE public.orders o
       SET shipping_recipient_name = COALESCE(NULLIF(btrim(o.shipping_recipient_name), ''), NULLIF(btrim(p_recipient_name), '')),
           shipping_phone          = COALESCE(NULLIF(btrim(o.shipping_phone), ''),          NULLIF(btrim(p_phone), '')),
           shipping_address_line   = COALESCE(NULLIF(btrim(o.shipping_address_line), ''),   NULLIF(btrim(p_address_line), '')),
           shipping_barangay       = COALESCE(NULLIF(btrim(o.shipping_barangay), ''),       NULLIF(btrim(p_barangay), '')),
           shipping_city           = COALESCE(NULLIF(btrim(o.shipping_city), ''),           NULLIF(btrim(p_city), '')),
           shipping_province       = COALESCE(NULLIF(btrim(o.shipping_province), ''),       NULLIF(btrim(p_province), '')),
           shipping_postal_code    = COALESCE(NULLIF(btrim(o.shipping_postal_code), ''),    NULLIF(btrim(p_postal_code), '')),
           shipping_fee = CASE WHEN COALESCE(o.shipping_fee, 0) > 0 THEN o.shipping_fee ELSE v_fee END,
           total = CASE
                       WHEN COALESCE(o.shipping_fee, 0) = 0
                        AND o.total = GREATEST(0, COALESCE(o.subtotal, 0) - COALESCE(o.discount_amount, 0))
                       THEN o.total + v_fee
                       ELSE o.total
                   END
     WHERE o.id = p_order_id
       AND o.auth_user_id::text = auth.uid()::text
    RETURNING * INTO v_order;

    RETURN to_jsonb(v_order);
END;
$$;

REVOKE ALL ON FUNCTION public.save_checkout_shipping_snapshot(text, text, text, text, text, text, text, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.save_checkout_shipping_snapshot(text, text, text, text, text, text, text, text) TO authenticated;
GRANT EXECUTE ON FUNCTION public.save_checkout_shipping_snapshot(text, text, text, text, text, text, text, text) TO service_role;

-- Optional: inspect recent Delivery orders saved before this fix.
-- SELECT id, date, shipping_recipient_name, shipping_phone, shipping_address_line, shipping_fee, subtotal, discount_amount, total
--   FROM public.orders
--  WHERE fulfillment = 'Delivery'
--  ORDER BY date DESC
--  LIMIT 10;
