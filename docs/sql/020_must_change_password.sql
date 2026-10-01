-- NUBulldogsExchange: Staff first-login password change flag
-- Run in Supabase SQL Editor BEFORE using the redesigned Add Staff flow.
-- Safe to re-run.
--
-- FIX (PostgreSQL 42P16):
-- CREATE OR REPLACE VIEW cannot rename/reorder existing view columns
-- (error: cannot change name of view column "profile_image" to "role").
-- Drop + recreate is required when column order differs.

-- ============================================================
-- 1) Column on public.users
-- ============================================================
ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN public.users.must_change_password IS
    'When true, Admin/Staff must set a new password before using the Web portal.';

-- ============================================================
-- 2) Recreate users_with_roles so PostgREST exposes the column
-- ============================================================
-- If this DROP fails because other objects depend on the view,
-- run this first to inspect dependents, then decide on CASCADE:
--   SELECT dependent_ns.nspname, dependent_view.relname
--   FROM pg_depend
--   JOIN pg_rewrite ON pg_depend.objid = pg_rewrite.oid
--   JOIN pg_class AS dependent_view ON pg_rewrite.ev_class = dependent_view.oid
--   JOIN pg_namespace AS dependent_ns ON dependent_view.relnamespace = dependent_ns.oid
--   JOIN pg_class AS source_view ON pg_depend.refobjid = source_view.oid
--   WHERE source_view.relname = 'users_with_roles'
--     AND source_view.relkind = 'v';

DROP VIEW IF EXISTS public.users_with_roles;

CREATE VIEW public.users_with_roles AS
SELECT
    u.id,
    u.role_id,
    u.first_name,
    u.middle_name,
    u.last_name,
    u.email,
    u.phone_number,
    r.name AS role,
    u.status,
    u.profile_image,
    u.student_id,
    u.college,
    u.address,
    COALESCE(u.is_primary_admin, false) AS is_primary_admin,
    u.permissions,
    u.last_login_at,
    COALESCE(u.must_change_password, false) AS must_change_password,
    u.created_at,
    u.updated_at
FROM public.users u
LEFT JOIN public.roles r ON r.id = u.role_id;

-- Grants (re-apply after DROP)
GRANT SELECT ON public.users_with_roles TO authenticated, anon, service_role;

-- ============================================================
-- NOTES
-- ============================================================
-- • Passwords stay in Supabase Auth — never in public.users.
-- • Staff creation from Web uses Auth Admin API when
--   Supabase:ServiceRoleKey is configured on the Web.Web server only.
-- • If DROP VIEW fails due to dependencies, either:
--     A) DROP VIEW public.users_with_roles CASCADE; then re-run CREATE/GRANT above, or
--     B) Keep your existing view and only append the new column at the END
--        with CREATE OR REPLACE VIEW (same column names/order as before).
