# EKAA Payroll

A Xero-style payroll web app for New Zealand employers.
**ASP.NET Core 8 MVC · Entity Framework Core · MySQL 8 (MySQL Workbench) · Bootstrap 5**

This is **Phase 1** (foundation). Public holidays, leave, the full NZ PAYE engine, bank CSV export and IRD Gateway come in Phase 2 (see the end).

---

## 1. What Phase 1 does

| Area | Details |
|---|---|
| Sign up / sign in | ASP.NET Core Identity. Sign-up creates the **company and its first user** together. Every page requires login. |
| Multi-tenant safety | Every query is filtered by the signed-in user's company. |
| Company | Legal/trading name, IRD, NZBN, contact, address, default pay frequency, **company bank accounts**. |
| Employees | Personal info, residential address, emergency contact, employment type, hourly/salary pay, IRD number, tax code, KiwiSaver rates, **employee bank accounts**, deactivate/reactivate. |
| Schedule | Weekly roster grid (Mon–Sun). "Log hours" copies a shift into a timesheet. |
| Timesheets | Daily hours with unpaid break; overnight shifts handled; entries lock once paid. |
| Pay runs | Draft → review → **Post**. Overlapping periods are blocked; hours can't be paid twice. |
| Payslips | PDF download, and **email to one employee or everyone** with the PDF attached. |

---

## 2. Prerequisites

Install these once:

1. **.NET 8 SDK** – https://dotnet.microsoft.com/download (check with `dotnet --version`)
2. **MySQL Server 8** and **MySQL Workbench** – https://dev.mysql.com/downloads/installer/
   - During install, set a **root password** and remember it. Keep port `3306`.
3. **VS Code** (or Visual Studio) with the *C# Dev Kit* extension.
4. **EF Core CLI tool**:
   ```bash
   dotnet tool install --global dotnet-ef
   ```

---

## 3. Set up the database in MySQL Workbench

### 3.1 Connect

1. Open **MySQL Workbench**.
2. Click your connection (usually `root@localhost:3306`) and enter the root password.
3. If the connection fails, the MySQL service isn't running: open Windows **Services**, find **MySQL80**, click **Start**.

### 3.2 Create the database and the app's login

1. In the SQL editor tab, paste the following:

   ```sql
   CREATE DATABASE payroll_nz CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

   CREATE USER 'payroll_user'@'localhost' IDENTIFIED BY 'payroll_dev_password';
   GRANT ALL PRIVILEGES ON payroll_nz.* TO 'payroll_user'@'localhost';
   FLUSH PRIVILEGES;
   ```

2. Click the blue **Run** button (not the small arrow beside it).
3. Check the **Output** panel for green ticks, then click the refresh icon in the **SCHEMAS** panel. `payroll_nz` should appear.

What these lines do:

| Line | Purpose |
|---|---|
| `CREATE DATABASE payroll_nz` | An empty database. The app creates the tables itself. In Workbench a "schema" is a database. |
| `CREATE USER` | A dedicated login for the app, so it doesn't use `root`. |
| `GRANT ... ON payroll_nz.*` | The user can do anything, but only inside `payroll_nz`. |
| `FLUSH PRIVILEGES` | Applies the change immediately. |

> Use your own password if you prefer, but then update the connection string (next step) to match.

### 3.3 (Optional) Test the app login

In Workbench click **+** next to the connection list and create one with Hostname `127.0.0.1`, Port `3306`, Username `payroll_user`, then **Test Connection**. If it succeeds, the database side is ready.

---

## 4. Connection string

Open `src/PayrollNZ.Web/appsettings.Development.json`:

```json
"ConnectionStrings": {
  "Default": "Server=localhost;Port=3306;Database=payroll_nz;User=payroll_user;Password=payroll_dev_password;"
}
```

| Part | Meaning |
|---|---|
| `Server=localhost` | MySQL runs on this computer |
| `Port=3306` | MySQL's default port |
| `Database=payroll_nz` | The database from step 3.2 |
| `User` / `Password` | The login from step 3.2 |

If you used the SQL exactly as written, **change nothing**.

**Keeping a real password out of the project:** store it with user-secrets, which overrides the JSON and is never committed to Git:

```bash
cd src/PayrollNZ.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3306;Database=payroll_nz;User=payroll_user;Password=YOUR_PASSWORD;"
```

---

## 5. Build and run

Open a terminal in VS Code (**Terminal → New Terminal**) and run the commands one at a time:

```bash
cd src/PayrollNZ.Web
```
Go into the web project folder (it contains `PayrollNZ.Web.csproj`).

```bash
dotnet restore
```
Downloads the NuGet packages (EF Core, Pomelo MySQL driver, QuestPDF, MailKit).

```bash
dotnet build
```
Compiles the C# code and the Razor views. Fix anything that appears in red before continuing.

```bash
dotnet ef migrations add InitialCreate
```
Generates a `Migrations` folder describing all tables, based on the C# models. It only reads your code, not MySQL. **Run this once.** Run it again with a new name (e.g. `AddLeaveBalances`) whenever you change a model.

```bash
dotnet run
```
Starts the app. On startup it connects to `payroll_nz` and creates the tables automatically. Open the `https://localhost:xxxx` address printed in the terminal.

Useful extras:

| Command | Use |
|---|---|
| `dotnet watch run` | Restarts the app automatically when you edit code |
| `dotnet dev-certs https --trust` | Fixes the browser's HTTPS certificate warning (run once) |
| `Ctrl+C` in the terminal | Stops the app |

---

## 6. Try it end to end

1. Open the site → **Create an account** (this also creates your company).
2. **Company** → fill in details, set the pay frequency, add a company bank account.
3. **Employees → Add employee** (e.g. hourly, $28.50). On their page, add a bank account.
4. **Schedule → Add shift**, then click **Log hours** (or use **Timesheets → Log hours**).
5. **Pay runs → New pay run → Create draft** → review → **Post pay run**.
6. Download the PDF, or **Email all payslips** (needs SMTP, section 8).

---

## 7. See your data in MySQL Workbench

1. In **SCHEMAS**, click refresh, then expand **payroll_nz → Tables**.
2. Right-click a table → **Select Rows – Limit 1000**.

Or run SQL:

```sql
USE payroll_nz;

SELECT * FROM Companies;
SELECT * FROM Employees;
SELECT * FROM EmployeeBankDetails;
SELECT * FROM Shifts;
SELECT * FROM Timesheets;
SELECT * FROM PayRuns;
SELECT * FROM Payslips;
SELECT Id, UserName, Email, FullName, CompanyId FROM AspNetUsers;

-- Payslips with employee names
SELECT e.FirstName, e.LastName, p.GrossPay, p.PayeTax, p.NetPay
FROM Payslips p JOIN Employees e ON e.Id = p.EmployeeId;
```

| Table | Holds |
|---|---|
| `Companies`, `CompanyBankDetails` | Employer and its debit accounts |
| `AspNetUsers` (and other `AspNet…`) | Logins. Passwords are hashed, not readable. |
| `Employees`, `EmployeeBankDetails` | Employee details and credit accounts |
| `Shifts` | Rostered (planned) work |
| `Timesheets` | Hours worked; `PayRunId` is set once paid |
| `PayRuns`, `Payslips` | Pay periods and each employee's pay |

---

## 8. Email setup (optional, for sending payslips)

Use user-secrets so passwords never go into files:

```bash
cd src/PayrollNZ.Web
dotnet user-secrets init
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:Username" "you@example.com"
dotnet user-secrets set "Smtp:Password" "app-password"
dotnet user-secrets set "Smtp:FromAddress" "payroll@yourcompany.co.nz"
```

Until this is set, "Email payslip" shows "Email is not configured". For testing, [Mailpit](https://github.com/axllent/mailpit) or Mailtrap avoid sending real emails. Gmail users need an **app password**.

---

## 9. Troubleshooting

| Error | Cause / fix |
|---|---|
| `dotnet` is not recognized | .NET 8 SDK not installed. Install it and restart VS Code. |
| `dotnet-ef` not recognized | Run `dotnet tool install --global dotnet-ef`, reopen the terminal. |
| `Access denied for user 'payroll_user'` | Password in the connection string doesn't match step 3.2. |
| `Unable to connect to any of the specified MySQL hosts` | MySQL service isn't running. Start **MySQL80** in Windows Services. |
| `Unknown database 'payroll_nz'` | Step 3.2 was skipped, or the name differs. |
| No tables in Workbench | The app hasn't started successfully. Check the terminal output, then refresh SCHEMAS. |
| Build error after editing a file | Make sure the file is saved (**Ctrl+S**) before building. |
| Browser certificate warning | `dotnet dev-certs https --trust` |
| Using MariaDB instead of MySQL | One line in `Program.cs` changes (`MySqlServerVersion` → `MariaDbServerVersion`). |

---

## 10. Project layout

```
PayrollNZ.sln
src/PayrollNZ.Web/
  Program.cs                  Startup: database, Identity, login-required-by-default, services
  appsettings.json            Default settings (connection string placeholder, SMTP section)
  appsettings.Development.json  Local connection string
  Data/AppDbContext.cs        EF Core model: tables and relationships
  Models/                     Company, Employee, TimesheetEntry, Shift, PayRun, Payslip, ...
  Models/ViewModels/          Login/Register/Dashboard/Schedule/PayRun form models
  Controllers/                Account, Home, Company, Employees, Schedule, Timesheets, PayRuns
  Services/
    ITaxCalculator / BasicTaxCalculator   PLACEHOLDER tax, replaced in Phase 2
    PayrollCalculationService             Gross-to-net, extended in Phase 2
    PayRunService                         Create draft / post / delete draft
    PayslipPdfService                     PDF payslips (QuestPDF)
    EmailService                          SMTP email (MailKit)
  Views/                      Razor pages (sidebar layout)
  wwwroot/css/site.css        Styling on top of Bootstrap
```

**How it fits together (MVC):** the browser requests a URL → a **Controller** handles it → uses **Services** and the **Models** (via EF Core, which talks to MySQL) → hands data to a **View**, which renders HTML.

---

## 11. Known limits of Phase 1

- **Tax figures are placeholders.** `BasicTaxCalculator` annualises pay and applies bracket/flat rates, with no ACC, student loan, ESCT or IRD table rounding. Its rates are marked "VERIFY" and must be checked against IRD. **Do not pay real staff with it yet.**
- No public holiday or leave handling.
- Pay runs can't be edited after creation (delete the draft and recreate).
- No user roles, and no password-reset email flow.
- Bank and IRD numbers are stored as plain text. Add encryption before production.
- QuestPDF uses the Community licence; check its terms for your company size.

---

## 12. Phase 2 plan

1. **NZ PAYE engine:** replace `BasicTaxCalculator` behind `ITaxCalculator` (all tax codes, secondary tables, student loan, KiwiSaver + ESCT, ACC levy with annual cap). Rates live in a per-tax-year table, not in code.
2. **Leave and public holidays:** leave rules service, `EmployeeLeaveBalance`, NZ holiday calendar, annual/sick/bereavement leave, alternative holidays, RDP/ADP, wired into `PayrollCalculationService`.
3. **Bank CSV export:** `BankExportService` (BNZ, ASB, ANZ, Westpac, Kiwibank). The disabled "Download bank CSV" button on the pay run page is already in place.
4. **IRD Gateway Services:** registration, TLS client certificate, OAuth2, token storage, Payday Filing, and `PayRunAuditLog`.