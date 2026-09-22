-- Quick fix: run this NOW in Supabase SQL Editor if you already ran 001
-- and get: permission denied for table product_variants

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.product_variants
    TO anon, authenticated;
GRANT ALL ON TABLE public.product_variants TO service_role;
GRANT USAGE, SELECT ON SEQUENCE public.product_variants_id_seq
    TO anon, authenticated, service_role;
