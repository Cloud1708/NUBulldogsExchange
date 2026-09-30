-- NUBulldogsExchange: Admin review visibility / moderation
-- Run in Supabase SQL Editor AFTER 015 (and 017 if used).
-- REUSES public.product_reviews. Does NOT create a second reviews table.
--
-- Adds:
--   is_visible boolean NOT NULL DEFAULT true
-- Updates:
--   nube_refresh_product_review_stats → only Visible reviews
--   public SELECT RLS → customers only see Visible reviews
-- Adds:
--   set_product_review_visibility(p_review_id, p_is_visible) for Admin/Staff

ALTER TABLE public.product_reviews
    ADD COLUMN IF NOT EXISTS is_visible boolean NOT NULL DEFAULT true;

CREATE INDEX IF NOT EXISTS product_reviews_visible_product_idx
    ON public.product_reviews (product_id, is_visible, review_date DESC);

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
     WHERE product_id = p_product_id
       AND COALESCE(is_visible, true) = true;

    UPDATE public.products
       SET rating = v_avg,
           reviews = v_count,
           updated_at = now()
     WHERE id = p_product_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.set_product_review_visibility(
    p_review_id bigint,
    p_is_visible boolean
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_row public.product_reviews;
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
        RAISE EXCEPTION 'Only admin or staff can moderate reviews.';
    END IF;

    IF p_review_id IS NULL OR p_review_id <= 0 THEN
        RAISE EXCEPTION 'Review id is missing.';
    END IF;

    IF p_is_visible IS NULL THEN
        RAISE EXCEPTION 'Visibility value is required.';
    END IF;

    UPDATE public.product_reviews
       SET is_visible = p_is_visible,
           updated_at = now()
     WHERE id = p_review_id
    RETURNING * INTO v_row;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Review not found.';
    END IF;

    PERFORM public.nube_refresh_product_review_stats(v_row.product_id);

    RETURN to_jsonb(v_row);
END;
$$;

REVOKE ALL ON FUNCTION public.set_product_review_visibility(bigint, boolean) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.set_product_review_visibility(bigint, boolean) TO authenticated;
GRANT EXECUTE ON FUNCTION public.set_product_review_visibility(bigint, boolean) TO service_role;

REVOKE ALL ON FUNCTION public.nube_refresh_product_review_stats(bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.nube_refresh_product_review_stats(bigint) TO authenticated;
GRANT EXECUTE ON FUNCTION public.nube_refresh_product_review_stats(bigint) TO service_role;

-- Customers/anon only see Visible reviews; Admin/Staff can see all.
DROP POLICY IF EXISTS product_reviews_select_public ON public.product_reviews;
CREATE POLICY product_reviews_select_public
    ON public.product_reviews
    FOR SELECT
    TO anon, authenticated
    USING (
        COALESCE(is_visible, true) = true
        OR EXISTS (
            SELECT 1
              FROM public.users_with_roles u
             WHERE u.id = auth.uid()
               AND lower(u.role) IN ('admin', 'staff')
        )
    );

-- Recompute cached product ratings from Visible reviews only.
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN SELECT DISTINCT product_id FROM public.product_reviews LOOP
        PERFORM public.nube_refresh_product_review_stats(r.product_id);
    END LOOP;
END $$;
