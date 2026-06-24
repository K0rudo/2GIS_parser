QUERIES = {
    'get_job': """
        SELECT City, SearchQuery FROM ParseJobs WHERE Id = ?
    """,

    'get_orgs_by_job': """
        SELECT o.Id, o.Name, o.ExternalId, o.District
        FROM Organizations o
        INNER JOIN ParseJobOrganizations pjo ON pjo.OrganizationId = o.Id
        WHERE pjo.ParseJobId = ?
    """,

    'delete_contacts': """
        DELETE FROM Contacts WHERE OrganizationId = ?
    """,

    'insert_contact': """
        INSERT INTO Contacts (OrganizationId, ContactType, Value) VALUES (?, ?, ?)
    """,

    'select_org_by_external': """
        SELECT Id FROM Organizations WHERE ExternalId = ?
    """,

    'update_org': """
        UPDATE Organizations 
        SET Name=?, Address=?, CategoryName=?, Rating=?, ReviewCount=?, 
            Latitude=?, Longitude=?, WorkingHours=?, District=?, UpdatedAt=GETDATE()
        WHERE Id=?
    """,

    'insert_org': """
        INSERT INTO Organizations 
            (ExternalId, Name, City, Address, CategoryName, Rating, ReviewCount, 
             Latitude, Longitude, WorkingHours, District)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """,

    'link_job_org': """
        IF NOT EXISTS (
            SELECT 1 FROM ParseJobOrganizations 
            WHERE ParseJobId=? AND OrganizationId=?
        )
        INSERT INTO ParseJobOrganizations VALUES (?, ?)
    """,

    'set_status_running': """
        UPDATE ParseJobs SET Status='Running', StartedAt=? WHERE Id=?
    """,

    'set_status_completed': """
        UPDATE ParseJobs SET Status='Completed', CompletedAt=?, TotalFound=? WHERE Id=?
    """,

    'set_status_failed': """
        UPDATE ParseJobs SET Status='Failed', ErrorMessage=? WHERE Id=?
    """,
}