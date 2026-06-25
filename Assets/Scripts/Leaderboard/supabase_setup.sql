-- ══════════════════════════════════════════════════════════════════════════════
--  SUPABASE LEADERBOARD + AUTH — Script completo
--  Ejecutar en: Supabase Dashboard > SQL Editor > New Query
-- ══════════════════════════════════════════════════════════════════════════════

-- ══════════════════════════════════════════════════════════════════════════════
--  PASO 0 (Manual): Deshabilitar confirmación de correo
--  Dashboard > Authentication > Providers > Email > desactiva "Confirm email"
--  Esto permite que los usuarios jueguen de inmediato sin verificar el correo.
-- ══════════════════════════════════════════════════════════════════════════════

-- ══════════════════════════════════════════════════════════════════════════════
--  TABLA: profiles
--  Una fila por usuario autenticado (UUID de auth.users).
--  Guarda username único, high_score y estadísticas del juego.
-- ══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS profiles (
    id           UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    username     TEXT NOT NULL UNIQUE,
    high_score   BIGINT DEFAULT 0,
    games_played INT DEFAULT 0,
    updated_at   TIMESTAMP WITH TIME ZONE DEFAULT TIMEZONE('utc', NOW())
);

CREATE INDEX IF NOT EXISTS idx_profiles_score ON profiles (high_score DESC);

ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Lectura publica de perfiles"
    ON profiles FOR SELECT USING (true);

CREATE POLICY "Insertar propio perfil"
    ON profiles FOR INSERT WITH CHECK (auth.uid() = id);

CREATE POLICY "Actualizar propio perfil"
    ON profiles FOR UPDATE USING (auth.uid() = id);

-- ══════════════════════════════════════════════════════════════════════════════
--  FASE 2: Columnas de progreso en profiles
--  Ejecutar si la tabla ya existe sin estas columnas (idempotente con IF NOT EXISTS).
--  Requeridas por AuthManager.UpsertProfileRoutine() y SupabaseManager.SyncProgressRoutine().
-- ══════════════════════════════════════════════════════════════════════════════
ALTER TABLE profiles ADD COLUMN IF NOT EXISTS max_unlocked_level INT DEFAULT 0;
ALTER TABLE profiles ADD COLUMN IF NOT EXISTS levels_completed   INT DEFAULT 0;
ALTER TABLE profiles ADD COLUMN IF NOT EXISTS times_played       INT DEFAULT 0;
ALTER TABLE profiles ADD COLUMN IF NOT EXISTS total_stars        INT DEFAULT 0;

-- ══════════════════════════════════════════════════════════════════════════════
--  TABLA: leaderboard (ranking global, soporta usuarios anon y autenticados)
-- ══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS leaderboard (
    user_id      TEXT PRIMARY KEY,
    username     TEXT NOT NULL,
    high_score   BIGINT DEFAULT 0,
    games_played INT DEFAULT 0,
    updated_at   TIMESTAMP WITH TIME ZONE DEFAULT TIMEZONE('utc', NOW()) NOT NULL,
    created_at   TIMESTAMP WITH TIME ZONE DEFAULT TIMEZONE('utc', NOW()) NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_leaderboard_score ON leaderboard (high_score DESC);

ALTER TABLE leaderboard ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Lectura publica del ranking"
    ON leaderboard FOR SELECT USING (true);

CREATE POLICY "Escritura libre de puntajes"
    ON leaderboard FOR INSERT WITH CHECK (true);

CREATE POLICY "Actualizacion libre de puntajes"
    ON leaderboard FOR UPDATE USING (true);

-- ══════════════════════════════════════════════════════════════════════════════
--  FUNCION: submit_score
--  Actualiza profiles (auth) Y leaderboard (ranking global).
--  GREATEST() garantiza que nunca se sobreescriba un record mayor.
-- ══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE FUNCTION submit_score(
    p_user_id   TEXT,
    p_username  TEXT,
    p_score     BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_prev_high     BIGINT;
    v_is_new_record BOOLEAN;
    v_auth_uuid     UUID;
BEGIN
    BEGIN
        v_auth_uuid := p_user_id::UUID;

        INSERT INTO profiles (id, username, high_score, games_played, updated_at)
        VALUES (v_auth_uuid, p_username, p_score, 1, NOW())
        ON CONFLICT (id) DO UPDATE SET
            high_score   = GREATEST(profiles.high_score, p_score),
            username     = p_username,
            games_played = profiles.games_played + 1,
            updated_at   = NOW()
        WHERE profiles.high_score < p_score OR profiles.username != p_username;

    EXCEPTION WHEN invalid_text_representation THEN
        v_auth_uuid := NULL;
    END;

    SELECT high_score INTO v_prev_high FROM leaderboard WHERE user_id = p_user_id;
    v_is_new_record := COALESCE((p_score > v_prev_high), true);

    INSERT INTO leaderboard (user_id, username, high_score, games_played, updated_at)
    VALUES (p_user_id, p_username, p_score, 1, NOW())
    ON CONFLICT (user_id) DO UPDATE SET
        high_score   = GREATEST(leaderboard.high_score, p_score),
        username     = p_username,
        games_played = leaderboard.games_played + 1,
        updated_at   = NOW()
    WHERE leaderboard.high_score < p_score OR leaderboard.username != p_username;

    RETURN jsonb_build_object(
        'success',       true,
        'is_new_record', v_is_new_record,
        'previous_high', COALESCE(v_prev_high, 0),
        'new_high',      p_score
    );

EXCEPTION WHEN OTHERS THEN
    RETURN jsonb_build_object('success', false, 'error', SQLERRM);
END;
$$;

-- ══════════════════════════════════════════════════════════════════════════════
--  FUNCION: get_top_scores
-- ══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE FUNCTION get_top_scores(limit_num INT DEFAULT 10)
RETURNS TABLE (posicion BIGINT, username TEXT, high_score BIGINT, updated_at TIMESTAMP WITH TIME ZONE)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
    RETURN QUERY
    SELECT ROW_NUMBER() OVER (ORDER BY l.high_score DESC),
           l.username, l.high_score, l.updated_at
    FROM leaderboard l
    ORDER BY l.high_score DESC
    LIMIT limit_num;
END;
$$;

-- ══════════════════════════════════════════════════════════════════════════════
--  FUNCION: get_player_rank
-- ══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE FUNCTION get_player_rank(p_user_id TEXT)
RETURNS TABLE (posicion BIGINT, username TEXT, high_score BIGINT, total_players BIGINT)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
    RETURN QUERY
    WITH ranked AS (
        SELECT l.user_id, l.username, l.high_score,
               ROW_NUMBER() OVER (ORDER BY l.high_score DESC) AS posicion
        FROM leaderboard l
    )
    SELECT r.posicion, r.username, r.high_score,
           (SELECT COUNT(*) FROM leaderboard)::BIGINT
    FROM ranked r WHERE r.user_id = p_user_id;
END;
$$;

-- ══════════════════════════════════════════════════════════════════════════════
--  FUNCION: check_username_available
--  Llámala desde Unity antes del registro para validar el nombre en tiempo real.
-- ══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE FUNCTION check_username_available(p_username TEXT)
RETURNS BOOLEAN
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
    RETURN NOT EXISTS (
        SELECT 1 FROM profiles WHERE LOWER(username) = LOWER(p_username)
    );
END;
$$;

-- ══════════════════════════════════════════════════════════════════════════════
--  VERIFICACION FINAL (descomentar para probar)
-- ══════════════════════════════════════════════════════════════════════════════
-- SELECT check_username_available('TestPlayer');
-- SELECT * FROM submit_score('device-test-001', 'TestPlayer', 5000);
-- SELECT * FROM get_top_scores(10);
-- SELECT * FROM get_player_rank('device-test-001');
