import json
import pyodbc
import sys
import os
import tempfile
import subprocess

conn_str = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost\\SQLEXPRESS;"
    "DATABASE=SvyazTransit2GIS;"
    "Trusted_Connection=yes;"
)

CITY_SLUGS = {
    'иркутск':     'irkutsk',
    'москва':      'moscow',
    'новосибирск': 'novosibirsk',
    'красноярск':  'krasnoyarsk',
    'ангарск':     'angarsk',
    'братск':      'bratsk',
    'улан-удэ':    'ulan-ude',
    'чита':        'chita',
    'хабаровск':   'khabarovsk',
    'владивосток': 'vladivostok',
}


def get_connection():
    return pyodbc.connect(conn_str)


def save_contacts(cursor, org_id: int, item: dict):
    cursor.execute("DELETE FROM Contacts WHERE OrganizationId = ?", org_id)
    for group in item.get('contact_groups', []):
        for contact in group.get('contacts', []):
            ctype = contact.get('type', 'other')
            value = contact.get('url') or contact.get('value', '')
            if not value:
                continue

            if '?text=' in value:
                value = value.split('?text=')[0]

            url_lower = value.lower()
            if ctype in ('website', 'extlink'):
                if 't.me' in url_lower or 'telegram' in url_lower:
                    ctype = 'telegram'
                elif 'wa.me' in url_lower or 'whatsapp' in url_lower:
                    ctype = 'whatsapp'
                elif 'vk.com' in url_lower:
                    ctype = 'vk'
                elif 'instagram.com' in url_lower:
                    ctype = 'instagram'
                elif 'viber://' in url_lower:
                    ctype = 'viber'

            try:
                cursor.execute(
                    "INSERT INTO Contacts (OrganizationId, ContactType, Value) VALUES (?, ?, ?)",
                    org_id, ctype, value
                )
            except Exception:
                pass


def parse_url(url, retries=2, timeout=60):
    for attempt in range(retries):
        out_file = tempfile.mktemp(suffix='.json')
        proc = subprocess.Popen(
            ['parser-2gis', '-i', url, '-f', 'json', '-o', out_file]
        )
        try:
            proc.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()
            try:
                os.remove(out_file)
            except Exception:
                pass
            if attempt < retries - 1:
                continue
            return []

        if not os.path.exists(out_file):
            if attempt < retries - 1:
                continue
            return []

        try:
            with open(out_file, 'r', encoding='utf-8-sig') as f:
                items = json.load(f)
            try:
                os.remove(out_file)
            except Exception:
                pass
            return items
        except Exception:
            if attempt < retries - 1:
                continue
            return []

    return []


def enrich_contacts(job_id: int):
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("SELECT City, SearchQuery FROM ParseJobs WHERE Id = ?", job_id)
    job = cursor.fetchone()
    if not job:
        conn.close()
        return

    city, search_query = job
    slug = CITY_SLUGS.get(city.lower(), city.lower())

    # Берём организации с районом и ExternalId
    cursor.execute("""
        SELECT o.Id, o.Name, o.ExternalId, o.District
        FROM Organizations o
        INNER JOIN ParseJobOrganizations pjo ON pjo.OrganizationId = o.Id
        WHERE pjo.ParseJobId = ?
    """, job_id)
    orgs = cursor.fetchall()

    if not orgs:
        conn.close()
        return

    # Словарь ExternalId -> org_id
    ext_lookup = {
        external_id: org_id
        for org_id, name, external_id, district in orgs
        if external_id
    }

    # Группируем организации по районам
    district_groups = {}
    """no_district = []"""

    for org_id, name, external_id, district in orgs:
        if district:
            if district not in district_groups:
                district_groups[district] = []
            district_groups[district].append((org_id, external_id))
            """else:
            no_district.append((org_id, external_id))"""

    # Для каждого района — отдельный поиск
    for district, district_orgs in district_groups.items():
        url = f"https://2gis.ru/{slug}/search/{search_query.replace(' ', '%20')}%20{district.replace(' ', '%20')}"
        items = parse_url(url)

        for item in items:
            full_id = item.get('id', '')
            ext_id = full_id.split('_')[0] if '_' in full_id else full_id

            if ext_id not in ext_lookup:
                continue

            org_id = ext_lookup[ext_id]
            save_contacts(cursor, org_id, item)
            conn.commit()

    """# Организации без района
    if no_district:
        url = f"https://2gis.ru/{slug}/search/{search_query.replace(' ', '%20')}"
        items = parse_url(url)

        for item in items:
            full_id = item.get('id', '')
            ext_id = full_id.split('_')[0] if '_' in full_id else full_id

            if ext_id not in ext_lookup:
                continue

            org_id = ext_lookup[ext_id]
            save_contacts(cursor, org_id, item)
            conn.commit()"""

    conn.close()


if __name__ == '__main__':
    if len(sys.argv) < 2:
        sys.exit(1)
    enrich_contacts(int(sys.argv[1]))