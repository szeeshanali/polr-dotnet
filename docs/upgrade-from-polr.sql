-- Prepare an existing Polr 2.x MySQL database for Polr.NET.
--
-- 1. Back up your database first:   mysqldump polr > polr-backup.sql
-- 2. Run this script once:          mysql polr < docs/upgrade-from-polr.sql
-- 3. Point ConnectionStrings:PolrNet at the database and start Polr.NET.
--
-- Links, users (including their passwords), API keys and click stats are all kept.

-- Endings become case-sensitive, so base-62 endings like "aB" and "Ab" can both exist.
-- Polr's collation was case-insensitive, so no existing endings clash.
ALTER TABLE links
    MODIFY short_url VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    MODIFY creator VARCHAR(255) NOT NULL DEFAULT '',
    MODIFY secret_key VARCHAR(255) NOT NULL DEFAULT '',
    ADD INDEX links_creator_index (creator, is_api, created_at);

-- Polr stored these flags and numbers as strings.
ALTER TABLE users
    MODIFY active TINYINT(1) NOT NULL DEFAULT 0,
    MODIFY api_quota INT NOT NULL DEFAULT 60,
    ADD INDEX users_email_index (email),
    ADD INDEX users_api_key_index (api_key);

-- Speeds up the stats pages and the analytics API.
ALTER TABLE clicks
    ADD INDEX clicks_link_created_index (link_id, created_at);
