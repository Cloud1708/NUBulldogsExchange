-- Required public bucket for product / category / store photos.
-- Run this in the Supabase SQL Editor so every device can see uploaded images.
-- The web app uploads only to this bucket (no local wwwroot fallback).

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
