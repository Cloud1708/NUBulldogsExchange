-- NUBulldogsExchange: Deduct stock once at successful order placement;
-- restore once on eligible cancellation.
-- Run in Supabase SQL Editor AFTER 001/009/012 as needed.
-- Reuses public.orders, public.order_items, public.products, public.product_variants.
-- Does NOT create a new inventory table. Does NOT grant extra customer UPDATE.
--
-- Why a deferred INSERT trigger AND apply_checkout_order_stock:
-- place_checkout_order lives only in Supabase (not in this repo) and does not
-- deduct variant stock. The deferred trigger deducts in the same transaction
-- when order_items already exist. The Web app also calls
-- apply_checkout_order_stock after checkout so stock is deducted even if the
-- trigger fired too early (no items yet) or was not created.
--
-- YOU MUST RUN THIS SCRIPT IN SUPABASE SQL EDITOR or stock will not move.
--
-- If place_checkout_order already deducts stock, DROP trigger
-- trg_nube_order_stock_insert before using this script, or checkout will fail
-- on insufficient stock (double deduct). Observed Web app behavior is that
-- status changes do not deduct, and overselling is possible — so this trigger
-- is the intended deduction point.
--
-- Cancellation restore uses AFTER UPDATE OF status so both:
--   cancel_order RPC (customer)
--   admin PATCH to Cancelled
-- restore exactly once via orders.stock_applied.

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS stock_applied boolean NOT NULL DEFAULT false;

-- App column is products.stock (not stock_quantity). Ignore if the check already exists
-- or if legacy negative rows prevent adding it.
DO $$
BEGIN
    ALTER TABLE public.products
        ADD CONSTRAINT products_stock_nonnegative CHECK (stock >= 0);
EXCEPTION
    WHEN duplicate_object THEN NULL;
    WHEN check_violation THEN NULL;
    WHEN others THEN NULL;
END $$;

CREATE OR REPLACE FUNCTION public.nube_sync_product_aggregate_stock(p_product_id bigint)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_total integer;
BEGIN
    IF EXISTS (SELECT 1 FROM public.product_variants WHERE product_id = p_product_id) THEN
        SELECT COALESCE(SUM(stock_quantity), 0)
          INTO v_total
          FROM public.product_variants
         WHERE product_id = p_product_id;

        UPDATE public.products
           SET stock = v_total,
               in_stock = v_total > 0,
               updated_at = now()
         WHERE id = p_product_id;
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION public.nube_insufficient_stock_message(
    p_name text,
    p_color text,
    p_size text
)
RETURNS text
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    v_name text := COALESCE(NULLIF(btrim(p_name), ''), 'Selected item');
    v_color text := NULLIF(btrim(COALESCE(p_color, '')), '');
    v_size text := NULLIF(btrim(COALESCE(p_size, '')), '');
BEGIN
    IF v_color IS NOT NULL AND v_size IS NOT NULL THEN
        RETURN format('%s - %s/%s is no longer available.', v_name, v_color, v_size);
    END IF;
    IF v_size IS NOT NULL THEN
        RETURN format('%s - Size %s is no longer available.', v_name, v_size);
    END IF;
    IF v_color IS NOT NULL THEN
        RETURN format('%s - %s is no longer available.', v_name, v_color);
    END IF;
    RETURN format('Insufficient stock for the selected item.');
END;
$$;

-- p_restore = false → deduct; true → restore.
-- Idempotent via orders.stock_applied.
CREATE OR REPLACE FUNCTION public.nube_apply_order_stock(p_order_id text, p_restore boolean)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_order public.orders;
    v_item record;
    v_qty integer;
    v_variant public.product_variants;
    v_product public.products;
    v_has_variants boolean;
    v_updated integer;
    v_processed integer := 0;
    v_variant_id bigint;
BEGIN
    IF p_order_id IS NULL OR btrim(p_order_id) = '' THEN
        RETURN;
    END IF;

    SELECT *
      INTO v_order
      FROM public.orders
     WHERE id = p_order_id
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Order not found.';
    END IF;

    IF p_restore THEN
        IF NOT COALESCE(v_order.stock_applied, false) THEN
            RETURN;
        END IF;
    ELSE
        IF COALESCE(v_order.stock_applied, false) THEN
            RETURN;
        END IF;
    END IF;

    FOR v_item IN
        SELECT *
          FROM public.order_items
         WHERE order_id = p_order_id
    LOOP
        v_qty := GREATEST(COALESCE(v_item.quantity, 0), 0);
        IF v_qty = 0 THEN
            CONTINUE;
        END IF;

        v_has_variants := EXISTS (
            SELECT 1 FROM public.product_variants WHERE product_id = v_item.product_id
        );

        v_variant_id := v_item.variant_id;
        IF v_variant_id IS NULL AND v_has_variants THEN
            SELECT pv.id
              INTO v_variant_id
              FROM public.product_variants pv
             WHERE pv.product_id = v_item.product_id
               AND lower(btrim(coalesce(pv.size, '')))
                   = lower(btrim(coalesce(v_item.size, '')))
               AND lower(btrim(coalesce(pv.color_name, '')))
                   = lower(btrim(coalesce(v_item.color_name, '')))
             ORDER BY pv.id
             LIMIT 1;
        END IF;

        IF v_variant_id IS NOT NULL THEN
            SELECT *
              INTO v_variant
              FROM public.product_variants
             WHERE id = v_variant_id
             FOR UPDATE;

            IF NOT FOUND THEN
                RAISE EXCEPTION 'Insufficient stock: %',
                    public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
            END IF;

            IF p_restore THEN
                UPDATE public.product_variants
                   SET stock_quantity = stock_quantity + v_qty,
                       updated_at = now()
                 WHERE id = v_variant_id;
            ELSE
                UPDATE public.product_variants
                   SET stock_quantity = stock_quantity - v_qty,
                       updated_at = now()
                 WHERE id = v_variant_id
                   AND stock_quantity >= v_qty;

                GET DIAGNOSTICS v_updated = ROW_COUNT;
                IF v_updated = 0 THEN
                    RAISE EXCEPTION 'Insufficient stock: %',
                        public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
                END IF;
            END IF;

            PERFORM public.nube_sync_product_aggregate_stock(v_variant.product_id);

            BEGIN
                UPDATE public.products
                   SET sold = GREATEST(0, COALESCE(sold, 0) + CASE WHEN p_restore THEN -v_qty ELSE v_qty END),
                       updated_at = now()
                 WHERE id = v_variant.product_id;
            EXCEPTION
                WHEN undefined_column THEN
                    NULL;
            END;
        ELSE
            IF v_has_variants THEN
                RAISE EXCEPTION 'Insufficient stock: %',
                    public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
            END IF;

            SELECT *
              INTO v_product
              FROM public.products
             WHERE id = v_item.product_id
             FOR UPDATE;

            IF NOT FOUND THEN
                RAISE EXCEPTION 'Insufficient stock: %',
                    public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
            END IF;

            IF p_restore THEN
                UPDATE public.products
                   SET stock = COALESCE(stock, 0) + v_qty,
                       in_stock = true,
                       updated_at = now()
                 WHERE id = v_item.product_id;
            ELSE
                UPDATE public.products
                   SET stock = COALESCE(stock, 0) - v_qty,
                       in_stock = (COALESCE(stock, 0) - v_qty) > 0,
                       updated_at = now()
                 WHERE id = v_item.product_id
                   AND COALESCE(stock, 0) >= v_qty;

                GET DIAGNOSTICS v_updated = ROW_COUNT;
                IF v_updated = 0 THEN
                    RAISE EXCEPTION 'Insufficient stock: %',
                        public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
                END IF;
            END IF;

            BEGIN
                UPDATE public.products
                   SET sold = GREATEST(0, COALESCE(sold, 0) + CASE WHEN p_restore THEN -v_qty ELSE v_qty END)
                 WHERE id = v_item.product_id;
            EXCEPTION
                WHEN undefined_column THEN
                    NULL;
            END;
        END IF;

        v_processed := v_processed + 1;
    END LOOP;

    -- If items were not present yet (trigger fired too early), leave stock_applied
    -- false so apply_checkout_order_stock can deduct after order_items exist.
    IF v_processed = 0 AND NOT p_restore THEN
        RETURN;
    END IF;

    UPDATE public.orders
       SET stock_applied = NOT p_restore
     WHERE id = p_order_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.apply_checkout_order_stock(p_order_id text)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_order public.orders;
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
        RAISE EXCEPTION 'Order not found.';
    END IF;

    IF v_order.auth_user_id::text IS DISTINCT FROM auth.uid()::text
       AND NOT EXISTS (
            SELECT 1
              FROM public.users_with_roles u
             WHERE u.id::text = auth.uid()::text
               AND lower(u.role) IN ('admin', 'staff')
       ) THEN
        RAISE EXCEPTION 'Order not found.';
    END IF;

    PERFORM public.nube_apply_order_stock(p_order_id, false);
    RETURN jsonb_build_object('id', p_order_id, 'ok', true);
END;
$$;

CREATE OR REPLACE FUNCTION public.nube_orders_apply_stock_on_insert()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    PERFORM public.nube_apply_order_stock(NEW.id, false);
    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION public.nube_orders_restore_stock_on_cancel()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    PERFORM public.nube_apply_order_stock(NEW.id, true);
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_nube_order_stock_insert ON public.orders;
CREATE CONSTRAINT TRIGGER trg_nube_order_stock_insert
AFTER INSERT ON public.orders
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE PROCEDURE public.nube_orders_apply_stock_on_insert();

DROP TRIGGER IF EXISTS trg_nube_order_stock_cancel ON public.orders;
CREATE TRIGGER trg_nube_order_stock_cancel
AFTER UPDATE OF status ON public.orders
FOR EACH ROW
WHEN (NEW.status = 'Cancelled' AND OLD.status IS DISTINCT FROM 'Cancelled')
EXECUTE PROCEDURE public.nube_orders_restore_stock_on_cancel();

REVOKE ALL ON FUNCTION public.nube_sync_product_aggregate_stock(bigint) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.nube_apply_order_stock(text, boolean) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.nube_orders_apply_stock_on_insert() FROM PUBLIC;
REVOKE ALL ON FUNCTION public.nube_orders_restore_stock_on_cancel() FROM PUBLIC;

GRANT EXECUTE ON FUNCTION public.nube_insufficient_stock_message(text, text, text) TO authenticated, service_role;
GRANT EXECUTE ON FUNCTION public.apply_checkout_order_stock(text) TO authenticated, service_role;

-- Optional backfill for orders placed before this script (skip cancelled):
-- SELECT public.nube_apply_order_stock(id, false)
--   FROM public.orders
--  WHERE COALESCE(stock_applied, false) = false
--    AND status IS DISTINCT FROM 'Cancelled';
