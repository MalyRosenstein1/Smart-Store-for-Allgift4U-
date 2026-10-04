#  Smart Store (Allgift4U) - AI-Powered Order Management System

An advanced, full-stack order and inventory management system designed using a decoupled client-server architecture. The platform features an autonomous chatbot agent capable of parsing natural language and user inputs to intelligently process sales and manage stocks.

##  Key Features

* ** Autonomous AI Agent:** Leverages smart intent recognition to parse free-text user orders (e.g., "חבילה לחתן") and automatically extract bundle items and quantities.
* ** Fallback Recommendation Engine:** Dynamically queries alternative templates when inventory is low, providing alternative recommendations in real time to prevent order loss.
* ** Document Generation:** Generates on-the-fly PDF Delivery Notes using QuestPDF upon successful orders, featuring a decoupled validation mechanism to prevent double stock reduction.
* ** Responsive UI:** A modern, detail-oriented design featuring interactive chat components, asynchronous state management, and real-time updates.

##  Tech Stack

* **Frontend:** Angular, TypeScript, TailwindCSS / SCSS.
* **Backend:** .NET Web API, C# (BLL & API architecture).
* **Database:** Microsoft SQL Server (MSSQL), Entity Framework Core.
* **Libraries:** QuestPDF (PDF compilation).

##  Architecture

The solution implements a robust architectural separation:
1. `SmartStore.API` - Handles request routing, controllers, and middleware execution.
2. `SmartStore.BLL` - Core business logic layers hosting service orchestrations, transaction handling, and recommendation models.
