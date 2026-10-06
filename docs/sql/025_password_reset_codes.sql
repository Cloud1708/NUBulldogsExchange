-- Password-reset verification codes (WEB Forgot Password).
-- Run in the Supabase SQL Editor. Does not change public.users status
-- (inactive staff stay inactive after a reset).
--
-- Stores a HASH of the 6-digit code (never the plaintext OTP).
-- Service role writes from the Web server; clients cannot read this table.

CREATE TABLE IF NOT EXISTS public.password_reset_codes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email text NOT NULL,
    auth_user_id uuid NOT NULL,
    code_hash text NOT NULL,
    reset_token_hash text,
    expires_at timestamptz NOT NULL,
    verified_at timestamptz,
    consumed_at timestamptz,
    attempt_count integer NOT NULL DEFAULT 0
        CHECK (attempt_count >= 0),
    last_sent_at timestamptz NOT NULL DEFAULT now(),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_password_reset_codes_email_created
    ON public.password_reset_codes (email, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_password_reset_codes_active
    ON public.password_reset_codes (email)
    WHERE consumed_at IS NULL;

ALTER TABLE public.password_reset_codes ENABLE ROW LEVEL SECURITY;

-- No anon/authenticated policies: only service_role (Web server) may access.
DROP POLICY IF EXISTS password_reset_codes_no_direct_client ON public.password_reset_codes;

GRANT ALL ON TABLE public.password_reset_codes TO service_role;
REVOKE ALL ON TABLE public.password_reset_codes FROM anon, authenticated;

-- Lookup for reset codes reads public.users (directly or via users_with_roles).
GRANT SELECT ON TABLE public.users TO service_role;
GRANT SELECT ON TABLE public.roles TO service_role;
GRANT SELECT ON TABLE public.users_with_roles TO service_role;
