-- Persist checkout payment_method + payment_status after place_checkout_order.
-- Needed because customers do not have UPDATE on public.orders (RLS).
-- Run in Supabase SQL Editor. Does NOT add columns and does NOT change order status.

CREATE OR REPLACE FUNCTION public.confirm_checkout_payment(
    p_order_id text,
    p_payment_method text,
    p_payment_status text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_order public.orders;
    v_method text;
    v_status text;
BEGIN
    IF auth.uid() IS NULL THEN
        RAISE EXCEPTION 'Not authenticated';
    END IF;

    IF p_order_id IS NULL OR btrim(p_order_id) = '' THEN
        RAISE EXCEPTION 'Order number is missing.';
    END IF;

    v_method := btrim(COALESCE(p_payment_method, ''));
    v_status := btrim(COALESCE(p_payment_status, ''));

    IF v_method NOT IN ('GCash', 'Maya', 'Credit Card', 'Cash on Pickup', 'Cash on Delivery') THEN
        RAISE EXCEPTION 'Invalid payment method';
    END IF;

    IF v_status NOT IN ('Paid', 'Pending') THEN
        RAISE EXCEPTION 'Invalid payment status';
    END IF;

    IF v_method IN ('Cash on Pickup', 'Cash on Delivery') THEN
        v_status := 'Pending';
    END IF;

    UPDATE public.orders
       SET payment_method = v_method,
           payment_status = v_status
     WHERE id = p_order_id
       AND auth_user_id::text = auth.uid()::text
    RETURNING * INTO v_order;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Order not found or not owned by the current user.';
    END IF;

    RETURN to_jsonb(v_order);
END;
$$;

REVOKE ALL ON FUNCTION public.confirm_checkout_payment(text, text, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.confirm_checkout_payment(text, text, text) TO authenticated;
GRANT EXECUTE ON FUNCTION public.confirm_checkout_payment(text, text, text) TO service_role;
