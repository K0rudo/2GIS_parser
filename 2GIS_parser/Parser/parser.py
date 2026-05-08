import requests
import pyodbc
import sys
from datetime import datetime

# Подключение к БД
conn_str = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost\\SQLEXPRESS;"
    "DATABASE=SvyazTransit2GIS;"
    "Trusted_Connection=yes;"
)

API_KEY = "d18a4a4a-f927-44e1-82fc-77656e228906"


def get_connection():
    return pyodbc.connect(conn_str)


"""def search_organizations(city, query):
    url = "https://catalog.api.2gis.com/3.0/items"
    params = {
        "q": f"{query} {city}",
        "key": API_KEY,
        "page_size": 10,
        "fields": "items.contact_groups,items.address,items.rating,items.reviews,items.schedule,items.point,items.rubrics"
    }

    results = []
    page = 1

    while True:
        params["page"] = page
        response = requests.get(url, params=params)
        data = response.json()

        if "result" not in data or "items" not in data["result"]:
            break

        items = data["result"]["items"]
        if not items:
            break

        results.extend(items)

        if len(items) < 10:
            break
        page += 1

    return results"""

def search_organizations(city, query):
    url = "https://catalog.api.2gis.com/3.0/items"
    params = {
        "q": f"{query} {city}",
        "key": API_KEY,
        "page_size": 10,
        "fields": "items.contact_groups,items.address,items.rating,items.reviews,items.schedule,items.point,items.rubrics"
    }

    response = requests.get(url, params=params)
    data = response.json()

    if "result" not in data or "items" not in data["result"]:
        return []

    return data["result"]["items"]

def save_organization(cursor, item, city, job_id):
    external_id = item.get("id", "")
    name = item.get("name", "")
    address = item.get("address_name", "")

    # Категория
    rubrics = item.get("rubrics", [])
    category = rubrics[0].get("name", "") if rubrics else ""

    # Рейтинг и отзывы
    reviews_data = item.get("reviews", {})
    rating = reviews_data.get("org_rating", None)
    reviews = reviews_data.get("org_review_count", None)

    # Координаты
    point = item.get("point", {})
    lat = point.get("lat") if point else None
    lon = point.get("lon") if point else None

    # Часы работы
    working_hours = ""
    schedule = item.get("schedule", {})
    if schedule:
        days_map = [
            ("Mon", "Пн"), ("Tue", "Вт"), ("Wed", "Ср"),
            ("Thu", "Чт"), ("Fri", "Пт"), ("Sat", "Сб"), ("Sun", "Вс")
        ]
        parts = []
        for day_key, day_name in days_map:
            info = schedule.get(day_key, {})
            if isinstance(info, dict) and info.get("working_hours"):
                wh = info["working_hours"][0]
                parts.append(f"{day_name} {wh.get('from', '')}-{wh.get('to', '')}")
            else:
                parts.append(f"{day_name} —")
        working_hours = ", ".join(parts)

    # Проверяем дубликат
    cursor.execute("SELECT Id FROM Organizations WHERE ExternalId = ?", external_id)
    row = cursor.fetchone()

    if row:
        # Запись существует — ОБНОВЛЯЕМ
        org_id = row[0]
        cursor.execute("""
            UPDATE Organizations 
            SET Name=?, Address=?, CategoryName=?, Rating=?, ReviewCount=?, 
                Latitude=?, Longitude=?, WorkingHours=?, UpdatedAt=GETDATE()
            WHERE Id=?
        """, name, address, category, rating, reviews, lat, lon, working_hours, org_id)
        cursor.execute("DELETE FROM Contacts WHERE OrganizationId=?", org_id)
    else:
        # Записи нет — ВСТАВЛЯЕМ
        cursor.execute("""
            INSERT INTO Organizations (ExternalId, Name, City, Address, CategoryName, Rating, ReviewCount, Latitude, Longitude, WorkingHours)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, external_id, name, city, address, category, rating, reviews, lat, lon, working_hours)
        cursor.execute("SELECT @@IDENTITY")
        org_id = int(cursor.fetchone()[0])

    # Контакты из API (для платного ключа)
    for group in item.get("contact_groups", []):
        for contact in group.get("contacts", []):
            ctype = contact.get("type", "other")
            # Сайты и соцсети используют поле 'url', остальные — 'value'
            value = contact.get("url") or contact.get("value", "")
            if not value:
                continue
            # Определяем соцсети по URL
            url_lower = value.lower()
            if "t.me" in url_lower or "telegram" in url_lower:
                ctype = "telegram"
            elif "wa.me" in url_lower or "whatsapp" in url_lower:
                ctype = "whatsapp"
            elif "vk.com" in url_lower:
                ctype = "vk"
            elif "instagram.com" in url_lower:
                ctype = "instagram"

            cursor.execute("""
                INSERT INTO Contacts (OrganizationId, ContactType, Value)
                VALUES (?, ?, ?)
            """, org_id, ctype, value)

    # Связываем с заданием
    cursor.execute("""
        IF NOT EXISTS (SELECT 1 FROM ParseJobOrganizations WHERE ParseJobId=? AND OrganizationId=?)
        INSERT INTO ParseJobOrganizations VALUES (?, ?)
    """, job_id, org_id, job_id, org_id)

    return org_id


def run_parser(job_id, city, query):
    conn = get_connection()
    cursor = conn.cursor()

    cursor.execute("UPDATE ParseJobs SET Status='Running', StartedAt=? WHERE Id=?", datetime.now(), job_id)
    conn.commit()

    try:
        items = search_organizations(city, query)
        """
        for item in items:
            print(f"=== {item.get('name')} ===")
            print(f"contact_groups: {item.get('contact_groups', 'НЕТ')}")
            print()
        """
        for item in items:
            save_organization(cursor, item, city, job_id)
            conn.commit()

        cursor.execute("""
            UPDATE ParseJobs SET Status='Completed', CompletedAt=?, TotalFound=? WHERE Id=?
        """, datetime.now(), len(items), job_id)
        conn.commit()
        print(f"Готово! Найдено: {len(items)}")

    except Exception as e:
        cursor.execute("UPDATE ParseJobs SET Status='Failed', ErrorMessage=? WHERE Id=?", str(e), job_id)
        conn.commit()
        print(f"Ошибка: {e}")
    finally:
        conn.close()


if __name__ == "__main__":
    job_id = int(sys.argv[1])
    city = sys.argv[2]
    query = sys.argv[3]
    run_parser(job_id, city, query)