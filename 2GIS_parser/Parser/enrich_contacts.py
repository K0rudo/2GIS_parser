import json
import pyodbc
import sys
import os
import tempfile

from parser_2gis.runner.cli import CLIRunner
from parser_2gis.config import Configuration

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


def names_match(item_name: str, org_name: str) -> bool:
    a = item_name.split(',')[0].strip().lower()
    b = org_name.split(',')[0].strip().lower()
    return a == b or a in b or b in a


def save_contacts(cursor, org_id: int, item: dict):
    cursor.execute("DELETE FROM Contacts WHERE OrganizationId = ?", org_id)
    for group in item.get('contact_groups', []):
        for contact in group.get('contacts', []):
            ctype = contact.get('type', 'other')
            value = contact.get('url') or contact.get('value', '')
            if not value:
                continue
            url_lower = value.lower()
            if ctype in ('website', 'extlink'):
                if 't.me' in url_lower or 'telegram' in url_lower:
                    ctype = 'telegram'
                elif 'wa.me' in url_lower or 'whatsapp' in url_lower:
                    ctype = 'whatsapp'
                    # Убираем предзаполненное сообщение от 2ГИС
                    if '?text=' in value:
                        value = value.split('?text=')[0]
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
    url = f"https://2gis.ru/{slug}/search/{search_query.replace(' ', '%20')}"

    cursor.execute("""
        SELECT o.Id, o.Name
        FROM Organizations o
        INNER JOIN ParseJobOrganizations pjo ON pjo.OrganizationId = o.Id
        WHERE pjo.ParseJobId = ?
    """, job_id)
    orgs = cursor.fetchall()

    if not orgs:
        conn.close()
        return

    org_lookup = {
        name.split(',')[0].strip().lower(): (org_id, name)
        for org_id, name in orgs
    }

    out_file = tempfile.mktemp(suffix='.json')
    try:
        config = Configuration()
        runner = CLIRunner(
            urls=[url],
            output_path=out_file,
            format='json',
            config=config
        )
        runner.start()
    except Exception:
        conn.close()
        return

    if not os.path.exists(out_file):
        conn.close()
        return

    try:
        with open(out_file, 'r', encoding='utf-8-sig') as f:
            items = json.load(f)
    except Exception:
        conn.close()
        return

    try:
        os.remove(out_file)
    except Exception:
        pass

    matched_ids = set()
    for item in items:
        item_name_key = item.get('name', '').split(',')[0].strip().lower()
        matched_org = None
        for key, (org_id, org_name) in org_lookup.items():
            if org_id in matched_ids:
                continue
            if names_match(item_name_key, key):
                matched_org = (org_id, org_name)
                break
        if not matched_org:
            continue
        org_id, org_name = matched_org
        matched_ids.add(org_id)
        save_contacts(cursor, org_id, item)
        conn.commit()

    conn.close()


if __name__ == '__main__':
    if len(sys.argv) < 2:
        sys.exit(1)
    enrich_contacts(int(sys.argv[1]))