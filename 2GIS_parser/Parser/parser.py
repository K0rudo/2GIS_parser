import requests
import pyodbc
import sys
from datetime import datetime
from queries import QUERIES

conn_str = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost\\SQLEXPRESS;"
    "DATABASE=SvyazTransit2GIS;"
    "Trusted_Connection=yes;"
)

API_KEY = "45cedc02-a92a-48d0-be46-142b310411b4"
DAYS_MAP = [
    ("Mon", "Пн"), ("Tue", "Вт"), ("Wed", "Ср"),
    ("Thu", "Чт"), ("Fri", "Пт"), ("Sat", "Сб"), ("Sun", "Вс")
]

def get_connection():
    return pyodbc.connect(conn_str)

def search_organizations(city, query):
    url = "https://catalog.api.2gis.com/3.0/items"
    params = {
        "q": f"{query} {city}",
        "key": API_KEY,
        "page_size": 10,
        "fields": "items.contact_groups,items.address,items.rating,items.reviews,items.schedule,items.point,items.rubrics,items.adm_div"
    }

    response = requests.get(url, params=params)
    data = response.json()

    if "result" not in data or "items" not in data["result"]:
        return []

    items = data["result"]["items"]
    items = [i for i in items if i.get("rubrics")]
    return items

def save_organization(cursor, item, city, job_id):
    external_id = item.get("id", "")
    name = item.get("name", "")
    address = item.get("address_name", "")

    district = ""
    adm_div = item.get("adm_div", [])
    for div in adm_div:
        if div.get("type") == "district":
            district = div.get("name", "")
            break

    rubrics = item.get("rubrics", [])
    category = rubrics[0].get("name", "") if rubrics else ""

    reviews_data = item.get("reviews", {})
    rating = reviews_data.get("general_rating", None)
    reviews = reviews_data.get("general_review_count", None)

    point = item.get("point", {})
    lat = point.get("lat") if point else None
    lon = point.get("lon") if point else None

    working_hours = ""
    schedule = item.get("schedule", {})
    if schedule:
        parts = []
        for day_key, day_name in DAYS_MAP:
            info = schedule.get(day_key, {})
            if isinstance(info, dict) and info.get("working_hours"):
                wh = info["working_hours"][0]
                parts.append(f"{day_name} {wh.get('from', '')}-{wh.get('to', '')}")
            else:
                parts.append(f"{day_name} —")
        working_hours = ", ".join(parts)

    cursor.execute(QUERIES['select_org_by_external'], external_id)
    row = cursor.fetchone()

    if row:
        org_id = row[0]
        cursor.execute(QUERIES['update_org'],
            name, address, category, rating, reviews,
            lat, lon, working_hours, district, org_id)
        if item.get("contact_groups"):
            cursor.execute(QUERIES['delete_contacts'], org_id)
    else:
        cursor.execute(QUERIES['insert_org'],
            external_id, name, city, address, category,
            rating, reviews, lat, lon, working_hours, district)
        cursor.execute("SELECT @@IDENTITY")
        org_id = int(cursor.fetchone()[0])

    for group in item.get("contact_groups", []):
        for contact in group.get("contacts", []):
            ctype = contact.get("type", "other")
            value = contact.get("url") or contact.get("value", "")
            if not value:
                continue
            url_lower = value.lower()
            if "t.me" in url_lower or "telegram" in url_lower:
                ctype = "telegram"
            elif "wa.me" in url_lower or "whatsapp" in url_lower:
                ctype = "whatsapp"
            elif "vk.com" in url_lower:
                ctype = "vk"
            elif "instagram.com" in url_lower:
                ctype = "instagram"

            cursor.execute(QUERIES['insert_contact'], org_id, ctype, value)

    cursor.execute(QUERIES['link_job_org'], job_id, org_id, job_id, org_id)
    return org_id


def run_parser(job_id, city, query):
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute(QUERIES['set_status_running'], datetime.now(), job_id)
    conn.commit()

    try:
        items = search_organizations(city, query)
        for item in items:
            save_organization(cursor, item, city, job_id)
            conn.commit()

        cursor.execute(QUERIES['set_status_completed'], datetime.now(), len(items), job_id)
        conn.commit()
        print(f"Готово! Найдено: {len(items)}")

    except Exception as e:
        cursor.execute(QUERIES['set_status_failed'], str(e), job_id)
        conn.commit()
        print(f"Ошибка: {e}")
    finally:
        conn.close()


if __name__ == "__main__":
    job_id = int(sys.argv[1])
    city = sys.argv[2]
    query = sys.argv[3]
    run_parser(job_id, city, query)