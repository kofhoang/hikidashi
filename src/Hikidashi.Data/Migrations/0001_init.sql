CREATE TABLE IF NOT EXISTS facts (
    id          uuid PRIMARY KEY,
    content     text NOT NULL,
    keywords    text[] NOT NULL DEFAULT '{}',
    enriched    boolean NOT NULL DEFAULT false,
    metadata    jsonb NOT NULL DEFAULT '{}',
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

-- Exact array membership / overlap. Substring matching is plain ILIKE (unindexed,
-- fine at personal scale); no pg_trgm dependency.
CREATE INDEX IF NOT EXISTS facts_keywords_gin ON facts USING gin (keywords);
