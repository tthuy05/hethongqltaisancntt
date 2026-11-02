-- Idempotent update for 4 demo assets missing purchase price (M1-DEMO-20261003-xxx)
-- REQUIRES EXPLICIT USER APPROVAL BEFORE RUNNING ON NEON PRODUCTION (neondb)
BEGIN;

-- Safety check: only update if current database is neondb or test database
DO $$
BEGIN
    IF current_database() NOT IN ('neondb') AND current_database() !~ '^itam_local_verify_' THEN
        RAISE EXCEPTION 'Target database % is not authorized for demo price update', current_database();
    END IF;
END $$;

-- 1. Demo Switch 06 (ID: 9, AssetCode: M1-DEMO-20261003-006) -> 7,500,000 VND
UPDATE assets 
SET purchase_cost = 7500000, 
    updated_at_utc = NOW()
WHERE id = 9 
  AND asset_code = 'M1-DEMO-20261003-006' 
  AND purchase_cost IS NULL;

-- 2. Demo Printer 12 (ID: 15, AssetCode: M1-DEMO-20261003-012) -> 15,000,000 VND
UPDATE assets 
SET purchase_cost = 15000000, 
    updated_at_utc = NOW()
WHERE id = 15 
  AND asset_code = 'M1-DEMO-20261003-012' 
  AND purchase_cost IS NULL;

-- 3. Demo Desktop 18 (ID: 21, AssetCode: M1-DEMO-20261003-018) -> 22,500,000 VND
UPDATE assets 
SET purchase_cost = 22500000, 
    updated_at_utc = NOW()
WHERE id = 21 
  AND asset_code = 'M1-DEMO-20261003-018' 
  AND purchase_cost IS NULL;

-- 4. Demo Other 24 (ID: 27, AssetCode: M1-DEMO-20261003-024) -> 30,000,000 VND
UPDATE assets 
SET purchase_cost = 30000000, 
    updated_at_utc = NOW()
WHERE id = 27 
  AND asset_code = 'M1-DEMO-20261003-024' 
  AND purchase_cost IS NULL;

-- Verify results
SELECT id, asset_code, name, purchase_cost, updated_at_utc 
FROM assets 
WHERE id IN (9, 15, 21, 27);

-- Check new total active asset value
SELECT 
    COUNT(*) as active_count,
    COUNT(*) FILTER (WHERE purchase_cost IS NULL) as missing_price_count,
    SUM(purchase_cost) as total_asset_value
FROM assets 
WHERE is_archived = false;

COMMIT;
