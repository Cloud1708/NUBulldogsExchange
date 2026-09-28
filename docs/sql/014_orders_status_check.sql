-- NUBulldogsExchange: Allow Delivery statuses on public.orders.status
-- Run in Supabase SQL Editor.
--
-- Symptom:
--   Admin Save Changes → "Please check constraint \"orders_status_check\""
--   when setting status to Out for Delivery.
--
-- Cause:
--   010_order_confirmed_status.sql only recreated the CHECK when Confirmed
--   was missing. If Confirmed was already allowed, Out for Delivery stayed out.
--
-- This script ALWAYS drops the status CHECK and recreates it with the full list.

DO $$
DECLARE
    rec record;
    v_typname text;
BEGIN
    SELECT t.typname
      INTO v_typname
      FROM pg_attribute a
      JOIN pg_class c ON c.oid = a.attrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
      JOIN pg_type t ON t.oid = a.atttypid
     WHERE n.nspname = 'public'
       AND c.relname = 'orders'
       AND a.attname = 'status'
       AND a.attnum > 0
       AND NOT a.attisdropped
     LIMIT 1;

    IF v_typname IS NOT NULL
       AND v_typname NOT IN ('text', 'varchar', 'bpchar', 'citext') THEN
        BEGIN
            EXECUTE format('ALTER TYPE %I ADD VALUE IF NOT EXISTS %L', v_typname, 'Out for Delivery');
        EXCEPTION
            WHEN duplicate_object THEN NULL;
            WHEN others THEN NULL;
        END;
        BEGIN
            EXECUTE format('ALTER TYPE %I ADD VALUE IF NOT EXISTS %L', v_typname, 'Shipped');
        EXCEPTION
            WHEN duplicate_object THEN NULL;
            WHEN others THEN NULL;
        END;
        BEGIN
            EXECUTE format('ALTER TYPE %I ADD VALUE IF NOT EXISTS %L', v_typname, 'Delivered');
        EXCEPTION
            WHEN duplicate_object THEN NULL;
            WHEN others THEN NULL;
        END;
        RAISE NOTICE 'orders.status is enum type %. Added missing values if needed.', v_typname;
        RETURN;
    END IF;

    FOR rec IN
        SELECT con.conname, pg_get_constraintdef(con.oid) AS def
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
        WHERE nsp.nspname = 'public'
          AND rel.relname = 'orders'
          AND con.contype = 'c'
    LOOP
        IF rec.conname = 'orders_status_check'
           OR (
                rec.def ILIKE '%status%'
                AND rec.def NOT ILIKE '%payment_status%'
                AND rec.def ILIKE '%Pending%'
           ) THEN
            EXECUTE format('ALTER TABLE public.orders DROP CONSTRAINT %I', rec.conname);
        END IF;
    END LOOP;

    ALTER TABLE public.orders
        ADD CONSTRAINT orders_status_check
        CHECK (status IN (
            'Pending',
            'Confirmed',
            'Processing',
            'Preparing',
            'Ready for Pickup',
            'Out for Delivery',
            'Shipped',
            'Delivered',
            'Completed',
            'Cancelled'
        ));
END $$;
