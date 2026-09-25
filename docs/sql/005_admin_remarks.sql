-- Optional: Admin remarks on orders (prototype).
-- Run in Supabase SQL Editor if you want Admin Remarks to persist.
-- Does NOT overwrite customer checkout order_notes.

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS admin_remarks text;

-- Existing admin UPDATE policies on public.orders still apply.
-- No extra GRANT is added here.
