# LoadHitch

A Blazor-based web application designed to efficiently connect shippers with available truck drivers. This project was developed as the final group project for the CSE325 course.

**[► View the Live Deployed Application on Azure](https://loadhitch.azurewebsites.net)**


## Table of Contents
1. [Project Overview](#project-overview)
2. [Core Features](#core-features)
3. [Technology Stack](#technology-stack)
4. [Getting Started (Local Setup)](#getting-started-local-setup)
5. [Usage](#usage)
6. [Contributors](#contributors)
7. [License](#license)

## Project Overview

LoadHitch is a streamlined logistics platform that addresses the challenge of connecting businesses that need to ship goods with independent truck drivers looking for loads. The application features two distinct dashboards: a "Loads Dashboard" where customers can post shipping jobs, and a "Trucks Dashboard" where drivers can post their availability and routes. This creates a transparent and efficient marketplace, reducing the friction for businesses to find transport and helping drivers maximize their profitability by minimizing wasted trips.

## Core Features

-   ✅ **Role-Based Authentication:** Secure user registration and login for three distinct roles: **Customer**, **Driver**, and **Admin**.
-   ✅ **Google OAuth Integration:** Users can sign up or log in using their Google accounts for convenience.
-   ✅ **Loads Dashboard:** Full CRUD (Create, Read, Update, Delete) functionality for customers to post and manage their shipping jobs.
-   ✅ **Trucks Dashboard:** Full CRUD functionality for drivers to post and manage their availability, location, and routes.
-   ✅ **Booking System:** A simple, status-based system for drivers to "Claim" jobs and for customers to "Book" trucks.
-   ✅ **Fully Responsive UI:** The application is designed to be fully functional and user-friendly across desktop, tablet, and mobile devices.

## Technology Stack

-   **Frontend:** .NET Blazor Server
-   **Backend:** ASP.NET Core
-   **Database:** PostgreSQL
-   **ORM:** Entity Framework Core
-   **Authentication:** ASP.NET Core Identity, Google OAuth 2.0
-   **Deployment & CI/CD:** Microsoft Azure (App Service, Azure DB for PostgreSQL) & GitHub Actions
-   **Testing:** xUnit (Unit Tests), bUnit (Component Tests), Playwright (End-to-End Tests)

## Getting Started (Local Setup)

Follow these instructions to get a copy of the project up and running on your local machine for development and testing purposes.

### Prerequisites

You will need the following software installed on your machine:
-   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-   [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or [Visual Studio Code](https://code.visualstudio.com/)
-   A local [PostgreSQL](https://www.postgresql.org/download/) instance

### Installation & Setup

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/thandosim/cse325t12Project.git
    cd cse325t12Project
    ```

2.  **Configure the Database Connection:**
    -   Create a new, empty database in your local PostgreSQL instance (e.g., `loadhitch_dev`).
    -   Open the `appsettings.Development.json` file located in the `src/LoadHitch.Web` project.
    -   Update the `DefaultConnection` string with your local PostgreSQL credentials:
        ```json
        "ConnectionStrings": {
          "DefaultConnection": "Host=localhost;Database=loadhitch_dev;Username=your_username;Password=your_password"
        }
        ```

3.  **Restore Dependencies and Apply Migrations:**
    -   Open a terminal in the root directory of the repository.
    -   Restore the .NET packages:
        ```bash
        dotnet restore
        ```
    -   Apply the Entity Framework migrations to create the database schema:
        ```bash
        dotnet ef database update --project src/LoadHitch.Web
        ```

4.  **Run the Application:**
    ```bash
    dotnet run --project src/LoadHitch.Web
    ```
    The application will be running at `https://localhost:7XXX` and `http://localhost:5XXX`.

### Azure PostgreSQL connection

The Blazor Server app now reads its production database credentials from an `.env` file so secrets never live in source control.

1. Copy `.env` from the project root (or create it if missing) and keep it local. Git already ignores the file.
2. Replace `REPLACE_ME` in the `AZURE_POSTGRES_CONNECTION` entry with the actual password for `lhadmin@loadhitch`:

   ```env
   AZURE_POSTGRES_CONNECTION="Server=loadhitch.postgres.database.azure.com;Database=postgres;Port=5432;User Id=lhadmin;Password=<your password>;Ssl Mode=Require;"
   ```

3. Run `dotnet restore` (first time only) and `dotnet run`. The home page will display whether the Azure PostgreSQL database can be reached.

## Usage

Once the application is running, you can explore its features:
-   **Register an Account:** You can create separate accounts with the **Customer** and **Driver** roles to see the different features available to each.
-   **As a Customer:** Log in and navigate to the "Post a Load" page to create a new shipping job. You can manage your posts from the "My Loads" dashboard.
-   **As a Driver:** Log in and post your availability on the "Trucks Dashboard". You can then browse the "Loads Dashboard" to find and claim jobs.

## Contributors

This project was a collaborative effort by the following team members:

| Name                      | Role                              |
| ------------------------- | --------------------------------- |
| **Thandokuhle Simelane**  | Project Lead / Product Owner      |
| **Ivan Sembetya**         | Backend & Auth Lead               |
| **George Omondi Olwal**   | Frontend (Blazor) Lead            |
| **Brighton Dube**         | DevOps & Deployment Lead          |
| **Tinashe Allan Kutenaire**| QA & Documentation Lead           |

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

Role	Email	Password
Admin	admin@loadhitch.com	Admin@123456!
Driver	driver@loadhitch.com	Driver@123456!
Customer	customer@loadhitch.com	Customer@123456!

Email: testauth999@example.com
Password: Test@12345!
Role: Customer


Role	Email	Password	Notes
Admin	admin@loadhitch.com	Admin@123456!	System administrator
Driver	driver1@loadhitch.com	Driver@123456!	Owns Truck T001 (Flatbed)
Driver	driver2@loadhitch.com	Driver@123456!	Owns Truck T002 (Box Truck)
Driver	driver3@loadhitch.com	Driver@123456!	Owns Truck T003 (Tanker)
Driver	driver4@loadhitch.com	Driver@123456!	Owns Truck T004 (Refrigerated)
Customer	customer1@loadhitch.com	Customer@123456!	Has 2 loads (Electronics, Steel Beams)
Customer	customer2@loadhitch.com	Customer@123456!	Has 1 load (Liquid Chemicals)
Customer	customer3@loadhitch.com	Customer@123456!	Has 2 loads (Frozen Food, Furniture)
Customer	testauth999@example.com	Test@12345!	Extra test account