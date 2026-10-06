-- Fix: Forgot Password "permission denied for table users"
-- The reset flow looks up accounts before emailing a code.
-- service_role must be able to SELECT public.users (and roles for users_with_roles).
-- Run in the Supabase SQL Editor, then retry Send Verification Code.

GRANT SELECT ON TABLE public.users TO service_role;
GRANT SELECT ON TABLE public.roles TO service_role;
GRANT SELECT ON TABLE public.users_with_roles TO service_role;

-- Keep reset-code table writable by the Web server only.
GRANT ALL ON TABLE public.password_reset_codes TO service_role;
REVOKE ALL ON TABLE public.password_reset_codes FROM PUBLIC;
REVOKE ALL ON TABLE public.password_reset_codes FROM anon, authenticated;
