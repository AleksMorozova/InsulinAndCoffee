-- LIVE IMPORT: inserts up to 11 personal foods and COMMITS the transaction.
-- Target user: afb608c4-9951-44eb-b1d9-f614ac9cec25 only.
-- Run the ENTIRE file in one SQL editor connection/batch. No edits required.
-- If a previous transaction failed, run ROLLBACK separately before starting.
-- Existing records are skipped, never overwritten; reruns do not duplicate seed IDs.
-- On any error the transaction cannot commit; run ROLLBACK before retrying.
-- Results must say inserted or skipped, and the final statement must succeed as COMMIT.
-- Prepared for manual execution only. This file has NOT been executed.
-- Carbs are user-supplied; other macros are estimates. Soup/pending foods excluded.
-- No meal history, insulin, glucose, or other-user records are written.
BEGIN;
SET LOCAL search_path = pg_catalog, public;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT set_config('iac.personal_food_seed_user', 'afb608c4-9951-44eb-b1d9-f614ac9cec25', true);

DO $guard$
DECLARE target uuid := current_setting('iac.personal_food_seed_user')::uuid;
BEGIN
  IF target <> 'afb608c4-9951-44eb-b1d9-f614ac9cec25'::uuid THEN
    RAISE EXCEPTION 'This reviewed manifest is restricted to user afb608c4-9951-44eb-b1d9-f614ac9cec25';
  END IF;
  IF current_setting('server_encoding') <> 'UTF8' THEN
    RAISE EXCEPTION 'UTF8 database encoding is required';
  END IF;
  -- Retain the FK parent until commit; never create a replacement user.
  PERFORM 1 FROM public."Users" WHERE "Id" = target FOR KEY SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Target user does not exist; nothing imported'; END IF;
  IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
    WHERE "MigrationId" = '20261121132517_AddCarbProvenanceAndVerification') THEN
    RAISE EXCEPTION 'Required carb provenance migration is missing; apply the application migrations separately';
  END IF;
END;
$guard$;

-- Temporary transaction-local manifest only; no persistent staging table or schema change.
CREATE TEMP TABLE personal_food_seed_manifest (
  name text PRIMARY KEY,
  carbs numeric(7,2) NOT NULL,
  protein numeric(7,2) NOT NULL,
  fat numeric(7,2) NOT NULL,
  kcal numeric(8,2) NOT NULL,
  id uuid,
  status text
) ON COMMIT DROP;

INSERT INTO personal_food_seed_manifest (name, carbs, protein, fat, kcal) VALUES
  ('Пюре',                         15,   2,    4,   104),
  ('Гречка',                       20,   4,    1.5, 109.5),
  ('Спагетти (34 г углеводов)',     34,   5,    1.5, 169.5),
  ('Спагетти (33,5 г углеводов)',   33.5, 5,    1.5, 167.5),
  ('Плов',                         17,   6,    8,   164),
  ('Овощи',                        10,   2,    3,    75),
  ('Салат с майонезом',             20,   3,   15,   227),
  ('Котлеты',                      20,  14,   15,   271),
  ('Хлеб',                         15,   7,    2,   106),
  ('Десерт',                       35,   5,   12,   268),
  ('Яблоко',                       15,   0.3,  0.2,  63);

-- The existing (UserId, Name) index is NOT unique. Serialize the short import
-- against all concurrent FoodItems writers, including application inserts.
-- In dry-run mode this lock also gives a stable preview; it is released on rollback.
LOCK TABLE public."FoodItems" IN SHARE ROW EXCLUSIVE MODE;

-- Only the temporary manifest is assigned metadata. No existing application row is updated.
-- IDs are stable per user and manifest name; no extension or UUID generator required.
SELECT set_config('iac.personal_food_seed_dry_run', 'false', true);
DO $import$
DECLARE
  target uuid := current_setting('iac.personal_food_seed_user')::uuid;
  preview boolean := current_setting('iac.personal_food_seed_dry_run')::boolean;
  entry record;
  record_id uuid;
  reason text;
BEGIN
  FOR entry IN SELECT * FROM pg_temp.personal_food_seed_manifest ORDER BY name LOOP
    IF entry.carbs < 0 OR entry.protein < 0 OR entry.fat < 0 OR entry.kcal < 0
      OR abs(entry.kcal - (4 * entry.protein + 9 * entry.fat + 4 * entry.carbs)) > 1 THEN
      RAISE EXCEPTION 'Invalid nutrition manifest for %', entry.name;
    END IF;
    record_id := md5('iac-personal-food-v1:' || target::text || ':' || entry.name)::uuid;
    reason := NULL;
    -- A prior seed ID is skipped even after its name or nutrients were customized.
    IF EXISTS (SELECT 1 FROM public."FoodItems" WHERE "Id" = record_id AND "UserId" <> target) THEN
      RAISE EXCEPTION 'Seed ID collision with another owner; aborting';
    ELSIF EXISTS (SELECT 1 FROM public."FoodItems" WHERE "Id" = record_id) THEN
      reason := 'skipped: seed ID already exists (existing values preserved)';
    ELSIF EXISTS (SELECT 1 FROM public."FoodItems" WHERE "UserId" = target
        AND lower(btrim("Name")) = lower(entry.name)) THEN
      reason := 'skipped: same user/name already exists (existing values preserved)';
    ELSIF entry.name LIKE 'Спагетти (%' AND EXISTS (
        SELECT 1 FROM public."FoodItems" WHERE "UserId" = target AND lower(btrim("Name")) = lower('Спагетти')) THEN
      reason := 'skipped: existing ambiguous Спагетти needs review';
    END IF;
    IF reason IS NULL AND NOT preview THEN
      INSERT INTO public."FoodItems" (
        "Id", "UserId", "Name", "MeasurementType", "CarbsPer100g", "CarbsPerUnit",
        "ProteinPer100g", "FatPer100g", "CaloriesPer100g", "IsFavorite", "CreatedAt", "CarbReferenceSource")
      VALUES (record_id, target, entry.name, 0, entry.carbs, NULL,
        entry.protein, entry.fat, entry.kcal, false, transaction_timestamp(), 1);
    END IF;
    -- Update temporary reporting rows only, never public.FoodItems.
    UPDATE pg_temp.personal_food_seed_manifest SET id = record_id,
      status = coalesce(reason, CASE WHEN preview THEN 'would insert' ELSE 'inserted' END)
      WHERE name = entry.name;
  END LOOP;
END;
$import$;

SELECT name AS "Food", id AS "SeedId", 'per 100 g' AS "Basis", carbs AS "VerifiedCarbs",
  protein AS "EstimatedProtein", fat AS "EstimatedFat", kcal AS "EstimatedKcal", status AS "Result"
FROM pg_temp.personal_food_seed_manifest ORDER BY name;
SELECT status AS "Result", count(*) AS "Count" FROM pg_temp.personal_food_seed_manifest GROUP BY status ORDER BY status;


COMMIT;
