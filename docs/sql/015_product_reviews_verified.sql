-- NUBulldogsExchange: Verified-purchase product reviews
-- Run in Supabase SQL Editor AFTER 004 (order_items grants) as needed.
-- REUSES public.product_reviews. Does NOT create a second reviews table.
--
-- Existing columns kept:
--   id, product_id, author, initials, rating, review_date, comment
-- Added:
--   auth_user_id, order_id, order_item_id, title, created_at, updated_at
--
-- Customers insert ONLY through submit_product_review (SECURITY DEFINER).
-- Direct INSERT/UPDATE/DELETE is not granted to authenticated customers.
-- Staff/admin may maintain rows through RLS (users_with_roles).
--
-- YOU MUST RUN THIS SCRIPT or Write Review will fail at the RPC.

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS auth_user_id uuid;

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS order_id text;

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS order_item_id bigint;

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS title text;

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS created_at timestamptz NOT NULL DEFAULT now();

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS updated_at timestamptz NOT NULL DEFAULT now();

DO $$
BEGIN
    ALTER TABLE public.product_reviews
        ADD CONSTRAINT product_reviews_rating_range
        CHECK (rating >= 1 AND rating <= 5);
EXCEPTION
    WHEN duplicate_object THEN NULL;
    WHEN others THEN NULL;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS product_reviews_auth_user_order_item_uidx
    ON public.product_reviews (auth_user_id, order_item_id)
    WHERE auth_user_id IS NOT NULL
      AND order_item_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS product_reviews_product_id_idx
    ON public.product_reviews (product_id, review_date DESC);

CREATE INDEX IF NOT EXISTS product_reviews_order_id_idx
    ON public.product_reviews (order_id);

CREATE OR REPLACE FUNCTION public.nube_refresh_product_review_stats(p_product_id bigint)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_avg numeric;
    v_count integer;
BEGIN
    IF p_product_id IS NULL THEN
        RETURN;
    END IF;

    SELECT COALESCE(ROUND(AVG(rating)::numeric, 1), 0),
           COUNT(*)::integer
      INTO v_avg, v_count
      FROM public.product_reviews
     WHERE product_id = p_product_id;

    UPDATE public.products
       SET rating = v_avg,
           reviews = v_count,
           updated_at = now()
     WHERE id = p_product_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.submit_product_review(
    p_order_id text,
    p_order_item_id bigint,
    p_rating integer,
    p_title text DEFAULT NULL,
    p_review text DEFAULT NULL
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_order public.orders;
    v_item public.order_items;
    v_first text;
    v_last text;
    v_author text;
    v_initials text;
    v_title text;
    v_review text;
    v_row public.product_reviews;
BEGIN
    IF auth.uid() IS NULL THEN
        RAISE EXCEPTION 'Not authenticated';
    END IF;

    IF p_order_id IS NULL OR btrim(p_order_id) = '' THEN
        RAISE EXCEPTION 'Order number is missing.';
    END IF;

    IF p_order_item_id IS NULL OR p_order_item_id <= 0 THEN
        RAISE EXCEPTION 'Order item is missing.';
    END IF;

    IF p_rating IS NULL OR p_rating < 1 OR p_rating > 5 THEN
        RAISE EXCEPTION 'Please choose a rating from 1 to 5 stars.';
    END IF;

    v_review := btrim(COALESCE(p_review, ''));
    IF char_length(v_review) < 5 THEN
        RAISE EXCEPTION 'Please write a short review (at least 5 characters).';
    END IF;

    v_title := NULLIF(btrim(COALESCE(p_title, '')), '');

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

    IF v_order.status ILIKE 'Cancelled' THEN
        RAISE EXCEPTION 'Cancelled orders cannot be reviewed.';
    END IF;

    IF COALESCE(v_order.fulfillment, '') = 'Delivery' THEN
        IF v_order.status IS DISTINCT FROM 'Delivered' THEN
            RAISE EXCEPTION 'Reviews are available after the order is delivered.';
        END IF;
    ELSE
        IF v_order.status IS DISTINCT FROM 'Completed' THEN
            RAISE EXCEPTION 'Reviews are available after the order is completed.';
        END IF;
    END IF;

    SELECT *
      INTO v_item
      FROM public.order_items
     WHERE id = p_order_item_id
     LIMIT 1;

    IF NOT FOUND OR v_item.order_id IS DISTINCT FROM v_order.id THEN
        RAISE EXCEPTION 'This item is not part of the selected order.';
    END IF;

    IF v_item.product_id IS NULL THEN
        RAISE EXCEPTION 'This order item cannot be reviewed.';
    END IF;

    SELECT u.first_name, u.last_name
      INTO v_first, v_last
      FROM public.users u
     WHERE u.id::text = auth.uid()::text
     LIMIT 1;

    v_first := NULLIF(btrim(COALESCE(v_first, '')), '');
    v_last := NULLIF(btrim(COALESCE(v_last, '')), '');

    IF v_first IS NULL AND v_last IS NULL THEN
        v_author := 'Customer';
        v_initials := 'C';
    ELSIF v_last IS NULL THEN
        v_author := v_first;
        v_initials := upper(left(v_first, 1));
    ELSIF v_first IS NULL THEN
        v_author := v_last;
        v_initials := upper(left(v_last, 1));
    ELSE
        v_author := v_first || ' ' || upper(left(v_last, 1)) || '.';
        v_initials := upper(left(v_first, 1));
    END IF;

    INSERT INTO public.product_reviews (
        product_id,
        author,
        initials,
        rating,
        review_date,
        comment,
        title,
        auth_user_id,
        order_id,
        order_item_id,
        created_at,
        updated_at
    )
    VALUES (
        v_item.product_id,
        v_author,
        v_initials,
        p_rating,
        CURRENT_DATE,
        v_review,
        v_title,
        auth.uid(),
        v_order.id,
        v_item.id,
        now(),
        now()
    )
    RETURNING * INTO v_row;

    PERFORM public.nube_refresh_product_review_stats(v_item.product_id);

    RETURN to_jsonb(v_row);

EXCEPTION
    WHEN unique_violation THEN
        RAISE EXCEPTION 'You already reviewed this item.';
END;
$$;

REVOKE ALL ON FUNCTION public.nube_refresh_product_review_stats(bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.nube_refresh_product_review_stats(bigint) TO authenticated;
GRANT EXECUTE ON FUNCTION public.nube_refresh_product_review_stats(bigint) TO service_role;

REVOKE ALL ON FUNCTION public.submit_product_review(text, bigint, integer, text, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.submit_product_review(text, bigint, integer, text, text) TO authenticated;
GRANT EXECUTE ON FUNCTION public.submit_product_review(text, bigint, integer, text, text) TO service_role;

GRANT SELECT ON TABLE public.product_reviews TO anon, authenticated;
GRANT INSERT, UPDATE, DELETE ON TABLE public.product_reviews TO authenticated;
GRANT ALL ON TABLE public.product_reviews TO service_role;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'product_reviews_id_seq'
          AND c.relkind = 'S'
    ) THEN
        GRANT USAGE, SELECT ON SEQUENCE public.product_reviews_id_seq
            TO service_role;
    END IF;
END $$;

ALTER TABLE public.product_reviews ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS product_reviews_select_public ON public.product_reviews;
CREATE POLICY product_reviews_select_public
    ON public.product_reviews
    FOR SELECT
    TO anon, authenticated
    USING (true);

DROP POLICY IF EXISTS product_reviews_staff_write ON public.product_reviews;
CREATE POLICY product_reviews_staff_write
    ON public.product_reviews
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
