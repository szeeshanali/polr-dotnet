-- Table and column names stay compatible with Polr 2.x databases, so they can be imported
-- (run docs/upgrade-from-polr.sql once on such a database first).

CREATE TABLE IF NOT EXISTS links (
    id            INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    short_url     VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    long_url      LONGTEXT     NOT NULL,
    long_url_hash VARCHAR(10)  NULL,
    ip            VARCHAR(255) NOT NULL,
    creator       VARCHAR(255) NOT NULL DEFAULT '',
    clicks        INT          NOT NULL DEFAULT 0,
    secret_key    VARCHAR(255) NOT NULL DEFAULT '',
    is_disabled   TINYINT(1)   NOT NULL DEFAULT 0,
    is_custom     TINYINT(1)   NOT NULL DEFAULT 0,
    is_api        TINYINT(1)   NOT NULL DEFAULT 0,
    created_at    DATETIME     NULL,
    updated_at    DATETIME     NULL,
    UNIQUE KEY links_short_url_unique (short_url),
    KEY links_long_url_index (long_url_hash),
    KEY links_creator_index (creator, is_api, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS users (
    id           INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    username     VARCHAR(255) NOT NULL,
    password     VARCHAR(255) NOT NULL,
    email        VARCHAR(255) NOT NULL,
    ip           VARCHAR(255) NOT NULL DEFAULT '',
    recovery_key VARCHAR(255) NOT NULL DEFAULT '',
    role         VARCHAR(255) NOT NULL DEFAULT '',
    active       TINYINT(1)   NOT NULL DEFAULT 0,
    api_key      VARCHAR(255) NULL,
    api_active   TINYINT(1)   NOT NULL DEFAULT 0,
    api_quota    INT          NOT NULL DEFAULT 60,
    created_at   DATETIME     NULL,
    updated_at   DATETIME     NULL,
    UNIQUE KEY users_username_unique (username),
    KEY users_email_index (email),
    KEY users_api_key_index (api_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS clicks (
    id           INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    link_id      INT UNSIGNED NOT NULL,
    ip           VARCHAR(255) NOT NULL,
    country      VARCHAR(255) NULL,
    referer      VARCHAR(255) NULL,
    referer_host VARCHAR(255) NULL,
    user_agent   TEXT         NULL,
    created_at   DATETIME     NULL,
    updated_at   DATETIME     NULL,
    KEY clicks_link_created_index (link_id, created_at),
    CONSTRAINT clicks_link_id_foreign FOREIGN KEY (link_id) REFERENCES links (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
