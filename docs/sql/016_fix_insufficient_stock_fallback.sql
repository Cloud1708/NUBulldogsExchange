-- NUBulldogsExchange: Fix stock deduction so Pending orders do NOT deduct stock
-- Run this script in the Supabase SQL Editor.
--
-- Rule:
-- 1) When an order is "Pending", stocks must NOT be deducted yet.
-- 2) Stocks are deducted only when the order is confirmed / moves out of "Pending" (e.g. Confirmed, Processing).
-- 3) Stocks are restored if a previously confirmed order is Cancelled.
-- 4) Uses robust size/color and in-stock variant resolution fallbacks.

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
    IF v_name IS DISTINCT FROM 'Selected item' THEN
        RETURN format('%s is out of stock.', v_name);
    END IF;
    RETURN format('Insufficient stock for the selected item.');
END;
$$;

-- p_restore = false → deduct; true → restore.
-- Idempotent via orders.stock_applied.
-- Skips deduction if order is Pending.
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
        -- Only restore if stock was actually applied
        IF NOT COALESCE(v_order.stock_applied, false) THEN
            RETURN;
        END IF;
    ELSE
        -- If order is still Pending, do NOT deduct stocks yet!
        IF lower(btrim(coalesce(v_order.status, ''))) = 'pending' THEN
            RETURN;
        END IF;

        -- If stock was already applied, do not double-deduct
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
            -- 1) Try exact size + color match
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

            -- 2) Match by size if size was provided
            IF v_variant_id IS NULL AND v_item.size IS NOT NULL AND btrim(v_item.size) <> '' THEN
                SELECT pv.id
                  INTO v_variant_id
                  FROM public.product_variants pv
                 WHERE pv.product_id = v_item.product_id
                   AND lower(btrim(coalesce(pv.size, '')))
                       = lower(btrim(coalesce(v_item.size, '')))
                 ORDER BY pv.stock_quantity DESC, pv.id
                 LIMIT 1;
            END IF;

            -- 3) Match by color if color was provided
            IF v_variant_id IS NULL AND v_item.color_name IS NOT NULL AND btrim(v_item.color_name) <> '' THEN
                SELECT pv.id
                  INTO v_variant_id
                  FROM public.product_variants pv
                 WHERE pv.product_id = v_item.product_id
                   AND lower(btrim(coalesce(pv.color_name, '')))
                       = lower(btrim(coalesce(v_item.color_name, '')))
                 ORDER BY pv.stock_quantity DESC, pv.id
                 LIMIT 1;
            END IF;

            -- 4) Fallback to any variant for this product with sufficient stock
            IF v_variant_id IS NULL THEN
                SELECT pv.id
                  INTO v_variant_id
                  FROM public.product_variants pv
                 WHERE pv.product_id = v_item.product_id
                   AND pv.stock_quantity >= v_qty
                 ORDER BY pv.stock_quantity DESC, pv.id
                 LIMIT 1;
            END IF;

            -- 5) Fallback to the variant with the most stock
            IF v_variant_id IS NULL THEN
                SELECT pv.id
                  INTO v_variant_id
                  FROM public.product_variants pv
                 WHERE pv.product_id = v_item.product_id
                 ORDER BY pv.stock_quantity DESC, pv.id
                 LIMIT 1;
            END IF;
        END IF;

        IF v_variant_id IS NOT NULL THEN
            SELECT *
              INTO v_variant
              FROM public.product_variants
             WHERE id = v_variant_id
             FOR UPDATE;

            IF NOT FOUND THEN
                SELECT COALESCE(SUM(stock_quantity), 0)
                  INTO v_updated
                  FROM public.product_variants
                 WHERE product_id = v_item.product_id;

                IF v_updated < v_qty THEN
                    RAISE EXCEPTION 'Insufficient stock: %',
                        public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
                END IF;
            END IF;

            IF p_restore THEN
                UPDATE public.product_variants
                   SET stock_quantity = stock_quantity + v_qty,
                       updated_at = now()
                 WHERE id = v_variant_id;
            ELSE
                UPDATE public.product_variants
                   SET stock_quantity = GREATEST(0, stock_quantity - v_qty),
                       updated_at = now()
                 WHERE id = v_variant_id
                   AND stock_quantity >= v_qty;

                GET DIAGNOSTICS v_updated = ROW_COUNT;
                IF v_updated = 0 THEN
                    IF (SELECT COALESCE(SUM(stock_quantity), 0) FROM public.product_variants WHERE product_id = v_item.product_id) < v_qty THEN
                        RAISE EXCEPTION 'Insufficient stock: %',
                            public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
                    END IF;

                    UPDATE public.product_variants
                       SET stock_quantity = GREATEST(0, stock_quantity - v_qty),
                           updated_at = now()
                     WHERE id = v_variant_id;
                END IF;
            END IF;

            -- Stamp variant_id, size, and color_name onto order_items if missing
            UPDATE public.order_items
               SET variant_id = COALESCE(variant_id, v_variant_id),
                   size = COALESCE(size, v_variant.size),
                   color_name = COALESCE(color_name, v_variant.color_name),
                   variant_sku = COALESCE(variant_sku, v_variant.sku)
             WHERE id = v_item.id;

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
                IF COALESCE(v_product.stock, 0) < v_qty THEN
                    RAISE EXCEPTION 'Insufficient stock: %',
                        public.nube_insufficient_stock_message(v_item.name, v_item.color_name, v_item.size);
                END IF;

                UPDATE public.products
                   SET stock = GREATEST(0, COALESCE(stock, 0) - v_qty),
                       in_stock = (COALESCE(stock, 0) - v_qty) > 0,
                       updated_at = now()
                 WHERE id = v_item.product_id;
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

    IF v_processed = 0 AND NOT p_restore THEN
        RETURN;
    END IF;

    UPDATE public.orders
       SET stock_applied = NOT p_restore
     WHERE id = p_order_id;
END;
$$;

-- Do NOT deduct on insert if status is Pending
CREATE OR REPLACE FUNCTION public.nube_orders_apply_stock_on_insert()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    IF lower(btrim(coalesce(NEW.status, ''))) NOT IN ('pending', 'cancelled') THEN
        PERFORM public.nube_apply_order_stock(NEW.id, false);
    END IF;
    RETURN NULL;
END;
$$;

-- Deduct when moving from Pending to an active status (Confirmed, etc.); restore on Cancelled
CREATE OR REPLACE FUNCTION public.nube_orders_apply_stock_on_status_change()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    IF lower(btrim(coalesce(OLD.status, ''))) = 'pending'
       AND lower(btrim(coalesce(NEW.status, ''))) NOT IN ('pending', 'cancelled') THEN
        PERFORM public.nube_apply_order_stock(NEW.id, false);
    END IF;

    IF lower(btrim(coalesce(NEW.status, ''))) = 'cancelled'
       AND lower(btrim(coalesce(OLD.status, ''))) <> 'cancelled' THEN
        PERFORM public.nube_apply_order_stock(NEW.id, true);
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_nube_order_stock_insert ON public.orders;
CREATE CONSTRAINT TRIGGER trg_nube_order_stock_insert
AFTER INSERT ON public.orders
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE PROCEDURE public.nube_orders_apply_stock_on_insert();

DROP TRIGGER IF EXISTS trg_nube_order_stock_status ON public.orders;
DROP TRIGGER IF EXISTS trg_nube_order_stock_cancel ON public.orders;
CREATE TRIGGER trg_nube_order_stock_status
AFTER UPDATE OF status ON public.orders
FOR EACH ROW
EXECUTE PROCEDURE public.nube_orders_apply_stock_on_status_change();

-- apply_checkout_order_stock RPC helper
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

    -- If the order is Pending, do NOT deduct stock
    IF lower(btrim(coalesce(v_order.status, ''))) = 'pending' THEN
        RETURN jsonb_build_object('id', p_order_id, 'ok', true, 'pending', true);
    END IF;

    PERFORM public.nube_apply_order_stock(p_order_id, false);
    RETURN jsonb_build_object('id', p_order_id, 'ok', true);
END;
$$;

GRANT EXECUTE ON FUNCTION public.nube_insufficient_stock_message(text, text, text) TO authenticated, service_role;
GRANT EXECUTE ON FUNCTION public.apply_checkout_order_stock(text) TO authenticated, service_role;
