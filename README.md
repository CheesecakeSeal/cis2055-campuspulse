## Running CampusPulse

### Requirements

- .NET 9 SDK
- SQL Server Express LocalDB

### Database

If the database does not create automatically, run this from the CampusPulse project folder:
```powershell
dotnet ef database update
```
If dotnet ef is not installed:
```powershell
dotnet tool install --global dotnet-ef --version 9.0.12
```

## Creating an Investigator Account

For security reasons, the application does not include a default investigator account or hardcoded investigator password.

To create an investigator account:

1. Run the application once so the database is created.
2. Register a normal account using the email address and display name that should become an investigator.
3. Open SQL Server Management Studio and connect to:

```text
(localdb)\MSSQLLocalDB
```

4. Select the CampusPulse database.
5. Run this query, adding in whatever email address you wish in place of youremail@example.com:

```sql
   IF NOT EXISTS (
       SELECT 1
       FROM dbo.InvestigatorEmails
       WHERE NormalizedEmail = UPPER('youremail@example.com')
   )
   BEGIN
       INSERT INTO dbo.InvestigatorEmails (Email, NormalizedEmail)
       VALUES ('youremail@example.com', UPPER('youremail@example.com'));
   END
   ```

6. Log out and log back in with that account.
