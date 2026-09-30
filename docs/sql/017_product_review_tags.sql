-- NUBulldogsExchange: Optional feedback tags on product reviews
-- Run in Supabase SQL Editor AFTER 015_product_reviews_verified.sql.
-- REUSES public.product_reviews. Does NOT create a second reviews table.
--
-- Adds:
--   review_tags text[] NULL
-- Updates:
--   submit_product_review(... , p_tags text[] DEFAULT NULL)

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS review_tags text[];

-- Remove old 5-arg overload before creating the 6-arg version.
DROP FUNCTION IF EXISTS public.submit_product_review(text, bigint, integer, text, text);

CREATE OR REPLACE FUNCTION public.submit_product_review(
    p_order_id text,
    p_order_item_id bigint,
    p_rating integer,
    p_title text DEFAULT NULL,
    p_review text DEFAULT NULL,
    p_tags text[] DEFAULT NULL
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
    v_tags text[];
    v_allowed text[] := ARRAY[
        'Good Quality',
        'Comfortable',
        'True to Size',
        'Worth the Price',
        'Nice Design'
    ];
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

    IF char_length(v_review) > 500 THEN
        RAISE EXCEPTION 'Review must be 500 characters or fewer.';
    END IF;

    v_title := NULLIF(btrim(COALESCE(p_title, '')), '');
    IF v_title IS NOT NULL AND char_length(v_title) > 100 THEN
        v_title := left(v_title, 100);
    END IF;

    -- Keep only known optional feedback chips (order preserved, max 5).
    SELECT COALESCE(array_agg(t ORDER BY ord), ARRAY[]::text[])
      INTO v_tags
      FROM (
            SELECT DISTINCT ON (x.t) x.t, x.ord
              FROM unnest(COALESCE(p_tags, ARRAY[]::text[])) WITH ORDINALITY AS x(t, ord)
             WHERE x.t = ANY (v_allowed)
             ORDER BY x.t, x.ord
           ) filtered;

    IF cardinality(v_tags) = 0 THEN
        v_tags := NULL;
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
        review_tags,
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
        v_tags,
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

REVOKE ALL ON FUNCTION public.submit_product_review(text, bigint, integer, text, text, text[]) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.submit_product_review(text, bigint, integer, text, text, text[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.submit_product_review(text, bigint, integer, text, text, text[]) TO service_role;
