-- Optional patch notes for place_checkout_order (run AFTER 001_product_variants.sql)
-- Adjust to match your existing RPC body. Goal:
-- 1) Read p_items[].variant_id and p_items[].selected_size
-- 2) If variant_id IS NOT NULL:
--      UPDATE product_variants SET stock_quantity = stock_quantity - qty WHERE id = variant_id AND stock_quantity >= qty
--      UPDATE products SET stock = (SELECT COALESCE(SUM(stock_quantity),0) FROM product_variants WHERE product_id = ...)
-- 3) Else keep existing product-level stock deduction
-- 4) INSERT order_items with variant_id, size, variant_sku snapshot

-- Example fragment (pseudo — merge into your real function):
/*
FOR EACH item IN p_items LOOP
  IF item.variant_id IS NOT NULL THEN
    UPDATE public.product_variants
       SET stock_quantity = stock_quantity - item.quantity,
           updated_at = now()
     WHERE id = item.variant_id
       AND stock_quantity >= item.quantity;

    IF NOT FOUND THEN
      RAISE EXCEPTION 'Insufficient stock for variant %', item.variant_id;
    END IF;

    UPDATE public.products p
       SET stock = sub.total,
           in_stock = sub.total > 0,
           updated_at = now()
      FROM (
        SELECT product_id, COALESCE(SUM(stock_quantity), 0) AS total
          FROM public.product_variants
         WHERE product_id = (SELECT product_id FROM public.product_variants WHERE id = item.variant_id)
         GROUP BY product_id
      ) sub
     WHERE p.id = sub.product_id;

    INSERT INTO public.order_items (..., variant_id, size, variant_sku)
    VALUES (..., item.variant_id, item.selected_size,
            (SELECT sku FROM public.product_variants WHERE id = item.variant_id));
  ELSE
    -- existing product stock logic
  END IF;
END LOOP;
*/
