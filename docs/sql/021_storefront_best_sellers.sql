-- NUBulldogsExchange: Storefront "Bulldog Favorites" / best sellers
-- Run in Supabase SQL Editor.
-- REUSES public.orders + public.order_items. Creates no tables.
--
-- Guests cannot read orders/order_items (RLS), so the Home page reads this
-- SECURITY DEFINER aggregate instead. It returns ONLY product_id + units_sold:
-- no order ids, customer names, emails, or amounts.
--
-- Fulfilled orders only (matches OrderFlow.IsFulfilled):
--   Campus Pickup (or empty fulfillment) -> status = 'Completed'
--   Delivery                             -> status = 'Delivered'
-- Pending / Confirmed / Processing / Ready / Out for Delivery / Cancelled are excluded.
--
-- Grouped by product_id, so size/color variants of one product are summed together.
-- Only published Active products are returned.

CREATE OR REPLACE FUNCTION public.get_storefront_best_sellers(p_limit integer DEFAULT NULL)
RETURNS TABLE (product_id bigint, units_sold bigint)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT oi.product_id::bigint AS product_id,
           SUM(oi.quantity)::bigint AS units_sold
      FROM public.order_items oi
      JOIN public.orders o ON o.id = oi.order_id
      JOIN public.products p ON p.id = oi.product_id
     WHERE oi.product_id IS NOT NULL
       AND COALESCE(oi.quantity, 0) > 0
       AND p.is_published = true
       AND p.status = 'Active'
       AND (
            (lower(COALESCE(o.fulfillment, '')) = 'delivery'
                AND lower(COALESCE(o.status, '')) = 'delivered')
         OR (lower(COALESCE(o.fulfillment, '')) <> 'delivery'
                AND lower(COALESCE(o.status, '')) = 'completed')
       )
     GROUP BY oi.product_id
     ORDER BY units_sold DESC, oi.product_id
     LIMIT CASE WHEN p_limit IS NULL OR p_limit <= 0 THEN NULL ELSE p_limit END;
$$;

REVOKE ALL ON FUNCTION public.get_storefront_best_sellers(integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.get_storefront_best_sellers(integer) TO anon, authenticated, service_role;
