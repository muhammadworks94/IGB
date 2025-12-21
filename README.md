# IGB Modern System

A modern, scalable IGB (International Geniuses Board) system built with .NET 8, ASP.NET Core MVC, and clean architecture principles.

## 🏗️ Architecture

- **IGB.Domain** - Domain entities and interfaces
- **IGB.Application** - Business logic and services
- **IGB.Infrastructure** - Data access and external services
- **IGB.Shared** - Shared utilities and DTOs
- **IGB.Web** - MVC web application

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- SQL Server (or SQL Server Express)
- Visual Studio 2022 or VS Code

### Setup

1. **Update Connection String**
   - Edit `IGB.Web/appsettings.json`
   - Update `DefaultConnection` with your SQL Server connection string

2. **Create Database**
   ```bash
   cd IGB.Web
   dotnet ef migrations add InitialCreate --project ../IGB.Infrastructure
   dotnet ef database update --project ../IGB.Infrastructure
   ```

3. **Run Application**
   ```bash
   cd IGB.Web
   dotnet run
   ```

4. **Access Application**
   - Navigate to `https://localhost:5001` (or the port shown in console)

## 📁 Project Structure

```
igb-new-version/
├── IGB.Domain/          # Domain layer
├── IGB.Application/     # Application layer (Services)
├── IGB.Infrastructure/  # Infrastructure layer
├── IGB.Shared/          # Shared utilities
└── IGB.Web/             # MVC Web application
    ├── Controllers/
    ├── Views/
    ├── ViewModels/
    └── wwwroot/         # Static files (NobleUI, CSS, JS)
```

## 🎨 UI Framework

- **NobleUI** - Bootstrap 5 admin dashboard template
- **Bootstrap 5** - CSS framework
- **Feather Icons** - Icon library
- **DataTables** - Table plugin

## 🔧 Features

- ✅ Clean Architecture
- ✅ Repository Pattern
- ✅ Unit of Work Pattern
- ✅ Service Layer Pattern
- ✅ AutoMapper for DTOs
- ✅ FluentValidation
- ✅ Serilog Logging
- ✅ Modern UI with NobleUI

## 📝 Next Steps

1. Add Authentication & Authorization
2. Implement remaining features (Courses, Lessons, etc.)
3. Add Redis caching
4. Add SignalR for real-time features
5. Add unit tests

## 📚 Documentation

See `PROJECT_SETUP_SUMMARY.md` for detailed setup information.

---

**Built with ❤️ using .NET 8 and Clean Architecture**

