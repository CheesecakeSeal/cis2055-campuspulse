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

### Investigator Account

- Email: investigator@campuspulse.local
- Password: Investigator123!
