-- NUBulldogsExchange: Customer confirms Delivery receipt
-- Run in Supabase SQL Editor.
-- Reuses public.orders. Does NOT grant customers general UPDATE.
-- Pattern matches cancel_order / confirm_checkout_payment (SECURITY DEFINER).
--
-- Allowed transition only:
--   own Delivery order
--   status IN ('Out for Delivery', 'Shipped')
--   → status = 'Delivered'
-- payment_status is NOT changed.

CREATE OR REPLACE FUNCTION public.confirm_order_received(p_order_id text)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_order public.orders;
    v_old_status text;
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

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Order not found or not owned by the current user.';
    END IF;

    IF v_order.auth_user_id::text IS DISTINCT FROM auth.uid()::text THEN
        RAISE EXCEPTION 'Order not found or not owned by the current user.';
    END IF;

    IF v_order.fulfillment IS DISTINCT FROM 'Delivery' THEN
        RAISE EXCEPTION 'Only Delivery orders can be marked as received.';
    END IF;

    IF v_order.status NOT IN ('Out for Delivery', 'Shipped') THEN
        RAISE EXCEPTION 'This order is not out for delivery.';
    END IF;

    v_old_status := v_order.status;

    UPDATE public.orders
       SET status = 'Delivered'
     WHERE id = p_order_id
       AND auth_user_id::text = auth.uid()::text
       AND fulfillment = 'Delivery'
       AND status IN ('Out for Delivery', 'Shipped')
    RETURNING * INTO v_order;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Order could not be marked as received.';
    END IF;

    BEGIN
        INSERT INTO public.order_status_history (
            order_id,
            old_status,
            new_status,
            notes,
            changed_by,
            created_at
        )
        VALUES (
            p_order_id,
            v_old_status,
            'Delivered',
            'Customer confirmed receipt',
            auth.uid()::text,
            now()
        );
    EXCEPTION
        WHEN undefined_table THEN
            NULL;
        WHEN OTHERS THEN
            NULL;
    END;

    RETURN to_jsonb(v_order);
END;
$$;

REVOKE ALL ON FUNCTION public.confirm_order_received(text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.confirm_order_received(text) TO authenticated;
GRANT EXECUTE ON FUNCTION public.confirm_order_received(text) TO service_role;
