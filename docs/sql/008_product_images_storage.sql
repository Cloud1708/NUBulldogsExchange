-- Optional public bucket for product photos.
-- Run in Supabase SQL Editor if you want images stored in Supabase Storage.
-- The web app also saves uploads under wwwroot/uploads/products so Shop can
-- show them even without this bucket.

INSERT INTO storage.buckets (id, name, public)
VALUES ('product-images', 'product-images', true)
ON CONFLICT (id) DO UPDATE SET public = true;

DROP POLICY IF EXISTS product_images_public_read ON storage.objects;
CREATE POLICY product_images_public_read
    ON storage.objects
    FOR SELECT
    TO public
    USING (bucket_id = 'product-images');

DROP POLICY IF EXISTS product_images_admin_write ON storage.objects;
CREATE POLICY product_images_admin_write
    ON storage.objects
    FOR ALL
    TO authenticated
    USING (
        bucket_id = 'product-images'
        AND EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id = auth.uid()
              AND lower(u.role) IN ('admin', 'staff')
        )
    )
    WITH CHECK (
        bucket_id = 'product-images'
        AND EXISTS (
            SELECT 1
            FROM public.users_with_roles u
            WHERE u.id = auth.uid()
              AND lower(u.role) IN ('admin', 'staff')
        )
    );
