-- Database per service: each service owns one schema and connects with its own user, which can only see that
-- schema (no service can read or join another service's tables). Run once by the MySQL image when the data
-- volume is created. Passwords are for local development only.

CREATE DATABASE IF NOT EXISTS roomtrack_identity;
CREATE DATABASE IF NOT EXISTS roomtrack_accommodations;
CREATE DATABASE IF NOT EXISTS roomtrack_bookings;
CREATE DATABASE IF NOT EXISTS roomtrack_profiles;
CREATE DATABASE IF NOT EXISTS roomtrack_analytics;

CREATE USER IF NOT EXISTS 'identity_svc'@'%' IDENTIFIED BY 'identity_dev';
CREATE USER IF NOT EXISTS 'accommodations_svc'@'%' IDENTIFIED BY 'accommodations_dev';
CREATE USER IF NOT EXISTS 'bookings_svc'@'%' IDENTIFIED BY 'bookings_dev';
CREATE USER IF NOT EXISTS 'profiles_svc'@'%' IDENTIFIED BY 'profiles_dev';
CREATE USER IF NOT EXISTS 'analytics_svc'@'%' IDENTIFIED BY 'analytics_dev';

GRANT ALL PRIVILEGES ON roomtrack_identity.* TO 'identity_svc'@'%';
GRANT ALL PRIVILEGES ON roomtrack_accommodations.* TO 'accommodations_svc'@'%';
GRANT ALL PRIVILEGES ON roomtrack_bookings.* TO 'bookings_svc'@'%';
GRANT ALL PRIVILEGES ON roomtrack_profiles.* TO 'profiles_svc'@'%';
GRANT ALL PRIVILEGES ON roomtrack_analytics.* TO 'analytics_svc'@'%';

-- The developer user of `dotnet run` / `dotnet ef` (appsettings.Development.json) can use every schema.
CREATE USER IF NOT EXISTS 'roomtrack'@'%' IDENTIFIED BY 'roomtrack_dev';
GRANT ALL PRIVILEGES ON `roomtrack\_%`.* TO 'roomtrack'@'%';

FLUSH PRIVILEGES;
