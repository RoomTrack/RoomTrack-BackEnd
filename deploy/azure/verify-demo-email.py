"""Marks a demo e-mail (@roomtrack.pe) as verified in the identity database of the Azure deployment.

Usage: python deploy/azure/verify-demo-email.py <email>

The demo accounts use a domain nobody receives, so their verification links never arrive: seed-demo.ps1 passes this
script to the app's tool/seed_backend.py as SEED_EMAIL_VERIFIER. It reads MYSQL_SERVER_NAME, MYSQL_ADMIN_USER and
MYSQL_ADMIN_PASSWORD from roomtrack.azure.env, next to this file, and needs PyMySQL (pip install pymysql) and a
firewall rule for this machine (seed-demo.ps1 opens and removes it).
"""
import os
import sys

import pymysql

ENV_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "roomtrack.azure.env")
DEMO_DOMAIN = "@roomtrack.pe"


def settings():
    values = {}
    with open(ENV_FILE, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                values[key.strip()] = value.strip()
    return values


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    email = sys.argv[-1].strip().lower()
    if not email.endswith(DEMO_DOMAIN):
        sys.exit(f"Solo verifico correos demo {DEMO_DOMAIN}, no {email}.")
    s = settings()
    connection = pymysql.connect(host=f"{s['MYSQL_SERVER_NAME']}.mysql.database.azure.com", port=3306,
                                 user=s["MYSQL_ADMIN_USER"], password=s["MYSQL_ADMIN_PASSWORD"],
                                 database="roomtrack_identity", ssl={"ssl": {}}, connect_timeout=20)
    try:
        with connection.cursor() as cursor:
            changed = cursor.execute(
                "UPDATE users SET email_verified = 1, email_verified_at = UTC_TIMESTAMP(6) "
                "WHERE LOWER(email) = %s AND email_verified = 0", (email,))
            cursor.execute("SELECT COUNT(*) FROM users WHERE LOWER(email) = %s AND email_verified = 1", (email,))
            verified = cursor.fetchone()[0]
        connection.commit()
    finally:
        connection.close()
    if not verified:
        sys.exit(f"{email} no existe en roomtrack_identity.users.")
    print(f"    (verificado en la base: {email}{'' if changed else ', ya lo estaba'})")


if __name__ == "__main__":
    main()
