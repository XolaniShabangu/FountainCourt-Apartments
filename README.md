# Fountain Court Residents – Rental & Tenant Management Web App

A web-based property management system that digitalizes Fountain Court’s rental workflow — from online tenant application and ID verification to lease signing, payments, maintenance, and analytics.  
The app supports **three roles**: **Landlord**, **Tenant**, and **Repairman**, automating all communications, payments, and service tracking.

---

## 🏡 Overview

Fountain Court is a 40-unit residential property (20 one-bedroom, 15 two-bedroom, 5 three-bedroom units) that accommodates over 2 600 residents.  
Previously, all applications, leases, and payments were handled manually with paper records, causing errors, delays, and poor visibility.  
This web app centralizes those processes to improve efficiency, transparency, and tenant satisfaction:contentReference[oaicite:0]{index=0}.

---

## ⚙️ Core Features

| Module | Description |
|--------|--------------|
| **Online Application & ID OCR** | Tenants apply online, upload their ID (front PDF/image) → the system scans using **Azure Computer Vision**, auto-fills key data, and verifies document type:contentReference[oaicite:1]{index=1}. |
| **Lease E-Signing & Payment** | Approved applicants receive a one-time **lease link**, sign digitally, and proceed to online payment (deposit/first-month rent). |
| **Automated Email Flow** | Emails for approval, decline, lease signing, receipts, and credentials are auto-sent. |
| **Tenant Portal** | After payment, tenants can log in, see balance/history, pay future months, and request emailed receipts. |
| **Maintenance Requests** | Tenants create tickets with photos; the system auto-assigns jobs to registered repairmen, who can update progress and close tasks:contentReference[oaicite:2]{index=2}. |
| **Repairman Portal** | Repairmen only see their assigned jobs; can mark them *In Progress* or *Completed*. |
| **Landlord Dashboard** | Displays units (occupied / vacant), repairman ratings, job graphs, and rent stats:contentReference[oaicite:3]{index=3}. |
| **Notices Board** | Landlord can publish up to **2 notices** shown atop all tenant dashboards (new notices overwrite old ones):contentReference[oaicite:4]{index=4}. |
| **Manage Tenants** | Expandable tenant cards show payment history and “Remove Tenant” to free up rooms:contentReference[oaicite:5]{index=5}. |

---

## 🧭 Main System Flow

### 1️⃣ Landlord Initial Setup
1. Log in as **Landlord**  
   → `/Account/Login`  
   Credentials: `landlord@fountaincourt.local` / `Landlord@12345`:contentReference[oaicite:6]{index=6}  
2. Navigate to **Room Types** → add or confirm room categories and quantities.  
3. Home page updates pricing dynamically per configured type.  
   _(Screenshot: `/Landlord/RoomTypes`)_  

---

### 2️⃣ Tenant Application
1. Visit the home page (`/`) → click **Apply Now**.  
   _(Screenshot: `/`)_
2. Upload ID (front PDF/image) → click **Scan** → auto-fill personal fields.  
3. Enter contact info, choose room type, upload bank statement, then **Submit**.  
   _(Screenshot: `/Applications/Create`)_  
4. Application stored with status *New*.  

---

### 3️⃣ Landlord Review
1. Log in → go to **Applications** (`/Landlord/Applications`).  
2. View ID & bank statement.  
3. Click **Approve** or **Reject**.  
   - On *Approve*: system emails lease link.  
   - On *Reject*: decline email sent.  
   _(Screenshot: `/Landlord/Applications/Details/{id}`)_  

---

### 4️⃣ Tenant Lease Signing & Payment
1. Tenant opens the emailed **lease link** (`/Lease/Sign?token=...`).  
2. Reviews document, signs digitally, clicks **Proceed to Pay**.  
3. Redirected to checkout (`/Payments/Checkout?applicationId=...`).  
4. After payment, tenant receives credentials by email and becomes **Active Tenant**.  
   _(Screenshots: `/Lease/Sign?token=...`, `/Payments/Checkout?...`)_  

---

### 5️⃣ Tenant Portal
- Dashboard shows lease details, notices, and payment summary.  
  _(Screenshot: `/Tenant/Dashboard`)_  
- Maintenance → create ticket (photo optional) → submit.  
  _(Screenshot: `/Tenant/Maintenance/Create`)_  
- Payment History → request emailed receipts or pay months ahead.  
- Notices appear on top banner when published by landlord:contentReference[oaicite:7]{index=7}.  

---

### 6️⃣ Repairman Portal
- Receives login details by email.  
- Views assigned jobs only (`/Repairman/Jobs`).  
- Marks job *Start* → *In Progress* → *Complete*.  
- Completed jobs become rateable by tenant (`/Tenant/Maintenance/Rate/{id}`).  
  _(Screenshots: `/Repairman/Jobs`, `/Repairman/Jobs/Details/{id}`)_  

---

### 7️⃣ Landlord Insights & Notices
- Dashboard graphs: occupancy %, vacant rooms, repairman ratings, jobs stats:contentReference[oaicite:8]{index=8}.  
- Notices: create/update alerts (e.g., electricity outage).  
  _(Screenshot: `/Landlord/Notices`)_  

---

## 🧩 Tech Stack

- ASP.NET MVC 5 (Visual Studio 2022)  
- Entity Framework 6 (DB-First)  
- SQL Server (LocalDB)  
- Azure Computer Vision API (ID OCR)  
- Stripe / Payment Gateway (Test Mode)  
- MailKit SMTP emails  
- Rotativa PDF receipts  
- AutoMapper / Newtonsoft.Json  

---

## 🔑 Default Demo Accounts

| Role | Email | Password |
|------|-------|-----------|
| Landlord | landlord@fountaincourt.local | Landlord@12345 |
| Tenant (Generated after payment) | — | Sent via email |
| Repairman (Added by Landlord) | — | Sent via email |

---

## 🧱 Database Entities
1. Users (Landlord, Tenant, Repairman)  
2. Applications  
3. Leases  
4. Payments  
5. MaintenanceJobs  
6. Notices  
7. RoomTypes  
8. Ratings  

---

## 🖼️ Screenshot Map (for docs/screenshots)

| Section | Capture | URL |
|----------|----------|-----|
| Landing page | Home with room types and pricing | `/` |
| Login screen | Landlord login page | `/Account/Login` |
| Room Type config | Manage Room Types list | `/Landlord/RoomTypes` |
| Tenant Apply | ID upload + scan form | `/Applications/Create` |
| Landlord Applications | All applications list | `/Landlord/Applications` |
| Application Detail | Decision view | `/Landlord/Applications/Details/{id}` |
| Lease Sign | Lease page before payment | `/Lease/Sign?token=...` |
| Payment Checkout | Stripe/Payment screen | `/Payments/Checkout?...` |
| Tenant Dashboard | After login – overview + notice | `/Tenant/Dashboard` |
| Maintenance New | Ticket creation form | `/Tenant/Maintenance/Create` |
| Maintenance List | Tenant ticket list + status | `/Tenant/Maintenance` |
| Repairman Jobs | Assigned job list | `/Repairman/Jobs` |
| Repairman Details | Job progress view | `/Repairman/Jobs/Details/{id}` |
| Rate Job | Tenant rating screen | `/Tenant/Maintenance/Rate/{id}` |
| Landlord Dashboard | Graphs and analytics | `/Landlord/Dashboard` |
| Notices | Manage tenant notices | `/Landlord/Notices` |
| Manage Tenants | Expand tenant card + Remove btn | `/Landlord/Tenants` |
| Applications Filter | Sort by status (accepted/rejected/past) | `/Landlord/Applications?status=Accepted` |

---

## 🧾 Notes & Highlights

- Tenants can pay **months ahead**; payment history auto-updates.  
- Landlord can only post **two active notices** → newest replaces oldest.  
- **Greyed-out room types** = unavailable (quantity = 0).  
- Home page prices load dynamically from configured room types.  
- **Remove Tenant** frees room and locks account.  
- **Ratings** aggregate into repairman scores for analytics:contentReference[oaicite:9]{index=9}.  

---

## 📦 Setup (Developer)

1. Clone repo and open in Visual Studio 2022.  
2. Verify connection string in `Web.config`.  
3. Run SQL LocalDB or Server Express instance.  
4. Restore NuGet packages, then `Ctrl + F5`.  

---

## 📄 License
MIT License — for educational and non-commercial use.  
